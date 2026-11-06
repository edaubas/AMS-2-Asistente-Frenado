using AMS2Frenado.Core.Grabacion;
using Xunit;

namespace AMS2Frenado.Tests;

public class AgrupadorTests
{
    private const double Largo = 3000;

    private static EventoFrenada Ev(double frenarEn, double apice, double v0 = 60, double vMin = 25, int prioridad = 3, int vuelta = 1)
        => new(frenarEn, apice - frenarEn, apice, v0, vMin, vuelta, Tipos.Clasificar(v0 - vMin), prioridad);

    private static Dictionary<ClaveVuelta, List<EventoFrenada>> Vueltas(params (int vuelta, EventoFrenada[] evs)[] v)
        => v.ToDictionary(x => new ClaveVuelta(0, x.vuelta), x => x.evs.ToList());

    [Fact]
    public void AgrupaPorMedianaYEsRobustaAUnaFrenadaRara()
    {
        var v = Vueltas(
            (1, [Ev(440, 590)]), (2, [Ev(444, 592)]), (3, [Ev(436, 589)]), (4, [Ev(520, 600)]), (5, [Ev(442, 591)]));
        var g = Assert.Single(Agrupador.Agrupar(v, 5, Largo));
        Assert.InRange(g.Bloque.FrenarEn, 438, 446);     // la vuelta de 520 no la corre
        Assert.Equal(5, g.Bloque.Vueltas);
        Assert.True(g.Bloque.DispersionM > 0);
        Assert.Equal(TipoCurva.Frenada, g.Tipo);
        Assert.InRange(g.Bloque.Vertice, 589, 592);
    }

    [Fact]
    public void ExigeAlMenosLaMitadDeLasVueltas()
    {
        var v = Vueltas(
            (1, [Ev(440, 590), Ev(1200, 1340)]), (2, [Ev(441, 590)]), (3, [Ev(439, 591)]), (4, [Ev(1201, 1340)]));
        var gs = Agrupador.Agrupar(v, 4, Largo);
        Assert.Equal(2, gs.Count);                      // 3 de 4 y 2 de 4: las dos pasan la mitad
        var solo1 = Vueltas((1, [Ev(2000, 2100)]), (2, []), (3, []), (4, []));
        Assert.Empty(Agrupador.Agrupar(solo1, 4, Largo));
    }

    [Fact]
    public void ElPoolDeVariosAutosCuentaCadaVueltaDeCadaAuto()
    {
        // 6 autos con una sola vuelta cada uno: la mitad son 3 vueltas
        var v = Enumerable.Range(0, 6).ToDictionary(i => new ClaveVuelta(i, 1),
            i => i < 4 ? new List<EventoFrenada> { Ev(440 + i, 590) } : new List<EventoFrenada>());
        var g = Assert.Single(Agrupador.Agrupar(v, 6, Largo));
        Assert.Equal(4, g.Bloque.Vueltas);
    }

    [Fact]
    public void CurvaQueCruzaLaMetaSeJunta()
    {
        var v = Vueltas((1, [Ev(2980, 40)]), (2, [Ev(5, 55)]), (3, [Ev(2990, 48)]));
        var g = Assert.Single(Agrupador.Agrupar(v, 3, Largo));
        Assert.True(g.Vertice < 80 || g.Vertice > 2950);
        Assert.Equal(3, g.Bloque.Vueltas);
    }

    [Fact]
    public void DepurarDescartaElSuaveQueSolapaConLaFrenadaFuerte()
    {
        var fuerte = Ev(440, 590, prioridad: 3);
        var suave = Ev(450, 585, v0: 62, vMin: 55, prioridad: 2);        // misma curva, vista por el suave
        var otro = Ev(1750, 1830, v0: 70, vMin: 64, prioridad: 2);       // otra curva: se queda
        var r = Agrupador.Depurar([fuerte, suave, otro], Largo);
        Assert.Equal(2, r.Count);
        Assert.DoesNotContain(suave, r);
    }

    [Fact]
    public void EmparejaIaYVosPorElVerticeNoPorElPuntoDeFrenado()
    {
        // la IA frena 40 m antes que vos, pero el vertice es casi el mismo
        var ia = Agrupador.Agrupar(Vueltas((1, [Ev(400, 590)]), (2, [Ev(402, 591)])), 2, Largo);
        var vos = Agrupador.Agrupar(Vueltas((1, [Ev(440, 592)]), (2, [Ev(442, 593)])), 2, Largo);
        var c = Assert.Single(Agrupador.Emparejar(ia, vos, Largo));
        Assert.NotNull(c.Ia);
        Assert.NotNull(c.Vos);
        Assert.True(c.Vos!.FrenarEn - c.Ia!.FrenarEn > 30);
    }

    [Fact]
    public void LosVerticesLejanosNoSeEmparejan()
    {
        var ia = Agrupador.Agrupar(Vueltas((1, [Ev(440, 590)])), 1, Largo);
        var vos = Agrupador.Agrupar(Vueltas((1, [Ev(1200, 1340)])), 1, Largo);
        var r = Agrupador.Emparejar(ia, vos, Largo);
        Assert.Equal(2, r.Count);
        Assert.Contains(r, c => c.Ia is not null && c.Vos is null);
        Assert.Contains(r, c => c.Ia is null && c.Vos is not null);
    }
}
