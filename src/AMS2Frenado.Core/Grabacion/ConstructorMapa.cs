namespace AMS2Frenado.Core.Grabacion;

/// <summary>
/// Arma el trazado de la pista: un punto cada 5 m de distancia de vuelta con x,z de mWorldPosition.
/// Cada auto llena un <see cref="BufferVuelta"/> y, si su vuelta resulta valida, se suma al mapa;
/// el punto final de cada tramo es la mediana de las vueltas (robusta a una salida de pista).
/// </summary>
public sealed class ConstructorMapa
{
    public const double PasoM = 5.0;
    private const int MaxMuestrasPorPunto = 31;

    private readonly double _largo;
    private readonly List<float>[] _xs, _zs;

    public int Vueltas { get; private set; }
    public int Tramos { get; }

    public ConstructorMapa(double largo)
    {
        _largo = largo;
        Tramos = Math.Max(1, (int)Math.Ceiling(largo / PasoM));
        _xs = new List<float>[Tramos];
        _zs = new List<float>[Tramos];
    }

    /// <summary>Lo que junta un auto durante su vuelta actual (primera muestra de cada tramo).</summary>
    public sealed class BufferVuelta
    {
        internal readonly float[] X, Z;
        internal readonly bool[] Lleno;
        internal BufferVuelta(int n) { X = new float[n]; Z = new float[n]; Lleno = new bool[n]; }
        public void Limpiar() => Array.Clear(Lleno);
    }

    public BufferVuelta NuevoBuffer() => new(Tramos);

    public void Agregar(BufferVuelta b, double dist, float x, float z)
    {
        if (!float.IsFinite(x) || !float.IsFinite(z) || dist < 0 || dist >= _largo) return;
        int i = Math.Min(Tramos - 1, (int)(dist / PasoM));
        if (b.Lleno[i]) return;
        b.Lleno[i] = true; b.X[i] = x; b.Z[i] = z;
    }

    /// <summary>Suma una vuelta valida completa. Se ignora si cubre menos del 90% de los tramos.</summary>
    public bool Confirmar(BufferVuelta b)
    {
        int llenos = 0;
        for (int i = 0; i < Tramos; i++) if (b.Lleno[i]) llenos++;
        if (llenos < Tramos * 0.9) return false;
        for (int i = 0; i < Tramos; i++)
        {
            if (!b.Lleno[i]) continue;
            var lx = _xs[i] ??= [];
            var lz = _zs[i] ??= [];
            if (lx.Count >= MaxMuestrasPorPunto) continue;
            lx.Add(b.X[i]); lz.Add(b.Z[i]);
        }
        Vueltas++;
        return true;
    }

    public MapaPista? Construir(string pista, string variante)
    {
        if (Vueltas == 0) return null;
        var mapa = new MapaPista { Pista = pista, Variante = variante, Largo = Math.Round(_largo, 1), Paso = PasoM, Vueltas = Vueltas };
        var med = new (double x, double z)?[Tramos];
        for (int i = 0; i < Tramos; i++)
            if (_xs[i] is { Count: > 0 })
                med[i] = (Agrupador.Mediana(_xs[i].Select(v => (double)v)), Agrupador.Mediana(_zs[i].Select(v => (double)v)));
        for (int i = 0; i < Tramos; i++)
        {
            var p = med[i] ?? Interpolar(med, i);
            if (p is null) continue;
            mapa.Puntos.Add(new PuntoMapa { D = i * PasoM, X = Math.Round(p.Value.x, 2), Z = Math.Round(p.Value.z, 2) });
        }
        return mapa;
    }

    // tramo sin datos: interpola entre los vecinos mas cercanos (hasta 6 tramos = 30 m a cada lado)
    private (double x, double z)? Interpolar((double x, double z)?[] med, int i)
    {
        for (int a = 1; a <= 6; a++)
        {
            var prev = med[(i - a + Tramos) % Tramos];
            if (prev is null) continue;
            for (int b = 1; b <= 6; b++)
            {
                var next = med[(i + b) % Tramos];
                if (next is null) continue;
                double f = a / (double)(a + b);
                return (prev.Value.x + f * (next.Value.x - prev.Value.x), prev.Value.z + f * (next.Value.z - prev.Value.z));
            }
        }
        return null;
    }
}
