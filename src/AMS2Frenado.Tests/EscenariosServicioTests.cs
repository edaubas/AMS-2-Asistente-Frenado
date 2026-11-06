using System.Text.Json;
using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>
/// Escenarios del servicio completo con datos guardados armados a mano (sin simulador): causas del SIN DATOS,
/// referencia principal en el pitido y el simbolo, y el caso real de curvas solo "vos". Compila y corre en Linux;
/// la ventana y el sonido no se ejecutaron.
/// </summary>
public sealed class EscenariosServicioTests : IDisposable
{
    private const double Largo = 5891;        // Muestras.Valida: Silverstone · Grand Prix, GT3
    private const double Dt = 1 / 27.0;
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static ResultadoLectura Ok(in SharedMemory sm) => new(EstadoLectura.Ok, ConstantesShm.Version, sm);

    private static BloqueCurva Bl(double frenar, double vertice, int vmin = 80)
        => new() { FrenarEn = frenar, Vertice = vertice, VVerticeKmh = vmin, VEntradaKmh = 200, LargoFrenada = vertice - frenar, Vueltas = 8 };

    private static CurvaGuardada C(string nombre, double frenar, double vertice, bool ia, bool vos, string tipo = "frenada")
        => new() { Nombre = nombre, Vertice = vertice, Tipo = tipo, Ia = ia ? Bl(frenar, vertice) : null, Vos = vos ? Bl(frenar, vertice) : null };

    private void Guardar(double largo, params CurvaGuardada[] curvas)
        => new Almacen(_dir).GuardarClase(new DatosClase
        {
            Pista = "Silverstone", Variante = "Grand Prix", Clase = "GT3", Largo = largo, Curvas = [.. curvas],
        });

    private static SharedMemory Muestra(uint seq, double d, double ms, double freno = 0, bool boxes = false)
    {
        var sm = Muestras.Valida(seq);
        sm.mParticipantInfo[0].mCurrentLapDistance = (float)d;
        sm.mSpeed = (float)ms;
        sm.mBrake = (float)freno;
        sm.mPitModes[0] = boxes ? 3u : 0u;
        return sm;
    }

    // ---------------------------------------------------------------- 1. causas del SIN DATOS

    [Fact]
    public void EnBoxesConDatosBuenosDiceQueLaGuiaArrancaAlSalirAPista()
    {
        Guardar(Largo, C("Curva 1", 1000, 1150, true, true));
        var s = new ServicioOverlay(new Almacen(_dir));
        var v = s.Paso(Ok(Muestra(2, 100, 0, boxes: true)), 1);
        Assert.Equal(TipoTarjeta.SinDatos, v.Panel.Tipo);
        Assert.Equal("SIN DATOS", v.Panel.Titulo);
        Assert.Equal("En boxes: la guía arranca al salir a pista.", v.Panel.Linea1);
        Assert.Equal(CausaSinDatos.EnBoxes, v.Guia!.Causa);
        Assert.DoesNotContain("No hay curvas", v.Panel.Linea1);

        // al salir a pista aparece la guia
        var fuera = s.Paso(Ok(Muestra(4, 100, 30)), 2);
        Assert.Equal(TipoTarjeta.Lejos, fuera.Panel.Tipo);
    }

    [Fact]
    public void SinArchivoDiceNoHayCurvasGuardadasConClaseYPista()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        var v = s.Paso(Ok(Muestra(2, 100, 30)), 1);
        Assert.Equal("No hay curvas guardadas para GT3 en Silverstone · Grand Prix.", v.Panel.Linea1);
        Assert.Contains("Grabar todo", v.Panel.Linea2);
        Assert.Equal(CausaSinDatos.SinCurvas, v.Guia!.Causa);

