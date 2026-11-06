namespace AMS2Frenado.Core.Grabacion;

/// <summary>Eventos de un (participante, vuelta). La clave identifica a esa vuelta de ese auto.</summary>
public readonly record struct ClaveVuelta(int Participante, int Vuelta);

/// <summary>Resultado de agrupar una fuente (IA o vos): una curva con su bloque.</summary>
public sealed record GrupoCurva(double Vertice, TipoCurva Tipo, BloqueCurva Bloque);

/// <summary>Una curva ya emparejada IA-vos por el vertice.</summary>
public sealed record CurvaEmparejada(double Vertice, TipoCurva Tipo, BloqueCurva? Ia, BloqueCurva? Vos);

public static class Agrupador
{
    public const double ToleranciaVerticeM = 60.0;
    public const double ToleranciaEmparejarM = 80.0;
    public const double FraccionMinima = 0.5;

    /// <summary>
    /// Si un evento de menor prioridad se solapa con uno de mayor prioridad de la misma vuelta
    /// (p. ej. el detector suave tambien ve una frenada fuerte) se descarta el de menor prioridad.
    /// </summary>
    public static List<EventoFrenada> Depurar(IReadOnlyList<EventoFrenada> eventos, double largo, double margenM = 30)
    {
        var res = new List<EventoFrenada>();
        foreach (var e in eventos)
        {
            bool tapado = false;
            foreach (var f in eventos)
            {
                if (f.Prioridad <= e.Prioridad) continue;
                // posicion de e relativa al inicio de f, y largos hasta el vertice
                double rel = Geometria.DifCirc(e.FrenarEn, f.FrenarEn, largo);
                double finE = rel + Geometria.Mod(e.Apice - e.FrenarEn, largo);
                double finF = Geometria.Mod(f.Apice - f.FrenarEn, largo);
                if (rel <= finF + margenM && finE >= -margenM) { tapado = true; break; }
            }
            if (!tapado) res.Add(e);
        }
        return res;
    }

    /// <summary>
    /// Junta eventos de varias vueltas en curvas agrupando por vertice (velocidad minima).
    /// Una curva debe aparecer en al menos la mitad de las vueltas (<paramref name="vueltasTotal"/>).
    /// Usa la mediana (robusta a una frenada rara) y el MAD como dispersion.
    /// </summary>
    public static List<GrupoCurva> Agrupar(
        IEnumerable<KeyValuePair<ClaveVuelta, List<EventoFrenada>>> eventosPorVuelta,
        int vueltasTotal, double largo,
        double tol = ToleranciaVerticeM, double frac = FraccionMinima)
    {
        var res = new List<GrupoCurva>();
        if (vueltasTotal <= 0 || largo <= 0) return res;

        var todos = new List<(double apice, ClaveVuelta clave, EventoFrenada ev)>();
        foreach (var (clave, evs) in eventosPorVuelta)
            foreach (var e in Depurar(evs, largo))
                todos.Add((Geometria.Mod(e.Apice, largo), clave, e));
        todos.Sort((a, b) => a.apice.CompareTo(b.apice));

        var grupos = new List<List<(double apice, ClaveVuelta clave, EventoFrenada ev)>>();
        foreach (var item in todos)
        {
            if (grupos.Count > 0 && item.apice - grupos[^1][^1].apice <= tol) grupos[^1].Add(item);
            else grupos.Add([item]);
        }
        // curva cuyo vertice cae justo del otro lado de la linea de meta
        if (grupos.Count > 1 && grupos[0][0].apice + largo - grupos[^1][^1].apice <= tol)
        {
            grupos[^1].AddRange(grupos[0]);
            grupos.RemoveAt(0);
        }

        int minimo = Math.Max(1, (int)Math.Ceiling(vueltasTotal * frac));
        foreach (var g in grupos)
        {
            if (g.Select(x => x.clave).Distinct().Count() < minimo) continue;
            var evs = g.Select(x => x.ev).ToList();
            double vertice = MedianaCirc(evs.Select(e => e.Apice), largo);
            double frenarEn = MedianaCirc(evs.Select(e => e.FrenarEn), largo);
            double dispM = Mediana(evs.Select(e => Math.Abs(Geometria.DifCirc(e.FrenarEn, frenarEn, largo))));
            double vMinMed = Mediana(evs.Select(e => e.VMin));
            double dispV = Mediana(evs.Select(e => Math.Abs(e.VMin - vMinMed))) * 3.6;
            double v0 = Mediana(evs.Select(e => e.V0));
            var bloque = new BloqueCurva
            {
                FrenarEn = Math.Round(frenarEn, 1),
                LargoFrenada = Math.Round(Mediana(evs.Select(e => e.LargoFrenada)), 1),
                Vertice = Math.Round(vertice, 1),
                VEntradaKmh = (int)Math.Round(v0 * 3.6),
                VVerticeKmh = (int)Math.Round(vMinMed * 3.6),
                Vueltas = g.Select(x => x.clave).Distinct().Count(),
                DispersionM = Math.Round(dispM, 1),
                DispersionVKmh = Math.Round(dispV, 1),
            };
            var tipo = Tipos.Clasificar(Mediana(evs.Select(e => e.Caida)));
            res.Add(new GrupoCurva(Math.Round(vertice, 1), tipo, bloque));
        }
        res.Sort((a, b) => a.Vertice.CompareTo(b.Vertice));
        return res;
    }

