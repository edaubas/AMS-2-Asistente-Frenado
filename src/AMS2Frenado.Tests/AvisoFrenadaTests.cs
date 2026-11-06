using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>
/// Presentacion del simbolo de frenada (opacidad y escala a ~60 Hz) y su uso desde el servicio completo.
/// La decision (rampa, "ya frenando", referencia) esta en <see cref="DecisorAvisoTests"/>.
/// La ventana WPF compila pero no se ejecuto.
/// </summary>
public sealed class AvisoFrenadaTests : IDisposable
{
    private const double Dt = 1 / 60.0;
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static List<double> Tick(AvisoFrenada a, ref double t, DecisionAviso d, double segundos)
    {
        var res = new List<double>();
        int n = (int)Math.Round(segundos / Dt);
        for (int i = 0; i < n; i++) { t += Dt; res.Add(a.Actualizar(d, t).Opacidad); }
        return res;
    }

    private static DecisionAviso Prog(double p, double expira = double.PositiveInfinity) => new(p, false, expira);

    [Fact]
    public void LaOpacidadSigueLaRampaSinDemoraNiRebotes()
    {
        var a = new AvisoFrenada(); double t = 0;
        // rampa de 2 s a 60 Hz alimentada a 27 Hz: la opacidad es el progreso (la subida no la frena)
        double ult = 0; int maxRetraso = 0;
        for (int i = 0; i <= 120; i++)
        {
            t += Dt;
            double p = i / 120.0;
            double o = a.Actualizar(Prog(p), t).Opacidad;
            Assert.True(o >= ult - 1e-12);
            Assert.InRange(p - o, -1e-9, 0.2);
            if (p - o > 0.02) maxRetraso++;
            ult = o;
        }
        Assert.Equal(1, ult, 9);
        Assert.Equal(0, maxRetraso);
    }

    [Fact]
    public void ArrancaEn0EnLaPrimeraVezQueSeVeYLlegaA1EnElPunto()
    {
        var a = new AvisoFrenada(); double t = 0;
        Assert.All(Tick(a, ref t, Prog(0), 1), o => Assert.Equal(0, o));
        var f = Tick(a, ref t, Prog(1), 0.2);
        // un salto (hueco de datos) se suaviza en 80 ms pero a los 100 ms ya esta completo
        Assert.InRange(f[0], 0.05, 0.4);
        Assert.Equal(1, f[5], 6);
    }

    [Fact]
    public void AlDesaparecerHaceUnFundidoCortoDe80msSinRebotes()
    {
        var a = new AvisoFrenada(); double t = 0;
        Tick(a, ref t, Prog(1), 0.3);
        var f = Tick(a, ref t, Prog(0), 0.2);
        Assert.Equal(0, f[^1]);
        for (int i = 1; i < f.Count; i++) Assert.True(f[i] <= f[i - 1] + 1e-12);
        Assert.True(f.TakeWhile(o => o > 0).Count() <= 5);          // 80 ms = 4,8 ticks a 60 Hz
    }

    [Fact]
    public void SeApagaSoloCuandoPasaElInstanteDeExpiracionAunqueNoLleguenMuestras()
    {
        var a = new AvisoFrenada(); double t = 0;
        var d = Prog(1, expira: 1.5);
        var f = Tick(a, ref t, d, 2.0);
        Assert.Equal(1, f[(int)(1.4 / Dt)], 6);
        Assert.Equal(0, f[^1]);
        Assert.InRange(f.Count(o => o > 0) * Dt, 1.5, 1.7);
    }

    [Fact]
    public void LaDuracionSeLimitaEntre1y2s()
    {
        var a = new AvisoFrenada();
        a.DuracionS = 0.1; Assert.Equal(1.0, a.DuracionS);
        a.DuracionS = 9; Assert.Equal(2.0, a.DuracionS);
        a.DuracionS = double.NaN; Assert.Equal(1.5, a.DuracionS);
        a.DuracionS = 1.2; Assert.Equal(1.2, a.DuracionS);
        Assert.Equal(1.0, AvisoFrenada.LimitarDuracion(-4));
        Assert.Equal(1.5, AvisoFrenada.DuracionPorDefectoS);
    }

    [Fact]
    public void DesactivadoNoMuestraYApagarloMientrasSeVeLoOculta()
    {
        var a = new AvisoFrenada { Habilitado = false }; double t = 0;
        Assert.All(Tick(a, ref t, Prog(1), 1), o => Assert.Equal(0, o));
        var b = new AvisoFrenada(); double t2 = 0;
        Tick(b, ref t2, Prog(1), 0.3);
        b.Habilitado = false;
        Assert.Equal(0, Tick(b, ref t2, Prog(1), 0.2)[^1]);
    }

    [Fact]
    public void LaEscalaAcompanaLaOpacidadDe085A1()
    {
        Assert.Equal(0.85, new CuadroAviso(0).Escala, 9);
        Assert.Equal(1.0, new CuadroAviso(1).Escala, 9);
        Assert.Equal(0.925, new CuadroAviso(0.5).Escala, 9);
        Assert.Equal(1.0, new CuadroAviso(7).Escala, 9);
    }

