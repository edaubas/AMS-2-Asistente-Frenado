using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;
using Xunit.Abstractions;

namespace AMS2Frenado.Tests;

/// <summary>
/// Sincronia pitido / simbolo y anticipacion (T_aviso = punto de frenado - anticipacion). Todo es logica pura del Core:
/// la ventana WPF y <c>SoundPlayer</c> compilan pero no se ejecutaron (su latencia propia esta documentada, no medida).
/// Con AMS2_CAPTURA_PRUEBA se repite sobre la captura real (sin ella esas pruebas no hacen nada).
/// </summary>
public sealed class SincroniaAvisoTests : IDisposable
{
    private const double Tick = 1 / 60.0;                    // ventana del simbolo
    private readonly ITestOutputHelper _out;
    private readonly string _dir = Simulador.CarpetaTemporal();
    public SincroniaAvisoTests(ITestOutputHelper o) => _out = o;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    // ---------------------------------------------------------------- decisor + presentacion, a distintas velocidades

    /// <summary>Panel como lo daria el motor: ventana por tiempo (5 s a la velocidad actual, entre 120 y 400 m) e Inminente a menos de 100 m.</summary>
    private static PanelGuia P(double metros, double v, int curva = 3, ReferenciaGuia principal = ReferenciaGuia.Ia)
    {
        double ventana = Math.Clamp(5 * v, 120, 400);
        var e = metros <= 0 ? EstadoPanel.Frena : metros < 100 ? EstadoPanel.Inminente : metros <= ventana ? EstadoPanel.Aproximacion : EstadoPanel.Lejos;
        return new PanelGuia
        {
            Estado = e, Visible = e != EstadoPanel.Lejos, IndiceCurva = curva, Tipo = TipoCurva.Frenada, Principal = principal,
            MetrosHastaFrenar = Math.Max(0, metros),
        };
    }

    private sealed record Paso(double T, bool Muestra, bool Pitar, double Op, double Progreso);

    /// <summary>
    /// Acerca un auto al punto a velocidad constante con muestras cada 'periodo' s y la ventana a 60 Hz como en la app:
    /// en cada tick, si toca muestra se decide y se pinta en el MISMO tick (Evaluar y luego Actualizar).
    /// </summary>
    private static List<Paso> Pasada(double v, double anticipacion, double periodo, double desde = 500, double rampa = 2.0,
        Func<double, double>? freno = null, (double desde, double hasta)? hueco = null, double extra = 3.0)
    {
        var dec = new DecisorAviso { RampaS = rampa, AnticipacionS = anticipacion };
        var av = new AvisoFrenada();
        var res = new List<Paso>();
        double tSig = 0, tPunto = desde / v;
        DecisionAviso d = DecisionAviso.Nada;
        for (double t = 0; t < tPunto + extra; t += Tick)
        {
            bool muestra = t >= tSig - 1e-9 && !(hueco is { } h && t >= h.desde && t < h.hasta);
            bool pitar = false;
            if (muestra)
            {
                double m = Math.Max(0, desde - v * t);
                d = dec.Evaluar(P(m, v), ReferenciaGuia.Ia, v, freno?.Invoke(m) ?? 0, t);
                pitar = d.Pitar;
                tSig += periodo;
            }
            // la ventana usa la ultima decision; Pitar solo vale en su propio tick
            var c = av.Actualizar(d, t);
            res.Add(new Paso(t, muestra, pitar, c.Opacidad, d.Progreso));
        }
        return res;
    }

    public static IEnumerable<object[]> Combinaciones()
    {
        foreach (double v in new[] { 100 / 3.6, 216 / 3.6, 300 / 3.6 })
            foreach (double a in new[] { 0, 0.5, 1.0, 2.0 })
                foreach (double hz in new[] { 27.0, 39.0 })
                    yield return [v, a, hz];
    }

