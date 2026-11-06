using System.Security.Cryptography;
using System.Text.Json;
using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>Modo "Grabar solo vos": la IA no se vuelve a grabar y lo que ya estaba guardado de la IA no se toca.</summary>
public sealed class GrabarSoloVosTests : IDisposable
{
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static ResultadoLectura Ok(in SharedMemory sm) => new(EstadoLectura.Ok, ConstantesShm.Version, sm);

    private static void Correr(Simulador sim, Grabador g, int vueltas)
    {
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < vueltas; i++)
        {
            var sm = sim.Paso();
            g.Procesar(in sm, sim.Tiempo);
        }
    }

    private static string Json(object o) => JsonSerializer.Serialize(o, Almacen.OpcionesJson);

    private static string HashArchivo(string ruta) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ruta)));

    /// <summary>Sesion completa (IA + vos) y devuelve lo guardado.</summary>
    private DatosClase GrabarTodo(int ias = 3, int vueltas = 9)
    {
        var g = new Grabador(new Almacen(_dir), 0);
        Correr(new Simulador(ias), g, vueltas);
        g.Guardar();
        return new Almacen(_dir).LeerClase("Demo Circuit", "GP", "GT3")!;
    }

    [Fact]
    public void ElGrabadorSoloVosNoProcesaAlaIa()
    {
        var g = new Grabador(new Almacen(_dir), 0, soloVos: true);
        Assert.True(g.SoloVos);
        Correr(new Simulador(ias: 3), g, 9);
        var e = Assert.Single(g.Estados());
        Assert.Equal(0, e.VueltasIa);
        Assert.Equal(0, e.EventosIa);
        Assert.True(e.VueltasVos >= 4);
        Assert.True(e.EventosVos > 0);
    }

    [Fact]
    public void SinArchivoPrevioSoloCreaCurvasConBloqueVos()
    {
        var g = new Grabador(new Almacen(_dir), 0, soloVos: true);
        Correr(new Simulador(ias: 3), g, 9);
        g.Guardar();
        var d = new Almacen(_dir).LeerClase("Demo Circuit", "GP", "GT3")!;
        Assert.NotEmpty(d.Curvas);
        Assert.All(d.Curvas, c => { Assert.Null(c.Ia); Assert.NotNull(c.Vos); });      // nada de curvas solo-IA
    }

    [Fact]
    public void ConservaLosBloquesIaLosNombresLasNotasYLasCurvasQueEstaSesionNoVio()
    {
        var antes = GrabarTodo();
        Assert.Equal(5, antes.Curvas.Count);
        // ediciones a mano + una curva que solo vio la IA y vos nunca frenas (no la vas a volver a grabar)
        antes.Curvas[1].Nombre = "Eau Rouge"; antes.Curvas[1].Nota = "tocar el freno recien al final";
        var fantasma = new CurvaGuardada
        {
            Nombre = "Curva 9", Vertice = 2800, Tipo = "frenada",
            Ia = new BloqueCurva { FrenarEn = 2700, Vertice = 2800, VEntradaKmh = 200, VVerticeKmh = 90, LargoFrenada = 100, Vueltas = 12 },
        };
        antes.Curvas.Add(fantasma);
        new Almacen(_dir).GuardarClase(antes);
        var iaAntes = antes.Curvas.Select(c => Json(c.Ia ?? new BloqueCurva())).ToList();
        var nombresAntes = antes.Curvas.Select(c => (c.Nombre, c.Nota, c.Vertice)).ToList();

        // otra sesion, solo vos (los parametros del simulador cambian la IA: si se grabara, cambiarian los bloques ia)
        var g = new Grabador(new Almacen(_dir), 0, soloVos: true);
        Correr(new Simulador(ias: 3, semilla: 99), g, 9);
        g.Guardar();

        var despues = new Almacen(_dir).LeerClase("Demo Circuit", "GP", "GT3")!;
        Assert.Equal(antes.Curvas.Count, despues.Curvas.Count);
        for (int i = 0; i < antes.Curvas.Count; i++)
        {
            Assert.Equal(nombresAntes[i], (despues.Curvas[i].Nombre, despues.Curvas[i].Nota, despues.Curvas[i].Vertice));
            Assert.Equal(iaAntes[i], Json(despues.Curvas[i].Ia ?? new BloqueCurva()));        // bloque ia intacto
        }
        Assert.Equal("Eau Rouge", despues.Curvas.Single(c => c.Nota is not null).Nombre);
        var f = despues.Curvas.Single(c => c.Nombre == "Curva 9");
        Assert.Equal(12, f.Ia!.Vueltas);
        Assert.Null(f.Vos);
        Assert.Contains(despues.Curvas, c => c.Vos is not null);
    }

    [Fact]
    public void ElMapaExistenteNoSeToca()
    {
        GrabarTodo();
        var al = new Almacen(_dir);
        string ruta = al.RutaMapa("Demo Circuit", "GP");
        Assert.True(File.Exists(ruta));
        string antes = HashArchivo(ruta);
        var mapaAntes = al.LeerMapa("Demo Circuit", "GP")!;

        var g = new Grabador(al, 0, soloVos: true);
        Correr(new Simulador(ias: 3, semilla: 5), g, 12);        // mas vueltas tuyas que las del mapa guardado
        g.Guardar();
        Assert.Equal(antes, HashArchivo(ruta));
        Assert.Equal(mapaAntes.Vueltas, al.LeerMapa("Demo Circuit", "GP")!.Vueltas);
    }

    [Fact]
    public void SinMapaPrevioSeCreaConTusVueltas()
    {
        var g = new Grabador(new Almacen(_dir), 0, soloVos: true);
        Correr(new Simulador(ias: 2), g, 6);
        g.Guardar();
        Assert.NotNull(new Almacen(_dir).LeerMapa("Demo Circuit", "GP"));
    }

    [Fact]
    public void LaCurvaNuevaSoloTuyaCercaDeUnaDeLaIaHeredaNombreYBloqueIa()
    {
        // Fusion en modo solo vos con un vertice cercano (< 80 m): se une a la guardada y conserva su bloque ia
        var previas = new List<CurvaGuardada>
        {
            new() { Nombre = "T1", Vertice = 361.0, Ia = new BloqueCurva { FrenarEn = 185, Vertice = 361, Vueltas = 67 } },
            new() { Nombre = "Curva 2", Vertice = 900, Ia = new BloqueCurva { FrenarEn = 800, Vertice = 900, Vueltas = 40 } },
        };
        var nuevas = new List<CurvaEmparejada>
        {
            new(365, TipoCurva.Frenada, null, new BloqueCurva { FrenarEn = 190, Vertice = 365, Vueltas = 2 }),
            new(2000, TipoCurva.Frenada, null, new BloqueCurva { FrenarEn = 1900, Vertice = 2000, Vueltas = 2 }),
        };
        var res = Fusion.Fusionar(nuevas, previas, 5440, conservarNoEmparejadas: true);
        Assert.Equal(3, res.Count);
        var t1 = res.Single(c => c.Nombre == "T1");
        Assert.Equal(67, t1.Ia!.Vueltas);
        Assert.Equal(2, t1.Vos!.Vueltas);
        Assert.Equal(40, res.Single(c => c.Nombre == "Curva 2").Ia!.Vueltas);       // no la viste: sigue
        Assert.Null(res.Single(c => c.Vertice == 2000).Ia);

        // sin el modo, una curva automatica que no apareció se descarta (comportamiento de siempre)
        Assert.Equal(2, Fusion.Fusionar(nuevas, previas, 5440).Count);
    }

    // ---------------------------------------------------------------- servicio: modo, chip y contadores

    [Fact]
    public void ElServicioEnSoloVosMarcaGrabandoSoloVosMuestraContadoresDeVosYNoGrabaALaIa()
    {
        var sim = new Simulador(ias: 3);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.GrabarSoloVos };
        var todas = new List<VistaOverlay>();
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < 6; i++) todas.Add(s.Paso(Ok(sim.Paso()), sim.Tiempo));
        var v = todas[^1];
        Assert.True(v.Grabando);
        Assert.True(v.SoloVos);
        v = todas.Last(x => x.Panel.Tipo == TipoTarjeta.Grabando);          // los contadores ocupan el lugar de la guia sin datos
        var k = v.Panel.Contadores!;
        Assert.True(k.SoloVos);
        Assert.Equal("", k.Ia);
        Assert.True(int.Parse(k.Vos) >= 2);
        Assert.Contains("la IA ya guardada no se toca", k.Pie);
        Assert.True(v.Guia!.Grabando);

        s.Modo = ModoApp.Mostrar;               // guarda
        var d = new Almacen(_dir).LeerClase("Demo Circuit", "GP", "GT3")!;
        Assert.All(d.Curvas, c => Assert.Null(c.Ia));
    }

    [Fact]
    public void GrabarTodoSigueMostrandoLosDosContadoresYElChipNoEsSoloVos()
    {
        var sim = new Simulador(ias: 3);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        var todas = new List<VistaOverlay>();
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < 6; i++) todas.Add(s.Paso(Ok(sim.Paso()), sim.Tiempo));
        var v = todas.Last(x => x.Panel.Tipo == TipoTarjeta.Grabando);
        Assert.True(v.Grabando);
        Assert.False(v.SoloVos);
        Assert.False(v.Panel.Contadores!.SoloVos);
        Assert.True(int.Parse(v.Panel.Contadores.Ia) >= 2);
    }

    [Fact]
    public void EnSoloVosLaGuiaSeMuestraConLoYaGuardado()
    {
        var sim = new Simulador(ias: 3);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < 9; i++) s.Paso(Ok(sim.Paso()), sim.Tiempo);
        s.Modo = ModoApp.GrabarSoloVos;
        var vistas = new List<VistaOverlay>();
        double t0 = 1000;
        uint objetivo = sim.Autos[0].Vueltas + 3;
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < objetivo; i++) vistas.Add(s.Paso(Ok(sim.Paso()), t0 + sim.Tiempo));
        Assert.Contains(vistas, v => v.Panel.Tipo == TipoTarjeta.Guia && v.Panel.Estado == EstadoPanel.Frena);
        Assert.All(vistas, v => Assert.True(v.SoloVos));
        // cambiar de "todo" a "solo vos" guardo lo que habia, con la IA
        var d = new Almacen(_dir).LeerClase("Demo Circuit", "GP", "GT3")!;
        Assert.Contains(d.Curvas, c => c.Ia is not null);
        s.Modo = ModoApp.Mostrar;
        var d2 = new Almacen(_dir).LeerClase("Demo Circuit", "GP", "GT3")!;
        Assert.Equal(d.Curvas.Select(c => Json(c.Ia ?? new BloqueCurva())), d2.Curvas.Select(c => Json(c.Ia ?? new BloqueCurva())));
    }

    // ---------------------------------------------------------------- configuracion

    [Fact]
    public void LaConfiguracionGuardaElModoNuevoYTolerarLosViejos()
    {
        string ruta = Path.Combine(_dir, "config.json");
        var a = new AlmacenConfig(ruta);
        a.Guardar(new ConfigApp { Modo = ModoApp.GrabarSoloVos, AvisoRampaS = 2.5 });
        Assert.Contains("\"grabarSoloVos\"", File.ReadAllText(ruta));
        var r = new AlmacenConfig(ruta).Cargar();
        Assert.Equal(ModoApp.GrabarSoloVos, r.Modo);
        Assert.Equal(2.5, r.AvisoRampaS);

        // configs de antes: "grabar" y "mostrar" se leen igual; sin avisoRampaS queda el valor por defecto
        File.WriteAllText(ruta, """{ "formato": 1, "modo": "grabar" }""");
        var g = new AlmacenConfig(ruta).Cargar();
        Assert.Equal(ModoApp.Grabar, g.Modo);
        Assert.Equal(2.0, g.AvisoRampaS);
        File.WriteAllText(ruta, """{ "formato": 1, "modo": "mostrar", "avisoRampaS": 9 }""");
        var m = new AlmacenConfig(ruta).Cargar();
        Assert.Equal(ModoApp.Mostrar, m.Modo);
        Assert.Equal(3.0, m.AvisoRampaS);                      // limitada a 1..3
        File.WriteAllText(ruta, """{ "modo": "otraCosa" }""");
        Assert.Equal(ModoApp.Mostrar, new AlmacenConfig(ruta).Cargar().Modo);

        Assert.Equal(0, (int)ModoApp.Mostrar);                 // los valores de siempre no cambian
        Assert.Equal(1, (int)ModoApp.Grabar);
        var c = new ConfigApp();
        c.CopiarDe(new ConfigApp { Modo = ModoApp.GrabarSoloVos, AvisoRampaS = 1.5 });
        Assert.Equal(ModoApp.GrabarSoloVos, c.Modo);
        Assert.Equal(1.5, c.AvisoRampaS);
    }
}
