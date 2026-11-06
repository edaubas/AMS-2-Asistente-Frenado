using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>Logica del aviso de frenada (cuando mostrar y ocultar). La ventana WPF compila pero no se ejecuto.</summary>
public sealed class AvisoFrenadaTests : IDisposable
{
    private const double Dt = 1 / 60.0;
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static PanelGuia P(EstadoPanel e, int curva = 3) => new() { Estado = e, IndiceCurva = curva, Visible = true };

    /// <summary>Avanza el aviso 'segundos' a 60 Hz con ese panel y freno constantes; devuelve la opacidad final y todas las intermedias.</summary>
    private static List<double> Tick(AvisoFrenada a, ref double t, PanelGuia? p, double freno, double segundos)
    {
        var res = new List<double>();
        int n = (int)Math.Round(segundos / Dt);
        for (int i = 0; i < n; i++) { t += Dt; res.Add(a.Actualizar(p, freno, t).Opacidad); }
        return res;
    }

    [Fact]
    public void ApareceAlEntrarEnFrenaConFundidoDe80msYNoAntes()
    {
        var a = new AvisoFrenada(); double t = 0;
        Assert.All(Tick(a, ref t, P(EstadoPanel.Lejos), 0, 1), o => Assert.Equal(0, o));
        Assert.All(Tick(a, ref t, P(EstadoPanel.Aproximacion), 0, 1), o => Assert.Equal(0, o));
        Assert.All(Tick(a, ref t, P(EstadoPanel.Inminente), 0, 1), o => Assert.Equal(0, o));

        var f = Tick(a, ref t, P(EstadoPanel.Frena), 0, 0.2);
        Assert.InRange(f[0], 0.05, 0.4);                         // primer tick: ya empieza a aparecer
        Assert.Equal(1, f[5], 6);                                // a los 6 ticks (100 ms) ya esta completo
        // el fundido sube sin retrocesos (nada de parpadeo)
        for (int i = 1; i < f.Count; i++) Assert.True(f[i] >= f[i - 1] - 1e-12);
        Assert.True(f.TakeWhile(o => o < 1).Count() <= 5);       // 80 ms = 4,8 ticks a 60 Hz
    }

    [Fact]
    public void DesapareceCuandoElUsuarioFrenaPorEncimaDelUmbral()
    {
        var a = new AvisoFrenada(); double t = 0;
        Tick(a, ref t, P(EstadoPanel.Frena), 0, 0.3);
        // justo en el umbral no cuenta como frenar
        Assert.All(Tick(a, ref t, P(EstadoPanel.Frena), 0.15, 0.1), o => Assert.Equal(1, o));
        var f = Tick(a, ref t, P(EstadoPanel.Frena), 0.16, 0.2);
        Assert.Equal(0, f[^1]);
        for (int i = 1; i < f.Count; i++) Assert.True(f[i] <= f[i - 1] + 1e-12);   // fundido de salida sin rebotes
        Assert.True(f.TakeWhile(o => o > 0).Count() <= 5);                          // 80 ms
        // y no vuelve a aparecer en esa curva aunque suelte el freno
        Assert.All(Tick(a, ref t, P(EstadoPanel.Frena), 0, 1), o => Assert.Equal(0, o));
    }

    [Fact]
    public void SiNoFrenaDesapareceALos1_5sPorDefecto()
    {
        var a = new AvisoFrenada(); double t = 0;
        var f = Tick(a, ref t, P(EstadoPanel.Frena), 0, 2.0);
        Assert.Equal(1, f[(int)(1.4 / Dt)], 6);                  // a 1,4 s sigue
        Assert.Equal(0, f[^1]);                                  // a 2,0 s ya se fue
        Assert.True(f.Count(o => o > 0) * Dt is > 1.5 and < 1.75);
        Assert.Equal(1.5, AvisoFrenada.DuracionPorDefectoS);
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

        double t = 0;
        a.DuracionS = 2.0;
        var f = Tick(a, ref t, P(EstadoPanel.Frena), 0, 2.4);
        Assert.Equal(1, f[(int)(1.9 / Dt)], 6);
        Assert.Equal(0, f[^1]);
    }