    [Theory]
    [MemberData(nameof(Combinaciones))]
    public void ElSimboloLlegaA1EnLaMismaMuestraDelPitidoAntesDelPuntoYSinParpadeo(double v, double a, double hz)
    {
        var r = Pasada(v, a, 1 / hz);
        var pitidos = r.Where(x => x.Pitar).ToList();
        Assert.Single(pitidos);                                      // una sola vez por pasada
        var p = pitidos[0];

        // en el tick del pitido el simbolo esta al 100 % ("nunca mas de un tick despues": cero ticks)
        Assert.True(p.Op >= 0.98, $"opacidad {p.Op:0.000} en el pitido");
        Assert.Equal(1.0, p.Op, 9);
        // y nunca llego al 100 % antes
        Assert.All(r.Where(x => x.T < p.T), x => Assert.True(x.Op < 1.0 - 1e-9, $"al 100 % antes del pitido (t={x.T:0.000})"));
        // T_aviso: el pitido cae A segundos antes del punto (con A = 0, en el punto) dentro de una muestra de telemetria
        double tPunto = 500 / v;
        Assert.InRange(tPunto - p.T, a - 1 / hz - Tick - 1e-9, a + 1e-6);

        // la rampa no retrocede ni parpadea: sube monotona hasta el pitido y se queda en 1
        var antes = r.Where(x => x.T <= p.T).Select(x => x.Op).ToList();
        for (int i = 1; i < antes.Count; i++) Assert.True(antes[i] >= antes[i - 1] - 1e-12);
        Assert.All(r.Where(x => x.T >= p.T && x.T < tPunto + 1.5 - 2 * Tick), x => Assert.Equal(1.0, x.Op, 9));
        // el simbolo pleno dura hasta 1,5 s despues del PUNTO (no despues del pitido) y se apaga solo
        double ultimoVisible = r.Last(x => x.Op > 0).T;
        Assert.InRange(ultimoVisible, tPunto + 1.5 - 2 * Tick, tPunto + 1.5 + 0.2);
        // una sola subida y una sola bajada (sin parpadeo)
        int cambios = 0; for (int i = 1; i < r.Count; i++) if ((r[i].Op > 0) != (r[i - 1].Op > 0)) cambios++;
        Assert.Equal(2, cambios);
    }

    [Theory]
    [InlineData(100 / 3.6, 0.0)]
    [InlineData(216 / 3.6, 1.0)]
    [InlineData(300 / 3.6, 2.0)]
    public void LaRampaEmpiezaRampaSAntesDeTAvisoYTerminaEnTAviso(double v, double a)
    {
        var r = Pasada(v, a, 1 / 27.0);
        double tAviso = 500 / v - a;
        var primero = r.First(x => x.Progreso > 0);
        // a v = 300 km/h la ventana de la guia (400 m = 4,8 s) recorta un poco el inicio con anticipacion 2 s + rampa 2 s: lo permitido es 1 muestra
        Assert.InRange(tAviso - primero.T, 2.0 - 1 / 27.0 - Tick, 2.0 + 1e-9);
        var pit = r.Single(x => x.Pitar);
        Assert.InRange(pit.T - tAviso, -1e-9, 1 / 27.0 + Tick);
    }

