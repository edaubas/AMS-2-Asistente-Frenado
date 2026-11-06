using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using Xunit;

namespace AMS2Frenado.Tests;

public sealed class GuiaTests
{
    private const double L = 5000;
    private const double Dt = 1 / 27.0;

    private static BloqueCurva Bl(double frenar, double vertice, int vmin = 80, int vent = 200)
        => new() { FrenarEn = frenar, Vertice = vertice, VVerticeKmh = vmin, VEntradaKmh = vent, LargoFrenada = vertice - frenar, Vueltas = 8 };

    private static CurvaGuardada Curva(string nombre, double frenar, double vertice, string tipo = "frenada",
        bool ia = true, bool vos = true, double vosFrenar = double.NaN, int vminIa = 80, int vminVos = 84)
        => new()
        {
            Nombre = nombre, Vertice = vertice, Tipo = tipo,
            Ia = ia ? Bl(frenar, vertice, vminIa) : null,
            Vos = vos ? Bl(double.IsNaN(vosFrenar) ? frenar : vosFrenar, vertice, vminVos) : null,
        };

    private static DatosClase Datos(params CurvaGuardada[] c)
        => new() { Pista = "P", Variante = "V", Clase = "C", Largo = L, Curvas = [.. c] };

    private static MotorGuia Motor(DatosClase? datos, MapaPista? mapa = null, OpcionesGuia? op = null,
        ReferenciaGuia r = ReferenciaGuia.Ia)
    {
        var m = new MotorGuia(op) { ReferenciaPrincipal = r };
        m.CargarDatos(datos, mapa);
        return m;
    }

    /// <summary>Recorre de d0 a d1 (sin envolver) a velocidad constante. 'ajuste' puede mover la distancia (ruido) o saltear muestras.</summary>
    private static List<(double t, double d, PanelGuia p)> Correr(MotorGuia m, double d0, double d1, double kmh,
        double t0 = 0, Func<int, double, double?>? ruido = null, Func<int, bool>? saltear = null)
    {
        var res = new List<(double, double, PanelGuia)>();
        double v = kmh / 3.6;
        int n = 0;
        for (double d = d0; d <= d1; d += v * Dt, n++)
        {
            if (saltear?.Invoke(n) == true) continue;
            double dd = d + (ruido?.Invoke(n, d) ?? 0);
            double t = t0 + n * Dt;
            res.Add((t, d, m.Actualizar(new EntradaGuia(t, Math.IEEERemainder(dd, L) < 0 ? dd % L + L : dd % L, kmh, L))));
        }
        return res;
    }

    private static List<EstadoPanel> Secuencia(IEnumerable<PanelGuia> ps)
    {
        var s = new List<EstadoPanel>();
        foreach (var p in ps) if (s.Count == 0 || s[^1] != p.Estado) s.Add(p.Estado);
        return s;
    }

    // ------------------------------------------------------------ ventana por tiempo

    [Theory]
    [InlineData(216, 300)]
    [InlineData(150, 208.333)]
    [InlineData(300, 400)]       // 417 m -> tope
    [InlineData(40, 120)]        // 56 m -> piso
    [InlineData(0, 120)]
    public void LaVentanaEsCincoSegundosConPisoYTope(double kmh, double esperado)
        => Assert.Equal(esperado, MotorGuia.Ventana(kmh, new OpcionesGuia()), 2);

    [Fact]
    public void LaVentanaSeConfigura()
    {
        var op = new OpcionesGuia { SegundosAviso = 8, VentanaMinM = 200, VentanaMaxM = 500 };
        Assert.Equal(480, MotorGuia.Ventana(216, op), 6);
        Assert.Equal(200, MotorGuia.Ventana(10, op));
        Assert.Equal(500, MotorGuia.Ventana(400, op));
        Assert.Equal(300, MotorGuia.Ventana(100, new OpcionesGuia { UsarVentanaPorTiempo = false }));
    }