    [Fact]
    public void DesapareceAlPasarElVerticeAunqueNoHayaPasadoElTiempo()
    {
        var a = new AvisoFrenada(); double t = 0;
        Tick(a, ref t, P(EstadoPanel.Frena), 0, 0.3);
        // la guia cambia de curva (paso el vertice)
        var f = Tick(a, ref t, P(EstadoPanel.Lejos, curva: 4), 0, 0.2);
        Assert.Equal(0, f[^1]);
        Assert.True(f.TakeWhile(o => o > 0).Count() <= 5);
        // o sin lectura valida
        double t2 = 0; var b = new AvisoFrenada();
        Tick(b, ref t2, P(EstadoPanel.Frena), 0, 0.3);
        Assert.Equal(0, Tick(b, ref t2, null, 0, 0.2)[^1]);
    }

    [Fact]
    public void NoVuelveAAparecerEnLaMismaCurvaEnLaMismaPasada()
    {
        var a = new AvisoFrenada(); double t = 0;
        Tick(a, ref t, P(EstadoPanel.Frena), 0, 2.0);                    // aparece y se va por tiempo
        Assert.All(Tick(a, ref t, P(EstadoPanel.Frena), 0, 1), o => Assert.Equal(0, o));

        // retroceso de estado (la guia baja a Inminente y vuelve a FRENA) en la misma curva: tampoco
        Assert.All(Tick(a, ref t, P(EstadoPanel.Inminente), 0, 1), o => Assert.Equal(0, o));
        Assert.All(Tick(a, ref t, P(EstadoPanel.Frena), 0, 1), o => Assert.Equal(0, o));

        // en la vuelta siguiente (pasa por Lejos) vuelve a avisar
        Tick(a, ref t, P(EstadoPanel.Lejos), 0, 1);
        Assert.Equal(1, Tick(a, ref t, P(EstadoPanel.Frena), 0, 0.3)[^1], 6);
    }

    [Fact]
    public void UnaChicaneAvisaCadaCurvaPorSeparado()
    {
        var a = new AvisoFrenada(); double t = 0;
        Assert.Equal(1, Tick(a, ref t, P(EstadoPanel.Frena, 3), 0, 0.3)[^1], 6);
        Tick(a, ref t, P(EstadoPanel.Frena, 3), 0.8, 0.3);               // frena: se oculta
        Assert.Equal(0, Tick(a, ref t, P(EstadoPanel.Frena, 3), 0.8, 0.3)[^1]);
        // curva siguiente directo en FRENA (sin pasar por Lejos)
        Assert.Equal(1, Tick(a, ref t, P(EstadoPanel.Frena, 4), 0, 0.3)[^1], 6);
    }

    [Fact]
    public void SiYaVeniasFrenandoAlLlegarNoAparece()
    {
        var a = new AvisoFrenada(); double t = 0;
        Assert.All(Tick(a, ref t, P(EstadoPanel.Frena), 0.7, 1), o => Assert.Equal(0, o));
        Assert.All(Tick(a, ref t, P(EstadoPanel.Frena), 0, 1), o => Assert.Equal(0, o));      // y soltar no lo dispara
    }

    [Fact]
    public void ToqueYLevantaNuncaAvisan()
    {
        var a = new AvisoFrenada(); double t = 0;
        Assert.All(Tick(a, ref t, P(EstadoPanel.Toque), 0, 1), o => Assert.Equal(0, o));
        Assert.All(Tick(a, ref t, P(EstadoPanel.Levanta), 0, 1), o => Assert.Equal(0, o));
    }

    [Fact]
    public void DesactivadoNoAvisaYApagarloMientrasSeVeLoOculta()
    {
        var a = new AvisoFrenada { Habilitado = false }; double t = 0;
        Assert.All(Tick(a, ref t, P(EstadoPanel.Frena), 0, 1), o => Assert.Equal(0, o));

        var b = new AvisoFrenada(); double t2 = 0;
        Tick(b, ref t2, P(EstadoPanel.Frena, 8), 0, 0.3);
        b.Habilitado = false;
        Assert.Equal(0, Tick(b, ref t2, P(EstadoPanel.Frena, 8), 0, 0.2)[^1]);
    }

    [Fact]
    public void ProbarAvisoLoMuestraSinPistaAunDesactivadoYSeApagaSolo()
    {
        var a = new AvisoFrenada { Habilitado = false }; double t = 100;
        a.Probar(t);
        var f = Tick(a, ref t, null, 0, 2.0);
        Assert.Equal(1, f[10], 6);
        Assert.Equal(0, f[^1]);
        Assert.InRange(f.Count(o => o > 0) * Dt, 1.4, 1.75);
    }

    [Fact]
    public void ElFrenoNoValidoSeTomaComoQueNoFrena()
    {
        var a = new AvisoFrenada(); double t = 0;
        Assert.Equal(1, Tick(a, ref t, P(EstadoPanel.Frena), double.NaN, 0.3)[^1], 6);
    }

