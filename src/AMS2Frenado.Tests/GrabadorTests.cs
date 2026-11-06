using System.Text.Json;
using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using Xunit;

namespace AMS2Frenado.Tests;

public sealed class GrabadorTests : IDisposable
{
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* limpieza best effort */ } }

    /// <summary>Corre el simulador hasta que el jugador completa 'vueltas' cruces de meta.</summary>
    private static void Correr(Simulador sim, Grabador g, int vueltas, Action<Simulador>? alPaso = null)
    {
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < vueltas; i++)
        {
            alPaso?.Invoke(sim);
            var sm = sim.Paso();
            g.Procesar(in sm, sim.Tiempo);
        }
    }

    private static CurvaGuardada Cerca(DatosClase d, double vertice, double tol = 40)
        => Assert.Single(d.Curvas, c => Math.Abs(c.Vertice - vertice) <= tol);

    [Fact]
    public void GrabaIaYVosConLasCincoCurvasDelSimulador()
    {
        var sim = new Simulador(ias: 3);
        var g = new Grabador(new Almacen(_dir), 0);
        Correr(sim, g, 9);
        g.Guardar();

        var datos = new Almacen(_dir).LeerClase("Demo Circuit", "GP", "GT3");
        Assert.NotNull(datos);
        Assert.Equal(5, datos!.Curvas.Count);

        // frenadas fuertes: el vertice de la IA queda ~ al final de la zona, y vos y la IA se emparejan
        foreach (var (fin, tipo) in new[] { (590.0, "frenada"), (1340.0, "frenada"), (2400.0, "frenada") })
        {
            var c = Cerca(datos, fin);
            Assert.Equal(tipo, c.Tipo);
            Assert.NotNull(c.Ia);
            Assert.NotNull(c.Vos);
            Assert.InRange(c.Vos!.Vueltas, 4, 9);
        }
        var c1 = Cerca(datos, 590);
        Assert.InRange(c1.Vos!.FrenarEn, 440 - 12, 440 + 12);
        Assert.InRange(c1.Ia!.FrenarEn, 455 - 15, 455 + 15);
        Assert.InRange(c1.Ia.VVerticeKmh, 26 * 3.6 - 6, 26 * 3.6 + 6);

        Assert.Equal("toque", Cerca(datos, 1830).Tipo);
        Assert.Equal("levanta", Cerca(datos, 230, 70).Tipo);
        Assert.Equal(new[] { "Curva 1", "Curva 2", "Curva 3", "Curva 4", "Curva 5" }, datos.Curvas.Select(c => c.Nombre));
    }

    [Fact]
    public void LaPrimeraVueltaQueSeVeNoCuenta()
    {
        var sim = new Simulador(ias: 0);
        var g = new Grabador(new Almacen(_dir), 0);
        Correr(sim, g, 3);          // arranca a mitad de pista: la 1ra es parcial; cuentan las vueltas 1 y 2
        var e = Assert.Single(g.Estados());
        Assert.Equal(2, e.VueltasVos);
        Assert.Equal(0, e.VueltasIa);
    }

    [Fact]
    public void UnaVueltaConBoxesNoCuenta()
    {
        var sim = new Simulador(ias: 0);
        var g = new Grabador(new Almacen(_dir), 0);
        Correr(sim, g, 6, s =>
        {
            // la vuelta 3 del jugador pasa por boxes un rato
            var a = s.Autos[0];
            a.EnPits = a.Vueltas == 3 && a.D is > 1000 and < 1100;
        });
        Assert.Equal(4, Assert.Single(g.Estados()).VueltasVos);     // vueltas 1,2,4,5 (no la 0 ni la 3)
    }

    [Fact]
    public void UnaVueltaInvalidadaNoCuenta()
    {
        var sim = new Simulador(ias: 0);
        var g = new Grabador(new Almacen(_dir), 0);
        Correr(sim, g, 6, s =>
        {
            var a = s.Autos[0];
            if (a.Vueltas == 2 && a.D > 1500) a.Invalida = true;
        });
        Assert.Equal(4, Assert.Single(g.Estados()).VueltasVos);
    }

    [Fact]
    public void UnaIaEnBoxesNoEnsuciaLaGrabacion()
    {
        var sim = new Simulador(ias: 2);
        var g = new Grabador(new Almacen(_dir), 0);
        Correr(sim, g, 6, s => s.Autos[2].EnPits = true);       // la IA 2 nunca sale de boxes
        var e = Assert.Single(g.Estados());
        Assert.True(e.VueltasIa >= 4);
        // solo vueltas de la IA 1: cada vuelta valida es de ese auto
        Assert.True(e.VueltasIa <= 6);
    }

    [Fact]
    public void DosClasesNoSeMezclan()
    {
        // GT3 con las zonas normales y una segunda clase cuyas curvas estan en otro lado
        var sim = new Simulador(ias: 2, clase: "GT3");
        var otras = new[] { new Simulador.Zona(800, 900, 30), new Simulador.Zona(1500, 1600, 35) };
        var rng = new Random(7);
        var ajeno = new Simulador.Auto(otras, 3, rng) { Clase = "Formula", D = 100 };
        sim.Autos.Add(ajeno);
        var ajeno2 = new Simulador.Auto(otras, 3, rng) { Clase = "Formula", D = 1800 };
        sim.Autos.Add(ajeno2);

        var g = new Grabador(new Almacen(_dir), 0);
        Correr(sim, g, 8);
        g.Guardar();

        var al = new Almacen(_dir);
        var gt3 = al.LeerClase("Demo Circuit", "GP", "GT3")!;
        var f = al.LeerClase("Demo Circuit", "GP", "Formula")!;
        Assert.NotNull(f);
        Assert.Equal(2, f.Curvas.Count);
        Assert.All(f.Curvas, c => Assert.Null(c.Vos));            // el jugador es GT3
        Assert.DoesNotContain(gt3.Curvas, c => Math.Abs(c.Vertice - 900) < 60);
        Assert.DoesNotContain(f.Curvas, c => Math.Abs(c.Vertice - 590) < 60);
        Assert.True(File.Exists(Path.Combine(_dir, "Demo Circuit", "GP", "GT3.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "Demo Circuit", "GP", "Formula.json")));
    }

    [Fact]
    public void ArmaElMapaCadaCincoMetros()
    {
        var sim = new Simulador(ias: 2);
        var g = new Grabador(new Almacen(_dir), 0);
        Correr(sim, g, 5);
        g.Guardar();
        var mapa = new Almacen(_dir).LeerMapa("Demo Circuit", "GP")!;
        Assert.Equal(3000, mapa.Largo, 1);
        Assert.Equal(5, mapa.Paso);
        Assert.InRange(mapa.Puntos.Count, 590, 601);
        Assert.Equal(5, mapa.Puntos[1].D - mapa.Puntos[0].D);
        double radio = Simulador.Largo / (2 * Math.PI);
        var p = mapa.Puntos.First(q => q.D == 750);                // un cuarto de vuelta: x ~ 0, z ~ radio
        Assert.InRange(p.X, -3, 3);
        Assert.InRange(p.Z, radio - 3, radio + 3);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("largo0")]
    [InlineData("nombre")]
    [InlineData("noJugando")]
    public void DescartaLecturasSinSentidoYNoGrabaNada(string caso)
    {
        var sim = new Simulador(ias: 1);
        var g = new Grabador(new Almacen(_dir), 0);
        for (int i = 0; i < 5000; i++)
        {
            var sm = sim.Paso();
            switch (caso)
            {
                case "version": sm.mVersion = 13; break;
                case "largo0": sm.mTrackLength = 0; break;
                case "nombre": Muestras.Texto(sm.mTrackLocation, ""); break;
                case "noJugando": sm.mGameState = 6; break;
            }
            Assert.NotEqual(ResultadoGrabacion.Ok, g.Procesar(in sm, sim.Tiempo));
        }
        Assert.Equal(5000, g.Descartadas);
        Assert.Empty(g.Estados());
        g.Guardar();
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void GuardadoAtomicoDejaRespaldoYSinTmp()
    {
        var sim = new Simulador(ias: 2);
        var al = new Almacen(_dir);
        var g = new Grabador(al, 1e9);
        Correr(sim, g, 5);
        g.Guardar();
        string ruta = al.RutaClase("Demo Circuit", "GP", "GT3");
        Assert.True(File.Exists(ruta));
        Assert.False(File.Exists(ruta + ".bak"));            // primera vez: no habia nada que respaldar
        string antes = File.ReadAllText(ruta);

        Correr(sim, g, 7);
        g.Guardar();
        Assert.True(File.Exists(ruta + ".bak"));
        Assert.Equal(antes, File.ReadAllText(ruta + ".bak")); // el respaldo es la version anterior
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ruta)!, "*.tmp"));
        JsonDocument.Parse(File.ReadAllText(ruta)).Dispose();  // y el principal es JSON valido
    }

    [Fact]
    public void SiElPrincipalEstaDanadoSeLeeElRespaldo()
    {
        var sim = new Simulador(ias: 1);
        var al = new Almacen(_dir);
        var g = new Grabador(al, 1e9);
        Correr(sim, g, 5);
        g.Guardar();
        string ruta = al.RutaClase("Demo Circuit", "GP", "GT3");
        File.Copy(ruta, ruta + ".bak", overwrite: true);
        File.WriteAllText(ruta, "{ esto no es json");
        Assert.NotEmpty(al.LeerClase("Demo Circuit", "GP", "GT3")!.Curvas);
    }

    [Fact]
    public void NoPisaNombresNiNotasEditadosAMano()
    {
        var sim = new Simulador(ias: 2);
        var al = new Almacen(_dir);
        var g = new Grabador(al, 0);
        Correr(sim, g, 5);
        g.Guardar();

        // el usuario edita el archivo: renombra, agrega una nota y un campo propio
        string ruta = al.RutaClase("Demo Circuit", "GP", "GT3");
        var datos = al.LeerClase("Demo Circuit", "GP", "GT3")!;
        var c = Cerca(datos, 590);
        c.Nombre = "La Horquilla";
        c.Nota = "frenar en el cartel de 100";
        c.Extra = new() { ["color"] = JsonDocument.Parse("\"rojo\"").RootElement.Clone() };
        File.WriteAllText(ruta, JsonSerializer.Serialize(datos, Almacen.OpcionesJson));

        // otra sesion: grabador nuevo, con datos algo distintos
        var g2 = new Grabador(new Almacen(_dir), 0);
        Correr(new Simulador(ias: 2, semilla: 99), g2, 6);
        g2.Guardar();

        var d2 = al.LeerClase("Demo Circuit", "GP", "GT3")!;
        var c2 = Cerca(d2, 590);
        Assert.Equal("La Horquilla", c2.Nombre);
        Assert.Equal("frenar en el cartel de 100", c2.Nota);
        Assert.Equal("rojo", c2.Extra!["color"].GetString());
        Assert.Equal(5, d2.Curvas.Count);
        Assert.Equal(d2.Curvas.Count, d2.Curvas.Select(x => x.Nombre).Distinct().Count());
    }

    [Fact]
    public void UnaSesionSoloConIaConservaLoQueYaHabiaDeVos()
    {
        var al = new Almacen(_dir);
        var g = new Grabador(al, 0);
        Correr(new Simulador(ias: 2), g, 5);
        g.Guardar();
        var antes = Cerca(al.LeerClase("Demo Circuit", "GP", "GT3")!, 590).Vos!;

        // segunda sesion: el auto visto esta en boxes todo el tiempo, solo graba la IA
        var g2 = new Grabador(new Almacen(_dir), 0);
        Correr(new Simulador(ias: 2, semilla: 5), g2, 5, s => s.Autos[0].EnPits = true);
        g2.Guardar();
        var despues = Cerca(al.LeerClase("Demo Circuit", "GP", "GT3")!, 590);
        Assert.NotNull(despues.Ia);
        Assert.Equal(antes.FrenarEn, despues.Vos!.FrenarEn);
    }

    [Fact]
    public void UnaSesionSinCurvasNoVaciaElArchivo()
    {
        var al = new Almacen(_dir);
        var g = new Grabador(al, 0);
        Correr(new Simulador(ias: 2), g, 5);
        g.Guardar();
        string ruta = al.RutaClase("Demo Circuit", "GP", "GT3");
        string antes = File.ReadAllText(ruta);

        var g2 = new Grabador(new Almacen(_dir), 0);
        var sim = new Simulador(ias: 1);
        Correr(sim, g2, 1);              // una sola vuelta (la parcial): ninguna valida
        g2.Guardar();
        Assert.Equal(antes, File.ReadAllText(ruta));
    }

    [Fact]
    public void NombreSeguroReemplazaCaracteresInvalidos()
    {
        Assert.Equal("Spa_Francorchamps_ GP", Almacen.NombreSeguro("Spa/Francorchamps: GP"));
        Assert.Equal("_", Almacen.NombreSeguro("..."));
    }
}