        // y en boxes sin archivo la causa de datos manda: es lo que de verdad falta
        var s2 = new ServicioOverlay(new Almacen(_dir));
        var b = s2.Paso(Ok(Muestra(2, 100, 0, boxes: true)), 1);
        Assert.StartsWith("No hay curvas guardadas", b.Panel.Linea1);
    }

    [Fact]
    public void ArchivoSinCurvasDiceNoHayCurvasGuardadas()
    {
        Guardar(Largo);
        var v = new ServicioOverlay(new Almacen(_dir)).Paso(Ok(Muestra(2, 100, 30)), 1);
        Assert.StartsWith("No hay curvas guardadas para GT3", v.Panel.Linea1);
    }

    [Fact]
    public void LargoDeLaPistaDistintoDiceAmbosLargos()
    {
        Guardar(5000, C("Curva 1", 1000, 1150, true, true));
        var v = new ServicioOverlay(new Almacen(_dir)).Paso(Ok(Muestra(2, 100, 30)), 1);
        Assert.Equal("Largo de pista distinto al guardado (guardado 5000 m, juego 5891 m).", v.Panel.Linea1);
        Assert.Equal(CausaSinDatos.LargoDistinto, v.Guia!.Causa);
        Assert.Equal(5000, v.Guia.LargoGuardadoM);
        Assert.Equal(5891, v.Guia.LargoJuegoM);
        // tambien dentro de boxes: el largo no depende de estar en pista
        var b = new ServicioOverlay(new Almacen(_dir)).Paso(Ok(Muestra(2, 100, 0, boxes: true)), 1);
        Assert.StartsWith("Largo de pista distinto", b.Panel.Linea1);
    }

    [Fact]
    public void ArchivoDanadoDiceQueNoSePudoLeer()
    {
        var al = new Almacen(_dir);
        Directory.CreateDirectory(al.CarpetaPista("Silverstone", "Grand Prix"));
        File.WriteAllText(al.RutaClase("Silverstone", "Grand Prix", "GT3"), "{ esto no es json");
        var v = new ServicioOverlay(al).Paso(Ok(Muestra(2, 100, 30)), 1);
        Assert.Equal("No se pudo leer el archivo de datos.", v.Panel.Linea1);
        Assert.Contains("GT3.json", v.Panel.Linea2);
        Assert.DoesNotContain("No hay curvas", v.Panel.Linea1);
    }

    [Fact]
    public void ArchivoDanadoConRespaldoBuenoUsaElRespaldoYMuestraLaGuia()
    {
        Guardar(Largo, C("Curva 1", 1000, 1150, true, true));
        var al = new Almacen(_dir);
        string ruta = al.RutaClase("Silverstone", "Grand Prix", "GT3");
        File.Copy(ruta, ruta + ".bak");
        File.WriteAllText(ruta, "[]basura");
        var v = new ServicioOverlay(al).Paso(Ok(Muestra(2, 100, 30)), 1);
        Assert.Equal(TipoTarjeta.Lejos, v.Panel.Tipo);
    }

    [Fact]
    public void CurvasSinFrenadoUtilizableDiceDatosSinCurvasValidas()
    {
        // frenar_en == vertice: span 0, la guia descarta la curva
        Guardar(Largo, C("Curva 1", 1150, 1150, true, true));
        var v = new ServicioOverlay(new Almacen(_dir)).Paso(Ok(Muestra(2, 100, 30)), 1);
        Assert.Equal("Datos sin curvas válidas.", v.Panel.Linea1);
        Assert.Equal(CausaSinDatos.CurvasInvalidas, v.Guia!.Causa);
    }

    [Fact]
    public void LaCausaSeVeEnElPanelAunqueNoHayaPitidoNiSimbolo()
    {
        Guardar(5000, C("Curva 1", 1000, 1150, true, true));
        var s = new ServicioOverlay(new Almacen(_dir)) { Pitido = true };
        for (int i = 0; i < 20; i++)
        {
            var v = s.Paso(Ok(Muestra((uint)(2 + 2 * i), 900 + i * 10, 40)), i * Dt);
            Assert.False(v.Pitar);
            Assert.False(s.ActualizarAviso(i * Dt).Visible);
        }
    }

    // ---------------------------------------------------------------- almacen: motivo de la lectura

    [Fact]
    public void AlmacenExponeElMotivoSinRomperLaApiAnterior()
    {
        var al = new Almacen(_dir);
        Assert.Equal(MotivoLectura.NoExiste, al.LeerClaseDetallado("P", "V", "C").Motivo);
        Assert.Null(al.LeerClase("P", "V", "C"));

        Directory.CreateDirectory(al.CarpetaPista("P", "V"));
        string ruta = al.RutaClase("P", "V", "C");
        File.WriteAllText(ruta, "{ roto");
        var d = al.LeerClaseDetallado("P", "V", "C");
        Assert.Equal(MotivoLectura.Danado, d.Motivo);
        Assert.NotNull(d.Detalle);
        Assert.Null(d.Datos);
        Assert.Null(al.LeerClase("P", "V", "C"));            // la API de siempre sigue devolviendo null

        File.WriteAllText(ruta, "null");
        Assert.Equal(MotivoLectura.Danado, al.LeerClaseDetallado("P", "V", "C").Motivo);

        File.WriteAllText(ruta + ".bak", JsonSerializer.Serialize(new DatosClase { Pista = "P", Variante = "V", Clase = "C" }, Almacen.OpcionesJson));
        var r = al.LeerClaseDetallado("P", "V", "C");
        Assert.Equal(MotivoLectura.Ok, r.Motivo);
        Assert.True(r.DeRespaldo);
        Assert.NotNull(al.LeerClase("P", "V", "C"));

        // solo el .bak, danado
        File.Delete(ruta);
        File.WriteAllText(ruta + ".bak", "xx");
        Assert.Equal(MotivoLectura.Danado, al.LeerClaseDetallado("P", "V", "C").Motivo);

        Assert.Equal(MotivoLectura.NoExiste, al.LeerMapaDetallado("P", "V").Motivo);
    }

    [Fact]
    public void SelectorDeDatosTraeElMotivoDeLaClase()
    {
        var al = new Almacen(_dir);
        var ctx = new ContextoJuego("Silverstone", "Grand Prix", "GT3");
        Assert.Equal(MotivoLectura.NoExiste, SelectorDatos.Cargar(al, ctx).MotivoClase);
        Guardar(Largo, C("Curva 1", 1000, 1150, true, true));
        Assert.Equal(MotivoLectura.Ok, SelectorDatos.Cargar(al, ctx).MotivoClase);
        File.WriteAllText(al.RutaClase("Silverstone", "Grand Prix", "GT3"), "{");
        Assert.Equal(MotivoLectura.Danado, SelectorDatos.Cargar(al, ctx).MotivoClase);
    }

    // ---------------------------------------------------------------- 4. referencia principal en pitido y simbolo

    /// <summary>
    /// Recorre las tres curvas del archivo a 40 m/s sin tocar el freno y devuelve, por curva (por vertice), si hubo pitido
    /// y la opacidad maxima del simbolo.
    /// </summary>
    private Dictionary<string, (int pitidos, double opMax, ReferenciaGuia? principal)> Recorrer(ReferenciaGuia referencia, double ini, double fin)
    {
        var s = new ServicioOverlay(new Almacen(_dir)) { Pitido = true, ReferenciaPrincipal = referencia };
        var res = new Dictionary<string, (int, double, ReferenciaGuia?)>();
        double t = 0, tc = 0; uint seq = 2;
        for (double d = ini; d < fin; d += 40 * Dt)
        {
            t += Dt; seq += 2;
            var v = s.Paso(Ok(Muestra(seq, d, 40)), t);
            double op = 0;
            for (; tc <= t; tc += 1 / 60.0) op = Math.Max(op, s.ActualizarAviso(tc).Opacidad);
            if (v.Guia is { Curva: { } c })
            {
                res.TryGetValue(c.Nombre, out var ant);
                res[c.Nombre] = (ant.Item1 + (v.Pitar ? 1 : 0), Math.Max(ant.Item2, op), v.Guia.Principal);
            }
        }
        return res;
    }

    private void GuardarMixtas()
        => Guardar(Largo,
            C("Solo vos", 1000, 1150, false, true),
            C("Solo IA", 2000, 2150, true, false),
            C("Ambas", 3000, 3150, true, true));

    [Fact]
    public void ConIaComoPrincipalNoPitaNiMuestraSimboloEnLaCurvaQueSoloTieneVos()
    {
        GuardarMixtas();
        var r = Recorrer(ReferenciaGuia.Ia, 700, 3300);
        Assert.Equal(0, r["Solo vos"].pitidos);
        Assert.Equal(0, r["Solo vos"].opMax);
        Assert.Equal(ReferenciaGuia.Vos, r["Solo vos"].principal);      // el panel sigue mostrando lo que hay, y de quien es
        Assert.Equal(1, r["Solo IA"].pitidos);
        Assert.Equal(1, r["Solo IA"].opMax, 6);
        Assert.Equal(1, r["Ambas"].pitidos);
        Assert.Equal(1, r["Ambas"].opMax, 6);
    }

    [Fact]
    public void ConVosComoPrincipalNoPitaNiMuestraSimboloEnLaCurvaQueSoloTieneIa()
    {
        GuardarMixtas();
        var r = Recorrer(ReferenciaGuia.Vos, 700, 3300);
        Assert.Equal(1, r["Solo vos"].pitidos);
        Assert.Equal(1, r["Solo vos"].opMax, 6);
        Assert.Equal(0, r["Solo IA"].pitidos);
        Assert.Equal(0, r["Solo IA"].opMax);
        Assert.Equal(ReferenciaGuia.Ia, r["Solo IA"].principal);
        Assert.Equal(1, r["Ambas"].pitidos);
    }

    [Fact]
    public void CambiarLaReferenciaEnMarchaHaceRegirLaNueva()
    {
        GuardarMixtas();
        var s = new ServicioOverlay(new Almacen(_dir)) { Pitido = true, ReferenciaPrincipal = ReferenciaGuia.Ia };
        double t = 0; uint seq = 2; int pitidos = 0;
        for (double d = 900; d < 1300; d += 40 * Dt)
        {
            t += Dt; seq += 2;
            if (d > 950) s.ReferenciaPrincipal = ReferenciaGuia.Vos;        // antes del punto de frenado de la curva solo-vos
            pitidos += s.Paso(Ok(Muestra(seq, d, 40)), t).Pitar ? 1 : 0;
        }
        Assert.Equal(1, pitidos);
    }

    [Fact]
    public void LasCurvasSoloVosDeTipoLevantaNoPitanComoEnElArchivoRealDelUsuario()
    {
        // F-Ultimate_Gen2 en Spa: Curva 9, 8 y 10 son "levanta" con solo vos; el resto tiene ia + vos
        Guardar(Largo,
            C("Curva 8", 1000, 1100, false, true, tipo: "levanta"),
            C("Curva 9", 1500, 1600, false, true, tipo: "levanta"),
            C("Curva 10", 2000, 2100, false, true, tipo: "levanta"),
            C("Curva 11", 3000, 3150, true, true));
        var r = Recorrer(ReferenciaGuia.Ia, 700, 3300);
        Assert.All(new[] { "Curva 8", "Curva 9", "Curva 10" }, n => { Assert.Equal(0, r[n].pitidos); Assert.Equal(0, r[n].opMax); });
        Assert.Equal(1, r["Curva 11"].pitidos);
    }

    [Fact]
    public void SiYaVasFrenandoAlLlegarNoPitaNiMuestraYSiSoloFrenasDespuesSi()
    {
        Guardar(Largo, C("Curva 1", 3000, 3150, true, true));
        // a) el pedal esta apretado desde 20 m antes del punto
        var s = new ServicioOverlay(new Almacen(_dir)) { Pitido = true };
        double t = 0, tc = 0, opMax = 0; uint seq = 2; int pitidos = 0;
        for (double d = 2700; d < 3300; d += 40 * Dt)
        {
            t += Dt; seq += 2;
            var v = s.Paso(Ok(Muestra(seq, d, 40, freno: d >= 2980 ? 0.6 : 0)), t);
            pitidos += v.Pitar ? 1 : 0;
            for (; tc <= t; tc += 1 / 60.0) if (d > 3010) opMax = Math.Max(opMax, s.ActualizarAviso(tc).Opacidad);
        }
        Assert.Equal(0, pitidos);
        Assert.Equal(0, opMax);

        // b) frenas 0,4 s despues del punto: pita una vez
        var s2 = new ServicioOverlay(new Almacen(_dir)) { Pitido = true };
        t = 0; seq = 2; pitidos = 0;
        for (double d = 2700; d < 3300; d += 40 * Dt)
        {
            t += Dt; seq += 2;
            pitidos += s2.Paso(Ok(Muestra(seq, d, 40, freno: d >= 3016 ? 0.6 : 0)), t).Pitar ? 1 : 0;
        }
        Assert.Equal(1, pitidos);
    }
}