    [Theory]
    [InlineData(216, 700)]       // frena en 1000: el panel aparece a 300 m
    [InlineData(150, 792)]       // 208 m
    [InlineData(300, 600)]       // tope 400 m
    public void ElPanelApareceCuandoFaltaLaVentana(double kmh, double dAparece)
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        var r = Correr(m, 300, 1100, kmh);
        var primero = r.First(x => x.p.Estado != EstadoPanel.Lejos);
        Assert.Equal(EstadoPanel.Aproximacion, primero.p.Estado);
        Assert.InRange(primero.d, dAparece - 3.5, dAparece + 3.5);       // un paso de 27 Hz de tolerancia
        Assert.All(r.TakeWhile(x => x.p.Estado == EstadoPanel.Lejos), x => Assert.False(x.p.Visible));
    }

    [Fact]
    public void ElPisoDeLaVentanaMantieneElAvisoCuandoVaLento()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        var r = Correr(m, 700, 1100, 40);                          // 40 km/h: 56 m por tiempo, piso 120 m
        var primero = r.First(x => x.p.Estado != EstadoPanel.Lejos);
        Assert.InRange(primero.d, 880 - 1, 880 + 1);
    }

    // ------------------------------------------------------------ estados y transiciones

    [Fact]
    public void RecorreLosEstadosEnOrdenYSeOcultaPasadoElVertice()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200), Curva("Curva 2", 2500, 2700)));
        var r = Correr(m, 400, 1600, 216);

        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Aproximacion, EstadoPanel.Inminente, EstadoPanel.Frena, EstadoPanel.Lejos],
            Secuencia(r.Select(x => x.p)));

        double Inicio(EstadoPanel e) => r.First(x => x.p.Estado == e).d;
        Assert.InRange(Inicio(EstadoPanel.Aproximacion), 700 - 3.5, 700 + 3.5);       // 300 m antes
        Assert.InRange(Inicio(EstadoPanel.Inminente), 900 - 3.5, 900 + 3.5);          // menos de 100 m
        Assert.InRange(Inicio(EstadoPanel.Frena), 1000 - 3.5, 1000 + 3.5);            // llego al punto

        var frena = r.Where(x => x.p.Estado == EstadoPanel.Frena).ToList();
        Assert.All(frena, x => Assert.Equal(0, x.p.MetrosHastaFrenar));
        Assert.All(frena, x => Assert.Equal("FRENÁ", x.p.TextoEstado));
        Assert.True(frena[^1].d <= 1200 + 5 + 3.5);                                   // se oculta pasado el vertice

        var despues = r.Last().p;
        Assert.Equal(EstadoPanel.Lejos, despues.Estado);
        Assert.False(despues.Visible);
        Assert.Equal(1, despues.IndiceCurva);                                         // ya apunta a la Curva 2
        Assert.StartsWith("Próxima CURVA 2 · ", despues.TextoMetros);
    }

    [Fact]
    public void LejosMuestraLaLineaDeLaProximaCurva()
    {
        var m = Motor(Datos(Curva("Curva 7", 1000, 1200)));
        var p = m.Actualizar(new EntradaGuia(0, 100, 216, L));
        Assert.Equal(EstadoPanel.Lejos, p.Estado);
        Assert.False(p.Visible);
        Assert.Equal("Próxima CURVA 7 · 900 m", p.TextoMetros);       // 1000 - 100, redondeado a 10 m
        Assert.Null(p.TextoPrincipal);
    }

    [Fact]
    public void ElNumeroGrandeEsElCartelYLosMetrosQuedanEnChico()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1208)));          // 208 m -> cartel 200
        var p = m.Actualizar(new EntradaGuia(0, 800, 216, L));       // faltan 200 m
        Assert.Equal(EstadoPanel.Aproximacion, p.Estado);
        Assert.Equal(200, p.CartelPrincipal);
        Assert.Equal("200 m", p.TextoPrincipal);
        Assert.Equal(200, p.MetrosHastaFrenar!.Value, 6);
        Assert.Equal("200 m", p.TextoMetros);
        Assert.Equal(408, p.MetrosHastaVertice!.Value, 6);
    }

    [Fact]
    public void LosUmbralesSeConfiguran()
    {
        var op = new OpcionesGuia { UmbralInminenteM = 150, UsarVentanaPorTiempo = false, UmbralLejosM = 250 };
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)), op: op);
        Assert.Equal(EstadoPanel.Lejos, m.Actualizar(new EntradaGuia(0, 740, 216, L)).Estado);
        Assert.Equal(EstadoPanel.Aproximacion, m.Actualizar(new EntradaGuia(Dt, 760, 216, L)).Estado);
        Assert.Equal(EstadoPanel.Inminente, m.Actualizar(new EntradaGuia(2 * Dt, 860, 216, L)).Estado);
    }

    [Fact]
    public void ArrancandoDentroDeLaZonaDeFrenadaEsFrena()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        var p = m.Actualizar(new EntradaGuia(0, 1100, 180, L));
        Assert.Equal(EstadoPanel.Frena, p.Estado);
        Assert.Equal(0, p.MetrosHastaFrenar);
        Assert.Equal(100, p.MetrosHastaVertice!.Value, 6);
    }

    // ------------------------------------------------------------ histeresis

    [Fact]
    public void ElRuidoDeMetrosNoHaceParpadearElPanel()
    {
        var rnd = new Random(7);
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        // +-6 m de ruido en la distancia, justo sobre los limites de 300, 100 y 0 m
        var r = Correr(m, 500, 1250, 216, ruido: (_, _) => rnd.NextDouble() * 12 - 6);
        var sec = Secuencia(r.Select(x => x.p));
        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Aproximacion, EstadoPanel.Inminente, EstadoPanel.Frena, EstadoPanel.Lejos], sec);
    }

    [Fact]
    public void LosMetrosMostradosNoSubenPorRuido()
    {
        var rnd = new Random(3);
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        var r = Correr(m, 600, 999, 150, ruido: (_, _) => rnd.NextDouble() * 8 - 4);
        var metros = r.Where(x => x.p.Visible).Select(x => x.p.MetrosHastaFrenar!.Value).ToList();
        Assert.NotEmpty(metros);
        for (int i = 1; i < metros.Count; i++) Assert.True(metros[i] <= metros[i - 1] + 1e-9);
    }

    [Fact]
    public void UnHuecoGrandeDeDatosNoHaceVolverAtrasNiParpadear()
    {
        // 27 Hz con huecos de ~0,45 s (12 muestras) justo en los cambios de estado
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        var r = Correr(m, 400, 1300, 216, saltear: n => (n >= 55 && n < 67) || (n >= 100 && n < 112) || (n >= 130 && n < 142));
        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Aproximacion, EstadoPanel.Inminente, EstadoPanel.Frena, EstadoPanel.Lejos],
            Secuencia(r.Select(x => x.p)));
        // el hueco de datos no deja tiempos "congelados": la muestra posterior ya trae su estado correcto
        var tras = r.First(x => x.t > 55 * Dt + 0.45 - 1e-9);
        Assert.NotEqual(EstadoPanel.SinDatos, tras.p.Estado);
    }

    [Fact]
    public void UnHuecoPuedeSaltearseUnEstadoSinRetroceder()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        m.Actualizar(new EntradaGuia(0, 850, 216, L));                                  // Aproximacion
        var p = m.Actualizar(new EntradaGuia(0.45, 1010, 216, L));                      // salta Inminente
        Assert.Equal(EstadoPanel.Frena, p.Estado);
        // una muestra rara que cae 40 m atras no baja el estado
        p = m.Actualizar(new EntradaGuia(0.45 + Dt, 970, 216, L));
        Assert.Equal(EstadoPanel.Frena, p.Estado);
        Assert.Equal(0, p.MetrosHastaFrenar);
    }

    [Fact]
    public void SiLaPosicionRetrocedeDeVerdadSeVuelveAtrasPeroConTiempoMinimo()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        m.Actualizar(new EntradaGuia(0, 950, 216, L));                                  // Inminente
        // vuelve a boxes / reaparece 500 m atras
        var a = m.Actualizar(new EntradaGuia(0.1, 450, 50, L));
        Assert.Equal(EstadoPanel.Inminente, a.Estado);                                  // todavia no: poco tiempo
        var b = m.Actualizar(new EntradaGuia(0.7, 450, 50, L));
        Assert.Equal(EstadoPanel.Inminente, b.Estado);
        var c = m.Actualizar(new EntradaGuia(1.2, 450, 50, L));
        Assert.Equal(EstadoPanel.Lejos, c.Estado);
        Assert.Equal(550, c.MetrosHastaFrenar!.Value, 6);                               // y los metros se reinician
    }

    [Fact]
    public void FrenandoLaVentanaNoSeAchicaYElPanelSeQueda()
    {
        // entra a 216 km/h (ventana 300) y baja a 60 km/h (ventana por tiempo 120 m, menor que lo que falta)
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        Assert.Equal(EstadoPanel.Aproximacion, m.Actualizar(new EntradaGuia(0, 705, 216, L)).Estado);
        for (int i = 1; i <= 60; i++)
            Assert.Equal(EstadoPanel.Aproximacion, m.Actualizar(new EntradaGuia(i * Dt, 705 + i * 0.6, 60, L)).Estado);
    }

    // ------------------------------------------------------------ linea de meta

    [Fact]
    public void CurvaCercaDeLaLineaDeMetaConFrenadaAntesDeLaMeta()
    {
        // frena en 4900, vertice en 100 (pasada la meta): 200 m de frenada
        var m = Motor(Datos(Curva("Curva 1", 4900, 100), Curva("Curva 2", 1500, 1700)));
        var r = new List<PanelGuia>();
        double v = 216 / 3.6;
        double t = 0;
        for (double d = 4400; d < 5000 + 400; d += v * Dt, t += Dt)
            r.Add(m.Actualizar(new EntradaGuia(t, d % L, 216, L)));
        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Aproximacion, EstadoPanel.Inminente, EstadoPanel.Frena, EstadoPanel.Lejos],
            Secuencia(r));
        Assert.Equal(0, r.First(p => p.Estado == EstadoPanel.Frena).IndiceCurva);
        Assert.Equal(200, r.First(p => p.Estado == EstadoPanel.Frena).CartelPrincipal);
        Assert.Equal(1, r[^1].IndiceCurva);
        // en Frena cruzando la meta la distancia de vuelta salta de ~5000 a 0 sin cambiar de estado
        Assert.All(r.Where(p => p.Estado == EstadoPanel.Frena), p => Assert.Equal(0, p.IndiceCurva));
    }

    [Fact]
    public void CurvaConElPuntoDeFrenadoDespuesDeLaMetaYElVerticeCercaDelCero()
    {
        var m = Motor(Datos(Curva("Curva 1", 20, 200), Curva("Curva 2", 2500, 2700)));
        double v = 216 / 3.6;
        var r = new List<PanelGuia>();
        double t = 0;
        for (double d = 4700; d < 5000 + 300; d += v * Dt, t += Dt)      // la pista se envuelve a 5000
            r.Add(m.Actualizar(new EntradaGuia(t, d % L, 216, L)));
        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Aproximacion, EstadoPanel.Inminente, EstadoPanel.Frena, EstadoPanel.Lejos],
            Secuencia(r));
        // el aviso aparece antes de la meta (20 m - 300 m = 4720) y la frenada empieza ya pasada la meta
        Assert.Contains(r, p => p.Estado == EstadoPanel.Frena && p.IndiceCurva == 0);
    }

    [Fact]
    public void UnaSolaCurvaDaLaVueltaSinRepetirElAvisoFueraDeLugar()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        double v = 216 / 3.6, t = 0;
        var r = new List<PanelGuia>();
        for (double d = 400; d < 400 + 2 * L; d += v * Dt, t += Dt) r.Add(m.Actualizar(new EntradaGuia(t, d % L, 216, L)));
        var sec = Secuencia(r);
        // dos vueltas: dos pasadas completas, sin parpadeos intermedios
        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Aproximacion, EstadoPanel.Inminente, EstadoPanel.Frena,
                      EstadoPanel.Lejos, EstadoPanel.Aproximacion, EstadoPanel.Inminente, EstadoPanel.Frena, EstadoPanel.Lejos], sec);
    }

    // ------------------------------------------------------------ toque y levanta

    [Fact]
    public void ToqueMuestraLaVelocidadMinimaYLosMetrosEnChico()
    {
        var m = Motor(Datos(Curva("Curva 4", 1000, 1100, "toque", vminIa: 132, vminVos: 128)));
        var r = Correr(m, 500, 1200, 216);
        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Toque, EstadoPanel.Lejos], Secuencia(r.Select(x => x.p)));
        var p = r.First(x => x.p.Estado == EstadoPanel.Toque).p;
        Assert.Equal("TOQUE", p.TextoEstado);
        Assert.Equal("132 km/h", p.TextoPrincipal);
        Assert.Equal("128 km/h", p.TextoSecundario);
        Assert.Equal(132, p.VelMinPrincipalKmh);
        Assert.NotNull(p.TextoMetros);
        Assert.EndsWith(" m", p.TextoMetros);
        Assert.Null(p.CartelPrincipal);
        Assert.True(p.Visible);
    }

    [Fact]
    public void LevantaMuestraLaVelocidadMinimaYNoTieneReglaDeMetros()
    {
        var m = Motor(Datos(Curva("Curva 9", 1000, 1080, "levanta", vminIa: 205, vminVos: 205)));
        var r = Correr(m, 500, 1200, 216);
        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Levanta, EstadoPanel.Lejos], Secuencia(r.Select(x => x.p)));
        var p = r.First(x => x.p.Estado == EstadoPanel.Levanta).p;
        Assert.Equal("LEVANTÁ", p.TextoEstado);
        Assert.Equal("205 km/h", p.TextoPrincipal);
        Assert.Null(p.TextoSecundario);                  // igual velocidad: una sola
        Assert.Null(p.TextoMetros);
    }

    [Fact]
    public void ToqueYLevantaNuncaPasanPorInminenteNiFrena()
    {
        var m = Motor(Datos(Curva("A", 1000, 1100, "toque"), Curva("B", 2000, 2050, "levanta")));
        var r = Correr(m, 500, 2200, 216);
        Assert.DoesNotContain(r, x => x.p.Estado is EstadoPanel.Inminente or EstadoPanel.Frena or EstadoPanel.Aproximacion);
        Assert.Equal([EstadoPanel.Lejos, EstadoPanel.Toque, EstadoPanel.Lejos, EstadoPanel.Levanta, EstadoPanel.Lejos],
            Secuencia(r.Select(x => x.p)));
    }

    // ------------------------------------------------------------ sin datos

    [Fact]
    public void SinGrabacionesEsSinDatos()
    {
        var m = Motor(null);
        var p = m.Actualizar(new EntradaGuia(0, 100, 100, L));
        Assert.Equal(EstadoPanel.SinDatos, p.Estado);
        Assert.True(p.Visible);
        Assert.Equal("SIN DATOS", p.TextoEstado);
        Assert.Equal(EstadoPanel.SinDatos, Motor(Datos()).Actualizar(new EntradaGuia(0, 100, 100, L)).Estado);
    }

    [Fact]
    public void ElLargoDePistaDistintoEsSinDatosYAlCargarDatosVuelve()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        Assert.Equal(EstadoPanel.SinDatos, m.Actualizar(new EntradaGuia(0, 100, 100, 3000)).Estado);
        m.CargarDatos(Datos(Curva("Curva 1", 1000, 1200)));
        Assert.Equal(EstadoPanel.Lejos, m.Actualizar(new EntradaGuia(1, 100, 100, L)).Estado);
        m.CargarDatos(null);
        Assert.Equal(EstadoPanel.SinDatos, m.Actualizar(new EntradaGuia(2, 100, 100, L)).Estado);
    }

    [Fact]
    public void UnaLecturaInvalidaMantieneElUltimoPanel()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        var a = m.Actualizar(new EntradaGuia(0, 850, 216, L));
        Assert.Same(a, m.Actualizar(new EntradaGuia(Dt, 0, 0, L, Valida: false)));
        Assert.Same(a, m.Actualizar(new EntradaGuia(Dt, double.NaN, 0, L)));
    }

    [Fact]
    public void CurvasConDatosSinSentidoSeIgnoran()
    {
        var mala = Curva("Mala", 1200, 1000);        // frenar_en despues del vertice, a ~4800 m
        var m = Motor(Datos(mala));
        Assert.Equal(EstadoPanel.SinDatos, m.Actualizar(new EntradaGuia(0, 100, 100, L)).Estado);
    }

    [Fact]
    public void ElFlagGrabandoSePasaSinCambiarElEstado()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200)));
        Assert.False(m.Actualizar(new EntradaGuia(0, 850, 216, L)).Grabando);
        m.Grabando = true;
        var p = m.Actualizar(new EntradaGuia(Dt, 851, 216, L));
        Assert.True(p.Grabando);
        Assert.Equal(EstadoPanel.Aproximacion, p.Estado);
        Assert.True(m.Ultimo.Grabando);
    }

    // ------------------------------------------------------------ referencias y carteles en el panel

    [Fact]
    public void LaReferenciaPrincipalDecideElCartelGrande()
    {
        // IA: 208 m -> 200; vos: 131 m -> 100
        var c = Curva("Curva 1", 1000, 1208, vosFrenar: 1077);
        var m = Motor(Datos(c), r: ReferenciaGuia.Ia);
        var p = m.Actualizar(new EntradaGuia(0, 850, 216, L));
        Assert.Equal(ReferenciaGuia.Ia, p.Principal);
        Assert.Equal(ReferenciaGuia.Vos, p.Secundaria);
        Assert.Equal(200, p.CartelPrincipal);
        Assert.Equal(100, p.CartelSecundario);
        Assert.Equal((200, 100), (p.CartelIa, p.CartelVos));
        Assert.Equal("200 m", p.TextoPrincipal);
        Assert.Equal("100 m", p.TextoSecundario);

        m.ReferenciaPrincipal = ReferenciaGuia.Vos;
        p = m.Actualizar(new EntradaGuia(Dt, 850, 216, L));
        Assert.Equal(ReferenciaGuia.Vos, p.Principal);
        Assert.Equal(100, p.CartelPrincipal);
        Assert.Equal(200, p.CartelSecundario);
        // el punto de frenado de la referencia principal manda: la de vos es 77 m mas tarde
        Assert.Equal(1077 - 850, p.MetrosHastaFrenar!.Value, 6);
    }

    [Fact]
    public void SiIaYVosCaenEnElMismoCartelSeMuestraUno()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1199, vosFrenar: 1005)));      // 199 y 194 -> 200
        var p = m.Actualizar(new EntradaGuia(0, 850, 216, L));
        Assert.Equal(200, p.CartelPrincipal);
        Assert.Null(p.CartelSecundario);
        Assert.Null(p.TextoSecundario);
        Assert.Equal(200, p.CartelVos);
    }

    [Fact]
    public void SiFaltaLaReferenciaPrincipalSeUsaLaOtra()
    {
        var m = Motor(Datos(Curva("Curva 1", 1000, 1200, ia: false)), r: ReferenciaGuia.Ia);
        var p = m.Actualizar(new EntradaGuia(0, 850, 216, L));
        Assert.Equal(ReferenciaGuia.Vos, p.Principal);
        Assert.Null(p.Secundaria);
        Assert.Equal(200, p.CartelPrincipal);
        Assert.Null(p.CartelIa);
        Assert.Null(p.CartelSecundario);
    }

    [Fact]
    public void LosCartelesDeLaPistaYElDesfaseAfectanElPanel()
    {
        var mapa = new MapaPista { Largo = L, Carteles = [200, 100, 50] };
        var d = Datos(Curva("Curva 1", 1000, 1302));            // 302 m
        Assert.Equal(300, Motor(d).Actualizar(new EntradaGuia(0, 750, 216, L)).CartelPrincipal);
        Assert.Equal(200, Motor(d, mapa).Actualizar(new EntradaGuia(0, 750, 216, L)).CartelPrincipal);      // sin cartel de 300
        var conDesfase = new OpcionesGuia { DesfaseCartelM = 40 };                                            // 262 -> 300 (mas cerca que 200)
        Assert.Equal(300, Motor(d, op: conDesfase).Actualizar(new EntradaGuia(0, 750, 216, L)).CartelPrincipal);
        Assert.Equal(200, Motor(d, op: new OpcionesGuia { DesfaseCartelM = 102 }).Actualizar(new EntradaGuia(0, 750, 216, L)).CartelPrincipal);
    }
}