    /// <summary>Empareja los grupos de la IA con los tuyos por cercania de vertice (no del punto de frenado).</summary>
    public static List<CurvaEmparejada> Emparejar(
        IReadOnlyList<GrupoCurva> ia, IReadOnlyList<GrupoCurva> vos, double largo, double tol = ToleranciaEmparejarM)
    {
        var pares = new List<(double dist, int i, int j)>();
        for (int i = 0; i < ia.Count; i++)
            for (int j = 0; j < vos.Count; j++)
            {
                double d = Math.Abs(Geometria.DifCirc(ia[i].Vertice, vos[j].Vertice, largo));
                if (d <= tol) pares.Add((d, i, j));
            }
        pares.Sort((a, b) => a.dist.CompareTo(b.dist));
        var usadoIa = new bool[ia.Count];
        var usadoVos = new bool[vos.Count];
        var res = new List<CurvaEmparejada>();
        foreach (var (_, i, j) in pares)
        {
            if (usadoIa[i] || usadoVos[j]) continue;
            usadoIa[i] = usadoVos[j] = true;
            res.Add(new CurvaEmparejada(ia[i].Vertice, ia[i].Tipo, ia[i].Bloque, vos[j].Bloque));
        }
        for (int i = 0; i < ia.Count; i++)
            if (!usadoIa[i]) res.Add(new CurvaEmparejada(ia[i].Vertice, ia[i].Tipo, ia[i].Bloque, null));
        for (int j = 0; j < vos.Count; j++)
            if (!usadoVos[j]) res.Add(new CurvaEmparejada(vos[j].Vertice, vos[j].Tipo, null, vos[j].Bloque));
        res.Sort((a, b) => a.Vertice.CompareTo(b.Vertice));
        return res;
    }

    public static double Mediana(IEnumerable<double> xs)
    {
        var a = xs.OrderBy(x => x).ToArray();
        if (a.Length == 0) return 0;
        int m = a.Length / 2;
        return a.Length % 2 == 1 ? a[m] : (a[m - 1] + a[m]) / 2;
    }

    /// <summary>Mediana de posiciones sobre una pista circular, resultado en [0, largo).</summary>
    public static double MedianaCirc(IEnumerable<double> xs, double largo)
    {
        var l = xs.ToList();
        if (l.Count == 0) return 0;
        double r = l[0];
        return Geometria.Mod(Mediana(l.Select(x => r + Geometria.DifCirc(x, r, largo))), largo);
    }
}