    [Fact]
    public void ProbarAvisoLoMuestraSinPistaAunDesactivadoYSeApagaSolo()
    {
        var a = new AvisoFrenada { Habilitado = false }; double t = 100;
        a.Probar(t);
        var f = Tick(a, ref t, DecisionAviso.Nada, 2.0);
        Assert.Equal(1, f[10], 6);
        Assert.Equal(0, f[^1]);
        Assert.InRange(f.Count(o => o > 0) * Dt, 1.4, 1.75);
    }

    [Fact]
    public void UnaPausaLargaDeRelojNoSaltaElFundido()
    {
        var a = new AvisoFrenada();
        a.Actualizar(Prog(0), 0);
        var c = a.Actualizar(Prog(1), 10);                          // 10 s despues: dt acotado, no aparece de golpe
        Assert.True(c.Opacidad < 1);
    }

    // ---------------------------------------------------------------- con el simulador (servicio completo)

    private static ResultadoLectura Ok(in SharedMemory sm) => new(EstadoLectura.Ok, ConstantesShm.Version, sm);

    [Fact]
    public void ConElSimuladorElSimboloLlegaAlCienPorCientoEnElPuntoYPitaUnaVezPorTramo()
    {
        var sim = new Simulador(ias: 3);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < 9; i++) s.Paso(Ok(sim.Paso()), sim.Tiempo);
        s.Modo = ModoApp.Mostrar;
        s.Pitido = true;

        // El pedal del simulador ya esta apretado al llegar al punto de la guia (la guia usa la mediana IA/vos), asi que se
        // reemplaza por un "conductor" que reacciona 0,4 s despues de que aparece FRENA en las curvas pares y nunca
        // reacciona en las impares (el simbolo se va solo por tiempo).
        double off = 1000, tCuadro = off;
        int tramos = 0, pitidos = 0;
        bool enFrenaPrev = false; int curvaPrev = -1; double tFrena = 0; bool tramoImpar = false;
        double opMax = 0, frenoAltoDesde = double.NaN;
        var llegadas = new List<double>();
        uint objetivo = sim.Autos[0].Vueltas + 4;
        for (int i = 0; i < 400_000 && sim.Autos[0].Vueltas < objetivo; i++)
        {
            var sm = sim.Paso();
            double t = off + sim.Tiempo;
            sm.mBrake = enFrenaPrev && !tramoImpar && t - tFrena >= 0.4 ? 0.8f : 0f;
            var v = s.Paso(Ok(sm), t);
            if (v.Pitar) { pitidos++; Assert.True(v.Freno <= 0.15, "pito con el pedal apretado"); }
            bool frena = v.Guia is { Estado: EstadoPanel.Frena };
            int curva = frena ? v.Guia!.IndiceCurva : -1;
            bool llega = frena && (!enFrenaPrev || curva != curvaPrev);
            if (llega) { tramos++; tFrena = t; tramoImpar = tramos % 2 == 1; }
            // la ventana se actualiza a 60 Hz aunque la telemetria llegue a 30 Hz
            bool primero = true;
            for (; tCuadro <= t; tCuadro += Dt)
            {
                var c = s.ActualizarAviso(tCuadro);
                opMax = Math.Max(opMax, c.Opacidad);
                if (llega && primero) llegadas.Add(c.Opacidad);      // el primer cuadro de la ventana tras llegar al punto
                primero = false;
                if (sm.mBrake > 0.15f) { if (double.IsNaN(frenoAltoDesde)) frenoAltoDesde = tCuadro; } else frenoAltoDesde = double.NaN;
                if (!double.IsNaN(frenoAltoDesde) && tCuadro - frenoAltoDesde > 0.15) Assert.False(c.Visible, "se vio el aviso con el freno apretado");
            }
            enFrenaPrev = frena; curvaPrev = curva;
        }
        Assert.True(tramos >= 6, $"{tramos} tramos");
        Assert.Equal(tramos, pitidos);                                    // un pitido por tramo de FRENA
        Assert.Equal(tramos, llegadas.Count);
        Assert.All(llegadas, o => Assert.True(o >= 0.97, $"el simbolo tenia {o:0.00} al llegar al punto"));    // no llega tarde
        Assert.Equal(1, opMax, 6);
    }

    [Fact]
    public void ElServicioPasaElPedalYLaConfiguracionAlAviso()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        s.AvisoDuracionS = 7;
        Assert.Equal(2.0, s.AvisoDuracionS);
        s.AvisoRampaS = 9;
        Assert.Equal(3.0, s.AvisoRampaS);
        s.AvisoRampaS = 2;
        s.AvisoActivo = false;
        Assert.False(s.AvisoActivo);
        var sim = new Simulador();
        var sm = sim.Paso(); sm.mBrake = 0.6f;
        var v = s.Paso(Ok(sm), 1);
        Assert.Equal(0.6, v.Freno, 5);
        s.Paso(ResultadoLectura.SinJuego, 2);
        Assert.False(s.ActualizarAviso(3).Visible);
        s.ProbarAviso(10);
        double tt = 10; bool visto = false;
        for (int i = 0; i < 180; i++) { tt += Dt; visto |= s.ActualizarAviso(tt).Visible; }
        Assert.True(visto);
        Assert.False(s.ActualizarAviso(tt + Dt).Visible);                   // 3 s despues ya se apago solo
    }
}