    [Fact]
    public void UnaPausaLargaDeRelojNoSaltaElFundido()
    {
        var a = new AvisoFrenada();
        a.Actualizar(P(EstadoPanel.Lejos), 0, 0);
        var c = a.Actualizar(P(EstadoPanel.Frena), 0, 10);              // 10 s despues: dt acotado, no aparece de golpe
        Assert.True(c.Opacidad < 1);
    }

    // ---------------------------------------------------------------- con el simulador (servicio completo)

    private static ResultadoLectura Ok(in SharedMemory sm) => new(EstadoLectura.Ok, ConstantesShm.Version, sm);

    [Fact]
    public void ConElSimuladorAvisaUnaVezPorTramoDeFrenaYNuncaMientrasFrenas()
    {
        var sim = new Simulador(ias: 3);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < 9; i++) s.Paso(Ok(sim.Paso()), sim.Tiempo);
        s.Modo = ModoApp.Mostrar;

        // El pedal del simulador ya esta apretado al llegar al punto de la guia (la guia usa la mediana IA/vos),
        // asi que se reemplaza por un "conductor" que reacciona 0,4 s despues de que aparece FRENA en las curvas
        // pares y nunca reacciona en las impares (el aviso se va solo por tiempo).
        double off = 1000, tCuadro = off;
        int avisos = 0, tramos = 0, impares = 0, pares = 0;
        bool enFrenaPrev = false; int curvaPrev = -1; double tFrena = 0;
        bool visiblePrev = false; double inicio = 0, maxVisible = 0, frenoAltoDesde = double.NaN;
        double durMaxImpar = 0, durMaxPar = 0; bool tramoImpar = false;
        uint objetivo = sim.Autos[0].Vueltas + 4;
        for (int i = 0; i < 400_000 && sim.Autos[0].Vueltas < objetivo; i++)
        {
            var sm = sim.Paso();
            double t = off + sim.Tiempo;
            sm.mBrake = enFrenaPrev && !tramoImpar && t - tFrena >= 0.4 ? 0.8f : 0f;
            var v = s.Paso(Ok(sm), t);
            // la ventana se actualiza a 60 Hz aunque la telemetria llegue a 30 Hz
            for (; tCuadro <= t; tCuadro += Dt)
            {
                bool vis = s.ActualizarAviso(tCuadro).Visible;
                if (vis && !visiblePrev) { avisos++; inicio = tCuadro; }
                if (vis) { maxVisible = Math.Max(maxVisible, tCuadro - inicio); if (tramoImpar) durMaxImpar = Math.Max(durMaxImpar, tCuadro - inicio); else durMaxPar = Math.Max(durMaxPar, tCuadro - inicio); }
                visiblePrev = vis;
                // con el pedal apretado mas de 0,15 s (muestra + fundido de 80 ms) el aviso ya no se ve
                if (sm.mBrake > 0.15f) { if (double.IsNaN(frenoAltoDesde)) frenoAltoDesde = tCuadro; } else frenoAltoDesde = double.NaN;
                if (!double.IsNaN(frenoAltoDesde) && tCuadro - frenoAltoDesde > 0.15) Assert.False(vis, "se vio el aviso con el freno apretado");
            }
            bool frena = v.Guia is { Estado: EstadoPanel.Frena };
            int curva = frena ? v.Guia!.IndiceCurva : -1;
            if (frena && (!enFrenaPrev || curva != curvaPrev))
            {
                tramos++; tFrena = t; tramoImpar = tramos % 2 == 1;
                if (tramoImpar) impares++; else pares++;
            }
            enFrenaPrev = frena; curvaPrev = curva;
        }
        Assert.True(tramos >= 6, $"{tramos} tramos");
        Assert.Equal(tramos, avisos);                       // exactamente un aviso por tramo de FRENA: ni falta ni se repite
        Assert.True(maxVisible <= 1.5 + AvisoFrenada.FundidoS + 0.1, $"se vio {maxVisible:0.00} s");
        Assert.InRange(durMaxPar, 0.3, 0.8);                // el conductor reacciona a los 0,4 s
        Assert.InRange(durMaxImpar, 1.4, 1.7);              // el que no reacciona: ~1,5 s
        Assert.True(impares > 0 && pares > 0);
    }

    [Fact]
    public void ElServicioPasaElPedalYLaConfiguracionAlAviso()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        s.AvisoDuracionS = 7;
        Assert.Equal(2.0, s.AvisoDuracionS);
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
