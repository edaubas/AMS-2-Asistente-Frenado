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

    /// <param name="conservarNoEmparejadas">
    /// Modo "solo vos": las curvas guardadas que esta sesion no volvio a ver (la IA frena ahi y vos no) se conservan
    /// enteras, sin importar si tienen nombre o nota (sin esto solo sobreviven las editadas a mano), y las que tienen
    /// bloque de la IA conservan su vertice y su tipo: solo se agrega o renueva el bloque "vos".
    /// </param>
    public static List<CurvaGuardada> Fusionar(IReadOnlyList<CurvaEmparejada> nuevas, IReadOnlyList<CurvaGuardada> previas, double largo,
        bool conservarNoEmparejadas = false)
    {
        var usadas = new HashSet<int>();
        var ordenadas = nuevas.OrderBy(c => c.Vertice).ToList();

        // Emparejado global por cercania (el par mas cercano primero), no en el orden de las nuevas:
        // si no, una curva solo tuya (sin IA) que cae a menos de RadioM de una curva ya guardada se
        // la quedaba antes que la curva real de la IA y heredaba su bloque "ia" viejo (curva duplicada).
        var pares = new List<(double dist, int n, int p)>();
        for (int i = 0; i < ordenadas.Count; i++)
            for (int j = 0; j < previas.Count; j++)
            {
                double d = Math.Abs(Geometria.DifCirc(ordenadas[i].Vertice, previas[j].Vertice, largo));
                if (d <= RadioM) pares.Add((d, i, j));
            }
        pares.Sort((a, b) => a.dist.CompareTo(b.dist));
        var previaDe = new int[ordenadas.Count];
        Array.Fill(previaDe, -1);
        foreach (var (_, i, j) in pares)
        {
            if (previaDe[i] >= 0 || usadas.Contains(j)) continue;
            previaDe[i] = j;
            usadas.Add(j);
        }

        var res = new List<CurvaGuardada>();
        for (int i = 0; i < ordenadas.Count; i++)
        {
            var n = ordenadas[i];
            var c = new CurvaGuardada { Vertice = n.Vertice, Tipo = Tipos.ATexto(n.Tipo), Ia = n.Ia, Vos = n.Vos };
            if (previaDe[i] >= 0)
            {
                var p = previas[previaDe[i]];
                c.Nombre = p.Nombre;
                c.Nota = p.Nota;
                c.Extra = p.Extra;
                // esta sesion no vio a la IA (o a vos): se conserva lo grabado antes
                c.Ia ??= p.Ia;
                c.Vos ??= p.Vos;
                // solo vos: la curva sigue siendo la de la IA guardada (mismo vertice y tipo), solo se agrega o renueva tu bloque
                if (conservarNoEmparejadas && n.Ia is null && p.Ia is not null) { c.Vertice = p.Vertice; c.Tipo = p.Tipo; }
            }
            res.Add(c);
        }
        // curvas con nombre o nota puestos a mano que esta vez no aparecieron: no se pierden
        for (int j = 0; j < previas.Count; j++)
            if (!usadas.Contains(j) && (conservarNoEmparejadas || EditadaAMano(previas[j]))) res.Add(previas[j]);

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
