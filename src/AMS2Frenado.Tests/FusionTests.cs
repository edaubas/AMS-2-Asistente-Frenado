using AMS2Frenado.Core.Grabacion;
using Xunit;

namespace AMS2Frenado.Tests;

public class FusionTests
{
    private const double Largo = 5440;

    private static BloqueCurva B(double frenarEn, double vertice, int vueltas)
        => new() { FrenarEn = frenarEn, Vertice = vertice, Vueltas = vueltas };

    [Fact]
    public void UnaCurvaSoloTuyaCercaDeUnaDeLaIaNoLeRobaElBloqueViejoDeLaIa()
    {
        // Guardado anterior (la IA ya vio la curva con 67 vueltas)
        var previas = new List<CurvaGuardada>
        {
            new() { Nombre = "Curva 1", Vertice = 361.0, Ia = B(185, 361.0, 67) },
        };
        // Ahora: la IA (98 vueltas) en 361.7 emparejada con vos, y otra curva solo tuya en 287.2
        // (a 74 m de la guardada, dentro de RadioM). Antes, la de 287.2 (que va primero por vertice)
        // se quedaba la guardada y heredaba su bloque IA viejo: la curva de la IA salia duplicada.
        var nuevas = new List<CurvaEmparejada>
        {
            new(287.2, TipoCurva.Frenada, null, B(186.6, 287.2, 1)),
            new(361.7, TipoCurva.Frenada, B(184, 361.7, 98), B(250, 374.2, 2)),
        };
        var res = Fusion.Fusionar(nuevas, previas, Largo);

        Assert.Equal(2, res.Count);
        var soloVos = res.Single(c => c.Vertice == 287.2);
        Assert.Null(soloVos.Ia);
        Assert.NotNull(soloVos.Vos);
        var conIa = res.Single(c => c.Vertice == 361.7);
        Assert.Equal(98, conIa.Ia!.Vueltas);
        Assert.Equal("Curva 1", conIa.Nombre);          // conserva el nombre de la guardada mas cercana
        Assert.Single(res, c => c.Ia is not null);
    }

    [Fact]
    public void SiEstaSesionNoVioALaIaSeConservaLoGrabadoAntes()
    {
        var previas = new List<CurvaGuardada>
        {
            new() { Nombre = "T1", Nota = "frenar fuerte", Vertice = 361.0, Ia = B(185, 361.0, 67) },
        };
        var nuevas = new List<CurvaEmparejada> { new(365, TipoCurva.Frenada, null, B(190, 365, 2)) };
        var c = Assert.Single(Fusion.Fusionar(nuevas, previas, Largo));
        Assert.Equal("T1", c.Nombre);
        Assert.Equal(67, c.Ia!.Vueltas);
        Assert.Equal(2, c.Vos!.Vueltas);
    }
}
