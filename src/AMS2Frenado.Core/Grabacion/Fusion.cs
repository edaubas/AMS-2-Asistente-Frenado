using System.Text.RegularExpressions;

namespace AMS2Frenado.Core.Grabacion;

/// <summary>
/// Combina las curvas recien calculadas con las que ya estaban guardadas, sin pisar lo que el
/// usuario edito a mano: nombres, notas y campos extra se conservan (port de nombrar() del prototipo).
/// </summary>
public static partial class Fusion
{
    public const double RadioM = 80.0;

    [GeneratedRegex(@"^Curva \d+$")]
    private static partial Regex NombreAutomatico();

    public static bool EditadaAMano(CurvaGuardada c)
        => !string.IsNullOrWhiteSpace(c.Nota) || !NombreAutomatico().IsMatch(c.Nombre) || c.Extra is { Count: > 0 };

    public static List<CurvaGuardada> Fusionar(IReadOnlyList<CurvaEmparejada> nuevas, IReadOnlyList<CurvaGuardada> previas, double largo)
    {
        var usadas = new HashSet<int>();
        var res = new List<CurvaGuardada>();
        foreach (var n in nuevas.OrderBy(c => c.Vertice))
        {
            var c = new CurvaGuardada { Vertice = n.Vertice, Tipo = Tipos.ATexto(n.Tipo), Ia = n.Ia, Vos = n.Vos };
            int mejor = -1; double dMejor = RadioM + 1;
            for (int j = 0; j < previas.Count; j++)
            {
                if (usadas.Contains(j)) continue;
                double d = Math.Abs(Geometria.DifCirc(n.Vertice, previas[j].Vertice, largo));
                if (d <= RadioM && d < dMejor) { mejor = j; dMejor = d; }
            }
            if (mejor >= 0)
            {
                usadas.Add(mejor);
                var p = previas[mejor];
                c.Nombre = p.Nombre;
                c.Nota = p.Nota;
                c.Extra = p.Extra;
                // esta sesion no vio a la IA (o a vos): se conserva lo grabado antes
                c.Ia ??= p.Ia;
                c.Vos ??= p.Vos;
            }
            res.Add(c);
        }
        // curvas con nombre o nota puestos a mano que esta vez no aparecieron: no se pierden
        for (int j = 0; j < previas.Count; j++)
            if (!usadas.Contains(j) && EditadaAMano(previas[j])) res.Add(previas[j]);

        res.Sort((a, b) => a.Vertice.CompareTo(b.Vertice));
        var nombres = res.Where(c => c.Nombre != "").Select(c => c.Nombre).ToHashSet();
        for (int i = 0; i < res.Count; i++)
        {
            if (res[i].Nombre != "") continue;
            int k = i + 1;
            while (nombres.Contains($"Curva {k}")) k++;
            res[i].Nombre = $"Curva {k}";
            nombres.Add(res[i].Nombre);
        }
        return res;
    }
}