    [Fact]
    public void ConHuecoDe450msEnLaRampaOEnTAvisoSigueSincronizado()
    {
        const double v = 216 / 3.6;
        foreach (double a in new[] { 0, 0.5, 1.0, 2.0 })
        {
            double tAviso = 500 / v - a;
            // hueco que contiene T_aviso, uno justo antes y uno en la mitad de la rampa
            foreach (double ini in new[] { tAviso - 0.2, tAviso - 0.45, tAviso - 1.0, tAviso - 0.05 })
            {
                var r = Pasada(v, a, 1 / 27.0, hueco: (ini, ini + 0.45));
                var pit = r.Where(x => x.Pitar).ToList();
                Assert.Single(pit);
                Assert.Equal(1.0, pit[0].Op, 9);
                Assert.All(r.Where(x => x.T < pit[0].T), x => Assert.True(x.Op < 1 - 1e-9));
                var ops = r.Select(x => x.Op).ToList();
                for (int i = 1; i < ops.Count && r[i].T <= pit[0].T; i++) Assert.True(ops[i] >= ops[i - 1] - 1e-12);   // sin retroceso
                // el hueco no hace que el pitido llegue mas de una muestra despues de T_aviso... mas el hueco entero si T_aviso cae adentro
                Assert.InRange(pit[0].T - tAviso, -1e-9, 0.45 + 1 / 27.0 + Tick);
            }
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void YaFrenandoEnTAvisoNoPitaNiMuestra(double a)
    {
        const double v = 216 / 3.6;
        // el pedal se aprieta justo cuando llega T_aviso (dentro de la zona inmediata de T_aviso): no pita, no se ve, aunque sueltes
        double mAviso = v * a;                                       // metros al punto en T_aviso
        var r = Pasada(v, a, 1 / 27.0, freno: m => m <= mAviso + 0.5 * v / 27 ? 0.8 : 0);
        Assert.DoesNotContain(r, x => x.Pitar);
        Assert.All(r.Where(x => x.T > 500 / v - a + 0.2), x => Assert.Equal(0, x.Op, 9));     // tras el fundido corto de salida (80 ms)

        // frenar antes de la zona inmediata (3 s antes de T_aviso) solo oculta mientras dura: despues pita igual si soltas
        var lejos = Pasada(v, a, 1 / 27.0, freno: m => m is < 500 - 0 and > 0 && (500 - m) / v is > 0.2 and < 0.4 ? 0.8 : 0);
        Assert.Single(lejos, x => x.Pitar);
    }

    [Fact]
    public void ConAnticipacionFrenarEntreElPitidoYElPuntoApagaElSimbolo()
    {
        const double v = 216 / 3.6; const double a = 1.0;
        double mAviso = v * a;
        // pita en T_aviso y el conductor frena 0,4 s despues (antes del punto): el simbolo se va y no vuelve
        var r = Pasada(v, a, 1 / 27.0, freno: m => m < mAviso - 0.4 * v ? 0.9 : 0);
        var pit = r.Single(x => x.Pitar);
        var despues = r.Where(x => x.T > pit.T + 0.4 + 0.2).ToList();
        Assert.All(despues, x => Assert.Equal(0, x.Op, 9));
        Assert.Equal(1.0, pit.Op, 9);
    }

    [Fact]
    public void LaAnticipacionSeCalculaConLaVelocidadActualYElMismoTiempoRestanteQueLaRampa()
    {
        // A x velocidad en metros: a 60 m/s y A = 1,5 s el pitido sale cuando faltan <= 90 m (una muestra de margen)
        var dec = new DecisorAviso { AnticipacionS = 1.5 };
        double m = 300, t = 0; const double v = 60;
        DecisionAviso d;
        do { d = dec.Evaluar(P(m, v), ReferenciaGuia.Ia, v, 0, t); if (d.Pitar) break; m -= v / 30; t += 1.0 / 30; } while (m > 0);
        Assert.True(d.Pitar);
        Assert.InRange(m, 90 - v / 30, 90);
        Assert.Equal(1.0, d.Progreso, 9);
    }

    [Fact]
    public void LaReferenciaPrincipalMandaTambienConAnticipacion()
    {
        const double v = 50;
        var dec = new DecisorAviso { AnticipacionS = 1.0 };
        double t = 0;
        for (double m = 300; m >= 0; m -= v / 27, t += 1 / 27.0)
        {
            var d = dec.Evaluar(P(m, v, principal: ReferenciaGuia.Vos), ReferenciaGuia.Ia, v, 0, t);   // la guia cae a vos, se eligio IA
            Assert.Equal(0, d.Progreso); Assert.False(d.Pitar);
        }
        var dec2 = new DecisorAviso { AnticipacionS = 1.0 };
        bool pito = false; t = 0;
        for (double m = 300; m >= 0; m -= v / 27, t += 1 / 27.0)
            pito |= dec2.Evaluar(P(m, v, principal: ReferenciaGuia.Vos), ReferenciaGuia.Vos, v, 0, t).Pitar;
        Assert.True(pito);
    }

    [Fact]
    public void NoRepiteEnLaMismaPasadaConAnticipacionAunqueLaVelocidadCaiga()
    {
        // pita en T_aviso; despues levantas: el tiempo restante sube por encima de A, pero ya se aviso (no repite, no baja)
        var dec = new DecisorAviso { AnticipacionS = 1.0 };
        double m = 200, v = 60, t = 0; int pitidos = 0; double ult = 0;
        while (m > 0)
        {
            var d = dec.Evaluar(P(m, v), ReferenciaGuia.Ia, v, 0, t);
            if (d.Pitar) pitidos++;
            Assert.True(d.Progreso >= ult - 1e-12);
            ult = d.Progreso;
            m -= v / 27; t += 1 / 27.0;
            if (pitidos > 0) v = Math.Max(10, v - 1.5);      // levanta fuerte despues de T_aviso
        }
        Assert.Equal(1, pitidos);
        Assert.Equal(1.0, ult, 9);
    }

    [Fact]
    public void PasadaNuevaCuandoVuelveALejos()
    {
        var dec = new DecisorAviso { AnticipacionS = 0.5 };
        const double v = 50; double t = 0;
        int pit = 0;
        for (int vuelta = 0; vuelta < 3; vuelta++)
        {
            for (double m = 400; m >= 0; m -= v / 27, t += 1 / 27.0) pit += dec.Evaluar(P(m, v), ReferenciaGuia.Ia, v, 0, t).Pitar ? 1 : 0;
            for (int k = 0; k < 20; k++, t += 1 / 27.0) dec.Evaluar(P(2000, v), ReferenciaGuia.Ia, v, 0, t);   // Lejos
        }
        Assert.Equal(3, pit);
    }

    // ---------------------------------------------------------------- latencia propia

    [Fact]
    public void LaCompensacionDeLatenciaAdelantaPitidoYSimboloPorIgualYEstaEnCeroPorDefecto()
    {
        Assert.Equal(0, DecisorAviso.CompensacionLatenciaPorDefectoS);
        Assert.Equal(0, new DecisorAviso().CompensacionLatenciaS);
        const double v = 216 / 3.6;
        double Pitido(double comp)
        {
            var dec = new DecisorAviso { AnticipacionS = 0.5, CompensacionLatenciaS = comp };
            var av = new AvisoFrenada(); double tp = double.NaN;
            DecisionAviso d = DecisionAviso.Nada;
            for (double t = 0; t < 12; t += Tick)
            {
                bool pita = false;
                if ((int)Math.Round(t / Tick) % 2 == 0)       // 30 Hz
                {
                    d = dec.Evaluar(P(Math.Max(0, 500 - v * t), v), ReferenciaGuia.Ia, v, 0, t); pita = d.Pitar;
                }
                var c = av.Actualizar(d, t);
                if (pita) { tp = t; Assert.Equal(1.0, c.Opacidad, 9); }
            }
            return tp;
        }
        double sin = Pitido(0), con = Pitido(0.1);
        Assert.InRange(sin - con, 0.1 - 2 * Tick, 0.1 + 2 * Tick);
        var d2 = new DecisorAviso { CompensacionLatenciaS = 9 }; Assert.Equal(0.25, d2.CompensacionLatenciaS);
        d2.CompensacionLatenciaS = double.NaN; Assert.Equal(0, d2.CompensacionLatenciaS);
        d2.CompensacionLatenciaS = -1; Assert.Equal(0, d2.CompensacionLatenciaS);
    }

    [Fact]
    public void LaVentanaNoSumaFundidoDeEntradaPropioNiTopeDeDt()
    {
        // la opacidad sube EXACTAMENTE al objetivo en el mismo cuadro, con cualquier salto de reloj (hueco, pausa)
        var a = new AvisoFrenada();
        a.Actualizar(DecisionAviso.Nada, 0);
        foreach (double dt in new[] { Tick, 0.045, 0.45, 10 })
        {
            a = new AvisoFrenada();
            a.Actualizar(DecisionAviso.Nada, 0);
            Assert.Equal(0.37, a.Actualizar(new DecisionAviso(0.37, false, double.PositiveInfinity), dt).Opacidad, 12);
            Assert.Equal(1.0, a.Actualizar(new DecisionAviso(1.0, true, double.PositiveInfinity), dt + Tick).Opacidad, 12);
        }
    }

    // ---------------------------------------------------------------- Probar aviso / Probar pitido

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void ProbarAvisoMuestraLaRampaCompletaYPitaAlTerminarlaConElMismoCuadroAl100(double a)
    {
        var s = new ServicioOverlay(new Almacen(_dir)) { Pitido = true, AvisoRampaS = 2.0, AvisoAnticipacionS = a, AvisoDuracionS = 1.5 };
        double t0 = 50; s.ProbarAviso(t0);
        var cuadros = new List<(double t, CuadroAviso c)>();
        for (double t = t0; t < t0 + 2 + a + 1.5 + 0.5; t += Tick) cuadros.Add((t, s.ActualizarAviso(t)));
        var pita = cuadros.Where(x => x.c.Pitar).ToList();
        Assert.Single(pita);
        Assert.Equal(1.0, pita[0].c.Opacidad, 9);
        Assert.InRange(pita[0].t - t0, 2.0 - 1e-9, 2.0 + Tick);
        Assert.All(cuadros.Where(x => x.t < pita[0].t), x => Assert.True(x.c.Opacidad < 1));
        // arranca en 0 y sube lineal durante la rampa completa
        Assert.True(cuadros[0].c.Opacidad < 0.02);
        var medio = cuadros.First(x => x.t - t0 >= 1.0);
        Assert.InRange(medio.c.Opacidad, 0.48, 0.55);
        // pleno hasta el "punto" (T_aviso + a) + duracion
        Assert.All(cuadros.Where(x => x.t >= pita[0].t && x.t < t0 + 2 + a + 1.5 - 2 * Tick), x => Assert.Equal(1.0, x.c.Opacidad, 9));
        Assert.Equal(0, cuadros[^1].c.Opacidad);
    }

    [Fact]
    public void ProbarAvisoRespetaElInterruptorDelPitidoYProbarPitidoSiempreSuena()
    {
        var s = new ServicioOverlay(new Almacen(_dir)) { Pitido = false, AvisoAnticipacionS = 1.0 };
        s.ProbarAviso(0);
        Assert.DoesNotContain(Enumerable.Range(0, 400).Select(i => s.ActualizarAviso(i * Tick)), c => c.Pitar);

        s.ProbarPitido(10);
        var cs = Enumerable.Range(0, 400).Select(i => (t: 10 + i * Tick, c: s.ActualizarAviso(10 + i * Tick))).ToList();
        var pit = Assert.Single(cs, x => x.c.Pitar);
        Assert.InRange(pit.t - 10, 2.0 - 1e-9, 2.0 + Tick);
        Assert.Equal(1.0, pit.c.Opacidad, 9);              // aviso activado por defecto: el simbolo acompana

        s.AvisoActivo = false;
        s.ProbarPitido(30);
        var sin = Enumerable.Range(0, 400).Select(i => s.ActualizarAviso(30 + i * Tick)).ToList();
        Assert.Single(sin, c => c.Pitar);
        Assert.All(sin, c => Assert.Equal(0, c.Opacidad));  // aviso apagado: solo se oye
    }

    // ---------------------------------------------------------------- servicio, config, menu

    [Fact]
    public void ElServicioExponeLaAnticipacionConClamp()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        Assert.Equal(0, s.AvisoAnticipacionS);
        s.AvisoAnticipacionS = 1.3; Assert.Equal(1.3, s.AvisoAnticipacionS);
        s.AvisoAnticipacionS = 9; Assert.Equal(2.0, s.AvisoAnticipacionS);
        s.AvisoAnticipacionS = -1; Assert.Equal(0, s.AvisoAnticipacionS);
        s.AvisoAnticipacionS = double.NaN; Assert.Equal(0, s.AvisoAnticipacionS);
        Assert.Equal(0, s.CompensacionLatenciaS);
    }

    [Fact]
    public void LaAnticipacionSePersisteEsTolerantePorConfigViejoYSeLimita()
    {
        var ruta = Path.Combine(_dir, "config.json");
        var a = new AlmacenConfig(ruta);
        a.Guardar(new ConfigApp { AvisoAnticipacionS = 1.4 });
        Assert.Contains("\"avisoAnticipacionS\": 1.4", File.ReadAllText(ruta));
        Assert.Equal(1.4, new AlmacenConfig(ruta).Cargar().AvisoAnticipacionS);
        Assert.Equal(0, new ConfigApp().AvisoAnticipacionS);

        File.WriteAllText(ruta, """{ "formato": 1, "modo": "mostrar", "avisoRampaS": 2.5 }""");        // config de antes: sin el campo
        var viejo = new AlmacenConfig(ruta).Cargar();
        Assert.Equal(0, viejo.AvisoAnticipacionS);
        Assert.Equal(2.5, viejo.AvisoRampaS);

        File.WriteAllText(ruta, """{ "formato": 1, "avisoAnticipacionS": 7.5 }""");
        Assert.Equal(2.0, new AlmacenConfig(ruta).Cargar().AvisoAnticipacionS);
        File.WriteAllText(ruta, """{ "formato": 1, "avisoAnticipacionS": -3 }""");
        Assert.Equal(0, new AlmacenConfig(ruta).Cargar().AvisoAnticipacionS);
        File.WriteAllText(ruta, """{ "formato": 1, "avisoAnticipacionS": "mucho" }""");
        Assert.Equal(0, new AlmacenConfig(ruta).Cargar().AvisoAnticipacionS);

        var c = new ConfigApp(); c.CopiarDe(new ConfigApp { AvisoAnticipacionS = 0.8 });
        Assert.Equal(0.8, c.AvisoAnticipacionS);
    }

    [Fact]
    public void LosPasosDelMenuDeLaBandejaSonLosAprobadosYEstanEnRango()
    {
        Assert.Equal([0, 0.2, 0.4, 0.6, 0.8, 1.0, 1.5, 2.0], DecisorAviso.AnticipacionesSugeridasS);
        Assert.All(DecisorAviso.AnticipacionesSugeridasS, p => Assert.Equal(p, DecisorAviso.LimitarAnticipacion(p)));
        Assert.Equal(0.1, DecisorAviso.AnticipacionPasoS);
    }

    // ---------------------------------------------------------------- servicio completo: simulador (27 y 39 Hz, con huecos)

    private static ResultadoLectura Ok(in SharedMemory sm) => new(EstadoLectura.Ok, ConstantesShm.Version, sm);

    private sealed record Tk(double T, bool Pitar, double Op);

    /// <summary>
    /// Corre la secuencia de lecturas por el servicio como la app: ventana a 60 Hz; si en el tick hay muestra, se llama a
    /// Paso y a ActualizarAviso con el mismo reloj (igual que Tick() del ControladorApp).
    /// </summary>
    private static List<Tk> Correr(ServicioOverlay s, IEnumerable<(ResultadoLectura r, double t)> lecturas)
    {
        var res = new List<Tk>();
        double tc = double.NaN;
        foreach (var (r, t) in lecturas)
        {
            if (double.IsNaN(tc)) tc = t;
            for (; tc < t - 1e-9; tc += Tick) res.Add(new Tk(tc, false, s.ActualizarAviso(tc).Opacidad));
            var v = s.Paso(r, t);
            res.Add(new Tk(t, v.Pitar, s.ActualizarAviso(t).Opacidad));
            tc = t + Tick;
        }
        return res;
    }

    /// <summary>Verifica las reglas de sincronia sobre los ticks; devuelve la cantidad de pitidos.</summary>
    private int Verificar(List<Tk> ticks, string etiqueta, out double peorOpacidad)
    {
        int pitidos = 0; peorOpacidad = 1;
        for (int i = 0; i < ticks.Count; i++)
        {
            if (ticks[i].Pitar)
            {
                pitidos++;
                peorOpacidad = Math.Min(peorOpacidad, ticks[i].Op);
                Assert.True(ticks[i].Op >= 0.98, $"{etiqueta}: opacidad {ticks[i].Op:0.000} en el pitido (t={ticks[i].T:0.000})");
                Assert.True(ticks[i].Op >= 1 - 1e-9, $"{etiqueta}: no llega a 1,0 en el pitido ({ticks[i].Op:0.000000})");
            }
        }
        // nunca antes: todo inicio de tramo al 100 % coincide con un pitido. (Si dos curvas van pegadas, el simbolo de la anterior
        // puede seguir al 100 % cuando pita la siguiente: no hay inicio de tramo nuevo y el simbolo sigue pleno, como debe.)
        for (int i = 1; i < ticks.Count; i++)
            if (ticks[i].Op >= 1 - 1e-9 && ticks[i - 1].Op < 1 - 1e-9)
                Assert.True(ticks[i].Pitar, $"{etiqueta}: el simbolo llega a 1,0 sin pitido (t={ticks[i].T:0.000})");
        return pitidos;
    }

    /// <summary>Las lecturas de la fase Mostrar, sin las muestras que caen en huecos periodicos de 0,45 s.</summary>
    private static IEnumerable<(ResultadoLectura r, double t)> ConHuecos(IEnumerable<(ResultadoLectura r, double t)> l, double periodo, double fase, double largo = 0.45)
    {
        double t0 = double.NaN;
        foreach (var (r, t) in l)
        {
            if (double.IsNaN(t0)) t0 = t;
            double x = (t - t0 + fase) % periodo;
            if (x < largo) continue;
            yield return (r, t);
        }
    }

    public static IEnumerable<object[]> Frecuencias() { yield return [27.0]; yield return [39.0]; }

    [Theory]
    [MemberData(nameof(Frecuencias))]
    public void ConElSimuladorElPitidoYElSimboloVanJuntosParaTodasLasAnticipaciones(double hz)
    {
        var sim = new Simulador(ias: 3, pasoS: 1 / hz);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        for (int i = 0; i < 400_000 && sim.Autos[0].Vueltas < 9; i++) s.Paso(Ok(sim.Paso()), sim.Tiempo);
        s.Modo = ModoApp.Mostrar;
        s.Pitido = true;

        foreach (double a in new[] { 0, 0.5, 1.0, 2.0 })
            foreach (bool huecos in new[] { false, true })
            {
                s.AvisoAnticipacionS = a;
                var lect = new List<(ResultadoLectura, double)>();
                double off = 1000 + (a * 100 + (huecos ? 50 : 0)) * 100;
                uint objetivo = sim.Autos[0].Vueltas + 3;
                double t0 = sim.Tiempo;
                for (int i = 0; i < 400_000 && sim.Autos[0].Vueltas < objetivo; i++)
                {
                    var sm = sim.Paso();
                    sm.mBrake = 0;       // el conductor no frena: se mide solo el aviso
                    lect.Add((Ok(sm), off + sim.Tiempo - t0));
                }
                var fuente = huecos ? ConHuecos(lect, 4.0, 0.3) : lect;
                var ticks = Correr(s, fuente);
                int pit = Verificar(ticks, $"sim {hz} Hz A={a} huecos={huecos}", out double peor);
                _out.WriteLine($"SIM {hz:0} Hz A={a:0.0} huecos={huecos}: {pit} pitidos, peor opacidad en el pitido {peor:0.0000}");
                Assert.True(pit >= 6, $"{pit} pitidos");
            }
    }

    // ---------------------------------------------------------------- servicio completo: capturas reales

    private static IEnumerable<(ResultadoLectura r, double t)> Lecturas(string ruta)
    {
        using var rep = ReproductorCaptura.Abrir(ruta);
        while (true)
        {
            var r = rep.Leer();
            if (r.Estado == EstadoLectura.FinCaptura) yield break;
            yield return (r, rep.SegundosActual);
        }
    }

    [Fact]
    public void SobreLaCapturaRealElPitidoYElSimboloVanJuntosConTodasLasAnticipacionesYLosHuecos()
    {
        var ruta = Environment.GetEnvironmentVariable("AMS2_CAPTURA_PRUEBA");
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return;

        var lecturas = Lecturas(ruta).ToList();
        double dur = lecturas[^1].t - lecturas[0].t;
        _out.WriteLine($"CAPTURA {Path.GetFileName(ruta)}: {lecturas.Count} lecturas, {dur:0.0} s, {lecturas.Count / dur:0.0} Hz de lecturas");
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        foreach (var (r, t) in lecturas) s.Paso(r, t);
        s.Modo = ModoApp.Mostrar;
        s.Pitido = true;

        int total = 0, pasadas = 0;
        foreach (var referencia in new[] { ReferenciaGuia.Ia, ReferenciaGuia.Vos })
            foreach (double a in new[] { 0, 0.5, 1.0, 2.0 })
                foreach (double? hueco in new double?[] { null, 0.0, 1.1, 2.3 })
                {
                    s.ReferenciaPrincipal = referencia;
                    s.AvisoAnticipacionS = a;
                    double off = 100000 * (1 + total);
                    var fuente = Lecturas(ruta).Select(x => (x.r, x.t + off));
                    if (hueco is double fase) fuente = ConHuecos(fuente, 3.0, fase);
                    var ticks = Correr(s, fuente);
                    string et = $"{Path.GetFileName(ruta)} {referencia} A={a} hueco={hueco}";
                    int pit = Verificar(ticks, et, out double peor);
                    total++; pasadas += pit;
                    _out.WriteLine($"REAL {et}: {pit} pitidos, peor opacidad en el pitido {(pit == 0 ? double.NaN : peor):0.0000}, maxima {ticks.Max(x => x.Op):0.000}");
                }
        _out.WriteLine($"REAL {Path.GetFileName(ruta)}: {total} corridas, {pasadas} pitidos en total");
        Assert.True(pasadas > 0, "ninguna corrida sobre la captura real llego a pitar");
    }
}
