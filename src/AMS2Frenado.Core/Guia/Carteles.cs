using AMS2Frenado.Core.Grabacion;

namespace AMS2Frenado.Core.Guia;

/// <summary>
/// Carteles de frenada del circuito (400, 300, 200, 100, 50 m). El numero grande del panel es el
/// cartel en el que hay que frenar, no el metro de la pista. Ver docs/CARTELES.md.
/// </summary>
public static class Carteles
{
    public static readonly IReadOnlyList<int> PorDefecto = [400, 300, 200, 100, 50];

    /// <summary>Quita repetidos y valores no positivos, y ordena de mayor a menor. Vacio o null: el conjunto por defecto.</summary>
    public static IReadOnlyList<int> Normalizar(IEnumerable<int>? carteles, IReadOnlyList<int>? porDefecto = null)
    {
        var l = (carteles ?? []).Where(c => c > 0).Distinct().OrderByDescending(c => c).ToList();
        return l.Count > 0 ? l : (porDefecto is { Count: > 0 } ? Normalizar(porDefecto) : PorDefecto);
    }

    /// <summary>Distancia del cartel: (vertice - frenar_en) con modulo de pista circular, menos el desfase.</summary>
    public static double Distancia(double frenarEn, double vertice, double largo, double desfaseM)
        => Geometria.Mod(vertice - frenarEn, largo) - desfaseM;

    /// <summary>Cartel mas cercano del conjunto. En empate exacto gana el mayor (el mas conservador: frenar antes).</summary>
    public static int Redondear(double metros, IReadOnlyList<int> conjunto)
    {
        int mejor = conjunto[0];
        double dMejor = Math.Abs(metros - mejor);
        foreach (int c in conjunto.Skip(1))
        {
            double d = Math.Abs(metros - c);
            if (d < dMejor - 1e-9 || (Math.Abs(d - dMejor) <= 1e-9 && c > mejor)) { mejor = c; dMejor = d; }
        }
        return mejor;
    }

    public static int Calcular(BloqueCurva b, double largo, double desfaseM, IReadOnlyList<int> conjunto)
        => Redondear(Distancia(b.FrenarEn, b.Vertice, largo, desfaseM), conjunto);
}
