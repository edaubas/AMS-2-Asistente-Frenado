using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

public sealed class OverlayServicioTests : IDisposable
{
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static ResultadoLectura Ok(in SharedMemory sm) => new(EstadoLectura.Ok, ConstantesShm.Version, sm);

    private static List<VistaOverlay> Correr(ServicioOverlay s, Simulador sim, int vueltas, double tIni = 0)
    {
        var res = new List<VistaOverlay>();
        uint objetivo = sim.Autos[0].Vueltas + (uint)vueltas;
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < objetivo; i++)
        {
            var sm = sim.Paso();
            res.Add(s.Paso(Ok(sm), tIni + sim.Tiempo));
        }
        return res;
    }

    private ServicioOverlay Grabado(out Simulador sim)
    {
        sim = new Simulador(ias: 3);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        Correr(s, sim, 9);
        s.Modo = ModoApp.Mostrar;       // al dejar Grabar guarda a disco
        return s;
    }

    [Fact]
    public void SinJuegoYVersionDistintaAvisanEnElPanel()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        var v = s.Paso(ResultadoLectura.SinJuego, 0);
        Assert.Equal("ESPERANDO AMS2", v.Panel.Titulo);
        v = s.Paso(new ResultadoLectura(EstadoLectura.VersionDistinta, 13, default), 1);
        Assert.Equal("VERSIÓN NO COMPATIBLE", v.Panel.Titulo);
        Assert.Contains("13", v.Panel.Linea1);
        // una lectura "Ok" con mVersion distinta tampoco se usa
        var sm = new Simulador().Armar(); sm.mVersion = 13;
        Assert.Equal("VERSIÓN NO COMPATIBLE", s.Paso(Ok(sm), 2).Panel.Titulo);
    }

    [Fact]
    public void LecturaInestableMantieneLoUltimo()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        var a = s.Paso(ResultadoLectura.SinJuego, 0);
        var b = s.Paso(new ResultadoLectura(EstadoLectura.Inestable, 14, default), 0.1);
        Assert.Equal(a.Panel.Titulo, b.Panel.Titulo);
    }

    [Fact]
    public void FueraDePistaPideEntrarYLuegoConservaLaUltimaVista()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        var sim = new Simulador();
        var menu = sim.Armar(); menu.mGameState = 1;
        Assert.Equal("SIN DATOS", s.Paso(Ok(menu), 0).Panel.Titulo);
        Assert.Contains("sesión en pista", s.Ultima.Panel.Linea1);
    }

    [Fact]
    public void MostrarSinArchivosDiceSinDatosConClaseYPista()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        var sim = new Simulador();
        var v = s.Paso(Ok(sim.Paso()), 0.1);
        Assert.Equal(EstadoPanel.SinDatos, v.Guia!.Estado);
        Assert.Equal("SIN DATOS", v.Panel.Titulo);
        Assert.Contains("GT3", v.Panel.Linea1);
        Assert.Contains("Demo Circuit · GP", v.Panel.Linea1);
        Assert.Equal("Demo Circuit · GP", v.TituloPista);
        Assert.Equal("GT3", v.ChipClase);
        Assert.False(v.Grabando);
    }

    [Fact]
    public void GrabarAlimentaAlGrabadorYMuestraContadoresMientrasNoHayGuia()
    {
        var sim = new Simulador(ias: 3);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        var vistas = Correr(s, sim, 4);
        Assert.All(vistas, v => Assert.True(v.Grabando));
        var ult = vistas.Last(v => v.Panel.Tipo == TipoTarjeta.Grabando);
        Assert.Equal("GT3", ult.Panel.Contadores!.Encabezado);
        Assert.Contains("3 autos", ult.Panel.Contadores.IaDetalle);
        Assert.Contains("Mostrar", ult.Panel.Contadores.Pie);
        Assert.True(int.Parse(ult.Panel.Contadores.Ia) >= 1);
        Assert.True(vistas.All(v => v.Guia!.Grabando));

        // al dejar Grabar se guardan clase y mapa
        s.Modo = ModoApp.Mostrar;
        Assert.NotNull(new Almacen(_dir).LeerClase("Demo Circuit", "GP", "GT3"));
        Assert.NotNull(new Almacen(_dir).LeerMapa("Demo Circuit", "GP"));
    }

    [Fact]
    public void MostrarCargaLoGrabadoYRecorreLosEstadosSinParpadear()
    {
        var s = Grabado(out var sim);
        var vistas = Correr(s, sim, 3, 1000);
        Assert.All(vistas, v => Assert.False(v.Grabando));
        Assert.Contains(vistas, v => v.Panel.Estado == EstadoPanel.Aproximacion);
        Assert.Contains(vistas, v => v.Panel.Estado == EstadoPanel.Inminente);
        var frena = vistas.Where(v => v.Panel.Estado == EstadoPanel.Frena).ToList();
        Assert.NotEmpty(frena);
        Assert.All(frena, v => { Assert.Equal("FRENÁ", v.Panel.TextoFranja); Assert.Equal("#C62F26", v.Panel.ColorFranja); Assert.Null(v.Panel.NumeroPrincipal); Assert.NotNull(v.Panel.VelocidadesTxt); });
        Assert.Contains(vistas, v => v.Panel.Estado == EstadoPanel.Toque && v.Panel.ColorFranja == "#3B4652");
        Assert.Contains(vistas, v => v.Panel.Estado == EstadoPanel.Levanta);

        // el mapa.json se sigue guardando aunque ya no se dibuje
        Assert.NotNull(new Almacen(_dir).LeerMapa("Demo Circuit", "GP"));

        // las cifras que se ven cambian como maximo a 10 Hz mientras la tarjeta es la misma
        var su = new Suavizador();
        double? tUlt = null; string? ultMetros = null; string? ultClave = null;
        for (int k = 0; k < vistas.Count; k++)
        {
            double t = 1000 + k / 30.0;
            var c = su.Actualizar(vistas[k].Panel, t);
            Assert.InRange(c.OpacidadTarjeta, 0, 1);
            string clave = $"{c.Panel.Estado}{c.Panel.IndiceCurva}";
            if (clave == ultClave && c.Panel.Tipo == TipoTarjeta.Guia && c.Panel.Metros != ultMetros && tUlt is double tu)
                Assert.True(t - tu >= TiemposUi.PeriodoCifrasS - 1e-9, $"cifra cambio en {t - tu:0.000} s");
            if (clave != ultClave || c.Panel.Metros != ultMetros) tUlt = t;
            ultClave = clave; ultMetros = c.Panel.Metros;
        }
    }

    /// <summary>Como <see cref="Correr"/>, pero el conductor nunca toca el freno (o usa el pedal natural del simulador).</summary>
    private static List<VistaOverlay> CorrerSinFrenar(ServicioOverlay s, Simulador sim, int vueltas, double tIni, bool pedalNatural = false)
    {
        var res = new List<VistaOverlay>();
        uint objetivo = sim.Autos[0].Vueltas + (uint)vueltas;
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < objetivo; i++)
        {
            var sm = sim.Paso();
            if (!pedalNatural) sm.mBrake = 0;
            res.Add(s.Paso(Ok(sm), tIni + sim.Tiempo));
        }
        return res;
    }

    [Fact]
    public void ElPitidoSuenaUnaVezPorFrenadaYSoloSiEstaActivado()
    {
        var s = Grabado(out var sim);
        var sin = CorrerSinFrenar(s, sim, 2, 1000);
        Assert.DoesNotContain(sin, v => v.Pitar);
        s.Pitido = true;
        var con = CorrerSinFrenar(s, sim, 2, 2000);          // el conductor no frena: pita en cada punto de frenado
        int pitidos = con.Count(v => v.Pitar);
        int entradas = 0;
        for (int i = 1; i < con.Count; i++)
            if (con[i].Panel.Estado == EstadoPanel.Frena && con[i - 1].Panel.Estado != EstadoPanel.Frena) entradas++;
        Assert.Equal(entradas, pitidos);
        Assert.True(pitidos >= 3);
    }

    [Fact]
    public void ElPitidoNoSuenaSiYaVasFrenandoAlLlegarAlPunto()
    {
        var s = Grabado(out var sim);
        s.Pitido = true;
        // pedal natural del simulador: ya esta apretado en (o antes de) el punto de frenado de la guia
        var con = CorrerSinFrenar(s, sim, 3, 2000, pedalNatural: true);
        int entradas = 0;
        for (int i = 1; i < con.Count; i++)
            if (con[i].Panel.Estado == EstadoPanel.Frena && con[i - 1].Panel.Estado != EstadoPanel.Frena) entradas++;
        Assert.True(entradas >= 3);
        Assert.All(con.Where(v => v.Pitar), v => Assert.True(v.Freno <= 0.15));
        Assert.True(con.Count(v => v.Pitar) < entradas, "al menos algun punto se tomo ya frenando");
    }

    [Fact]
    public void ReferenciaPrincipalCambiaLaEtiquetaPrincipal()
    {
        var s = Grabado(out var sim);
        s.ReferenciaPrincipal = ReferenciaGuia.Vos;
        var v = Correr(s, sim, 2, 1000).First(x => x.Panel.Tipo == TipoTarjeta.Guia && x.Panel.Estado == EstadoPanel.Frena);
        Assert.Equal(ReferenciaGuia.Vos, v.Guia!.Principal);
    }

    [Fact]
    public void MuestraRepetidaNoSeProcesaDosVeces()
    {
        var s = Grabado(out var sim);
        var sm = sim.Paso();
        var a = s.Paso(Ok(sm), 5000);
        var b = s.Paso(Ok(sm), 5000.016);        // misma mSequenceNumber
        Assert.Equal(a.Panel.Firma, b.Panel.Firma);
        Assert.Equal(a.Freno, b.Freno);
        Assert.False(b.Pitar);
    }

    [Fact]
    public void SelectorDeDatosCargaClaseYMapaDeLaPistaVarianteYClaseActual()
    {
        var s = Grabado(out _);
        var al = new Almacen(_dir);
        var d = SelectorDatos.Cargar(al, new ContextoJuego("Demo Circuit", "GP", "GT3"));
        Assert.True(d.HayCurvas);
        Assert.NotNull(d.Mapa);
        Assert.Equal(["GT3"], SelectorDatos.ClasesGuardadas(al, "Demo Circuit", "GP"));
        var otra = SelectorDatos.Cargar(al, new ContextoJuego("Demo Circuit", "GP", "GT4"));
        Assert.False(otra.HayCurvas);
        Assert.NotNull(otra.Mapa);                          // el mapa es de la pista, no de la clase
        Assert.Empty(SelectorDatos.ClasesGuardadas(al, "Otra", "X"));
        Assert.NotEqual(d.Sello, otra.Sello);
        Assert.Equal(0, SelectorDatos.Cargar(al, new ContextoJuego("Nada", "", "GT3")).Sello);
        _ = s;
    }

    [Fact]
    public void ContextoDelJuegoLeeLaClaseDelAutoVisto()
    {
        var sm = Muestras.Valida();
        var c = ContextoJuego.De(sm)!;
        Assert.Equal(new ContextoJuego("Silverstone", "Grand Prix", "GT3"), c);
        Muestras.Texto(sm.mTrackLocation, "");
        Assert.Null(ContextoJuego.De(sm));
    }
}
