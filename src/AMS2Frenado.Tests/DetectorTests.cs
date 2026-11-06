using AMS2Frenado.Core.Grabacion;
using Xunit;

namespace AMS2Frenado.Tests;

public class DetectorTests
{
    private const double Largo = 3000;

    /// <summary>
    /// Perfil sintetico a ~27 Hz: recto a v0, decelera 'decel' m/s2 hasta vMin, mantiene y vuelve a acelerar.
    /// Devuelve la lista de (d, v, freno, acel, t) y el metro donde empieza a decelerar.
    /// </summary>
    private static (List<(double d, double v, double fr, double ac, double t)> m, double inicio) Perfil(
        double v0, double vMin, double decel, bool conPedal = true, double dt = 0.037, double hueco = 0)
    {
        var l = new List<(double, double, double, double, double)>();
        double d = 500, v = v0, t = 0;
        double? inicio = null;
        for (int i = 0; i < 1600; i++)
        {
            double tt = t;
            if (i > 0 && hueco > 0 && i == 90) tt += hueco;     // pausa grande en el medio de la frenada
            t = tt;
            double fr = 0, ac = 1;
            bool frenando = i >= 40 && i < 200 && v > vMin;
            if (frenando) { inicio ??= d; v -= decel * dt; fr = conPedal ? Math.Min(1, decel / 12) : 0; ac = 0; }
            else if (i >= 200 && v < v0) v = Math.Min(v0, v + 5 * dt);
            d += v * dt; t += dt;
            l.Add((d % Largo, v, fr, ac, t));
        }
        return (l, inicio ?? 0);
    }

    private static List<EventoFrenada> Correr(DetectorFrenada det, List<(double d, double v, double fr, double ac, double t)> m, bool pedal)
    {
        var s = new List<EventoFrenada>();
        foreach (var x in m) det.Actualizar(x.d, x.v, pedal ? x.fr : null, x.t, 1, s);
        return s;
    }

    [Fact]
    public void FrenadaFuertePorVelocidad_ArrancaCercaDelInicioReal()
    {
        var (m, inicio) = Perfil(70, 25, 8);
        var ev = Correr(new DetectorFrenada(Largo, ModoDetector.Velocidad, ParametrosDetector.Fuerte), m, false);
        var e = Assert.Single(ev);
        Assert.Equal(TipoCurva.Frenada, e.Tipo);
        Assert.InRange(e.FrenarEn, inicio - 5, inicio + 40);   // el suavizado atrasa un poco, pero poco
        Assert.InRange(e.VMin * 3.6, 25 * 3.6 - 3, 25 * 3.6 + 3);
        Assert.True(e.LargoFrenada > 150);
    }

    [Fact]
    public void FrenadaFuertePorPedal()
    {
        var (m, inicio) = Perfil(70, 25, 8);
        var ev = Correr(new DetectorFrenada(Largo, ModoDetector.Pedal, ParametrosDetector.Fuerte), m, true);
        var e = Assert.Single(ev);
        Assert.Equal(TipoCurva.Frenada, e.Tipo);
        Assert.InRange(e.FrenarEn, inicio - 5, inicio + 10);
    }

    [Fact]
    public void ToqueLoVeElDetectorSuaveYNoElFuerte()
    {
        var (m, _) = Perfil(70, 64, 5);              // cae 6 m/s = 21,6 km/h
        Assert.Empty(Correr(new DetectorFrenada(Largo, ModoDetector.Velocidad, ParametrosDetector.Fuerte), m, false));
        var ev = Correr(new DetectorFrenada(Largo, ModoDetector.Velocidad, ParametrosDetector.Suave), m, false);
        var e = Assert.Single(ev);
        Assert.Equal(TipoCurva.Toque, e.Tipo);
        Assert.Equal(2, e.Prioridad);
    }

    [Fact]
    public void CaidaMenorA10KmhEsLevantaEnLaIa()
    {
        var (m, _) = Perfil(70, 67.5, 2.5);          // cae 2,5 m/s = 9 km/h
        var ev = Correr(new DetectorFrenada(Largo, ModoDetector.Velocidad, ParametrosDetector.Suave), m, false);
        Assert.Equal(TipoCurva.Levanta, Assert.Single(ev).Tipo);
    }

    [Fact]
    public void LevantadaPorAceleradorSinFreno()
    {
        var (m, inicio) = Perfil(70, 68.5, 2.5, conPedal: false);
        var det = new DetectorLevantada(Largo);
        var s = new List<EventoFrenada>();
        foreach (var x in m) det.Actualizar(x.d, x.v, x.fr, x.ac, x.t, 1, s);
        var e = Assert.Single(s);
        Assert.Equal(TipoCurva.Levanta, e.Tipo);
        Assert.Equal(1, e.Prioridad);
        Assert.InRange(e.FrenarEn, inicio - 5, inicio + 10);
    }

    [Fact]
    public void LevantadaConFrenoSeDescarta()
    {
        var (m, _) = Perfil(70, 25, 8);              // acelerador a 0 pero con freno: es una frenada
        var det = new DetectorLevantada(Largo);
        var s = new List<EventoFrenada>();
        foreach (var x in m) det.Actualizar(x.d, x.v, x.fr, x.ac, x.t, 1, s);
        Assert.Empty(s);
    }

    [Fact]
    public void HuecoGrandeReiniciaElDetectorSinEventosFalsos()
    {
        // pausa de 0,45 s (como la de la captura real) en plena frenada: no debe inventar una frenada
        var (m, _) = Perfil(70, 25, 8, hueco: 0.45);
        var ev = Correr(new DetectorFrenada(Largo, ModoDetector.Velocidad, ParametrosDetector.Fuerte), m, false);
        Assert.All(ev, e => Assert.True(e.Caida >= 30 / 3.6 && e.LargoFrenada > 0));
        Assert.True(ev.Count <= 1);
    }

    [Fact]
    public void ModoPedalSinFrenoLanza()
    {
        var det = new DetectorFrenada(Largo, ModoDetector.Pedal);
        Assert.Throws<ArgumentException>(() => det.Actualizar(10, 50, null, 0, 0, []));
    }

    [Fact]
    public void RectaSinFrenarNoGeneraEventos()
    {
        var m = Enumerable.Range(0, 800).Select(i => (d: 100 + i * 1.9, v: 70.0, fr: 0.0, ac: 1.0, t: i * 0.037)).ToList();
        Assert.Empty(Correr(new DetectorFrenada(Largo, ModoDetector.Velocidad, ParametrosDetector.Suave), m, false));
    }

    [Theory]
    [InlineData(35, TipoCurva.Frenada)]
    [InlineData(30, TipoCurva.Frenada)]
    [InlineData(29, TipoCurva.Toque)]
    [InlineData(10, TipoCurva.Toque)]
    [InlineData(9.9, TipoCurva.Levanta)]
    [InlineData(0, TipoCurva.Levanta)]
    public void ClasificaPorCaidaEnKmh(double kmh, TipoCurva esperado)
        => Assert.Equal(esperado, Tipos.Clasificar(kmh / 3.6));
}
