using System.Text.Json;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using Xunit;

namespace AMS2Frenado.Tests;

public sealed class CartelesTests : IDisposable
{
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    // distancias frenar->vertice de Watkins Glen GP / GT3_Gen2
    [Theory]
    [InlineData(199, 200)]
    [InlineData(302, 300)]
    [InlineData(208, 200)]
    [InlineData(193, 200)]
    [InlineData(196, 200)]
    [InlineData(131, 100)]
    [InlineData(135, 100)]
    [InlineData(147, 100)]       // 147 esta mas cerca de 100 (47) que de 200 (53)
    public void RedondeaAlCartelMasCercano(double metros, int esperado)
        => Assert.Equal(esperado, Carteles.Redondear(metros, Carteles.PorDefecto));

    [Theory]
    [InlineData(150, 200)]       // empate exacto: gana el mayor (mas conservador)
    [InlineData(250, 300)]
    [InlineData(350, 400)]
    [InlineData(75, 100)]
    [InlineData(10, 50)]         // por debajo del menor: el menor
    [InlineData(900, 400)]       // por encima del mayor: el mayor
    public void EnEmpateGanaElMayor(double metros, int esperado)
        => Assert.Equal(esperado, Carteles.Redondear(metros, Carteles.PorDefecto));

    [Fact]
    public void ElConjuntoPorPistaCambiaElRedondeo()
    {
        var sin400y300 = Carteles.Normalizar([50, 100, 200, 100, 0, -5]);      // desordenado, repetido y con basura
        Assert.Equal([200, 100, 50], sin400y300);
        Assert.Equal(200, Carteles.Redondear(302, sin400y300));             // no existe el 300
        Assert.Equal(300, Carteles.Redondear(302, Carteles.Normalizar([300, 200, 100, 50])));
        Assert.Equal(Carteles.PorDefecto, Carteles.Normalizar(null));
        Assert.Equal(Carteles.PorDefecto, Carteles.Normalizar([]));
        Assert.Equal([200, 100], Carteles.Normalizar(null, [100, 200]));
    }

    [Fact]
    public void ElDesfaseSeRestaAntesDeRedondear()
    {
        var b = new BloqueCurva { FrenarEn = 1000, Vertice = 1230 };
        Assert.Equal(200, Carteles.Calcular(b, 5000, 0, Carteles.PorDefecto));       // 230 -> 200
        Assert.Equal(300, Carteles.Calcular(b, 5000, -40, Carteles.PorDefecto));     // 270 -> 300
        Assert.Equal(100, Carteles.Calcular(b, 5000, 90, Carteles.PorDefecto));      // 140 -> 100
        Assert.Equal(130, Carteles.Distancia(1000, 1230, 5000, 100));
    }

    [Fact]
    public void LaDistanciaEsCircularEnLaLineaDeMeta()
    {
        // frena en 5340 y el vertice esta en 40 (despues de la meta) en una pista de 5440
        Assert.Equal(140, Carteles.Distancia(5340, 40, 5440, 0), 6);
        var b = new BloqueCurva { FrenarEn = 5340, Vertice = 40 };
        Assert.Equal(100, Carteles.Calcular(b, 5440, 0, Carteles.PorDefecto));
    }

    [Fact]
    public void LosCartelesDelMapaSeLeenYSeConservanAlRegrabar()
    {
        var al = new Almacen(_dir);
        // mapa con carteles editados a mano y un campo ajeno
        var mapa = new MapaPista { Pista = "Demo Circuit", Variante = "GP", Largo = 3000, Vueltas = 1, Carteles = [300, 200, 100, 50] };
        mapa.Puntos.Add(new PuntoMapa { D = 0, X = 1, Z = 2 });
        al.GuardarMapa(mapa);
        string texto = File.ReadAllText(al.RutaMapa("Demo Circuit", "GP"));
        Assert.Contains("\"carteles\"", texto);

        var leido = al.LeerMapa("Demo Circuit", "GP")!;
        Assert.Equal([300, 200, 100, 50], leido.Carteles);

        // sin carteles no se escribe el campo
        var al2 = new Almacen(Path.Combine(_dir, "otro"));
        al2.GuardarMapa(new MapaPista { Pista = "P", Variante = "V" });
        Assert.DoesNotContain("carteles", File.ReadAllText(al2.RutaMapa("P", "V")));

        // el grabador no pisa los carteles al regrabar el mapa
        var sim = new Simulador(ias: 3);
        var g = new Grabador(al, 0);
        for (int i = 0; i < 200_000 && sim.Autos[0].Vueltas < 4; i++) { var sm = sim.Paso(); g.Procesar(in sm, sim.Tiempo); }
        g.Guardar();
        var regrabado = al.LeerMapa("Demo Circuit", "GP")!;
        Assert.Equal([300, 200, 100, 50], regrabado.Carteles);
        Assert.True(regrabado.Puntos.Count > 100);
    }
}
