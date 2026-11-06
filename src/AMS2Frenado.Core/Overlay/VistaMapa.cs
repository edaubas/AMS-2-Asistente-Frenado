using AMS2Frenado.Core.Grabacion;

namespace AMS2Frenado.Core.Overlay;

public sealed record PuntoPx(double X, double Y);

/// <summary>Curva en el mini mapa. Frenada = marcador lleno; toque y levanta = anillo vacio (Hueco). IA = rombo, vos = circulo.</summary>
public sealed record MarcadorMapa(FormaMarcador Forma, bool Hueco, string Color, double X, double Y, int IndiceCurva);

/// <summary>Mini mapa ya proyectado a un cuadro de ancho x alto px. Se arma una vez por pista y clase.</summary>
public sealed class VistaMapa
{
    public static readonly VistaMapa Vacio = new(MedidasUi.MapaAncho, MedidasUi.MapaAlto, [], [], [], 0, "", "");

    private readonly double[] _d;
    public double Ancho { get; }
    public double Alto { get; }
    public IReadOnlyList<PuntoPx> Trazo { get; }
    public IReadOnlyList<MarcadorMapa> Marcadores { get; }
    public double Largo { get; }
    public string LeyendaIa { get; }
    public string LeyendaVos { get; }
    public bool EstaVacio => Trazo.Count < 2;

    internal VistaMapa(double ancho, double alto, IReadOnlyList<PuntoPx> trazo, IReadOnlyList<MarcadorMapa> marcadores,
        double[] d, double largo, string leyendaIa, string leyendaVos)
    {
        Ancho = ancho; Alto = alto; Trazo = trazo; Marcadores = marcadores; _d = d; Largo = largo;
        LeyendaIa = leyendaIa; LeyendaVos = leyendaVos;
    }

    /// <summary>Posicion (px) y rumbo (radianes, 0 = hacia la derecha) a una distancia de vuelta. Null si no hay trazo.</summary>
    public (double X, double Y, double Angulo)? Posicion(double distancia)
    {
        if (EstaVacio || Largo <= 0 || !double.IsFinite(distancia)) return null;
        double d = Geometria.Mod(distancia, Largo);
        int n = _d.Length;
        int i = Array.BinarySearch(_d, d);
        if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, n - 1);
        int j = (i + 1) % n;
        double d0 = _d[i], d1 = j == 0 ? Largo + _d[0] : _d[j];
        double span = d1 - d0;
        double f = span <= 1e-9 ? 0 : Math.Clamp((d - d0) / span, 0, 1);
        var a = Trazo[i]; var b = Trazo[j];
        return (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, Math.Atan2(b.Y - a.Y, b.X - a.X));
    }
}

public static class ProyeccionMapa
{
    /// <summary>Separacion del marcador de "vos" respecto del de la IA, perpendicular a la pista (UI-SPEC P11).</summary>
    public const double DesplazamientoVosPx = 6;

    /// <summary>
    /// Proyecta mapa.json (x, z cada 5 m) a un cuadro de ancho x alto, con margen, conservando la proporcion.
    /// Eje horizontal = x del mundo; vertical = -z (z crece hacia adelante, se dibuja hacia arriba). Sin probar en AMS2:
    /// si en el juego el trazo saliera espejado, es un cambio de signo aca.
    /// </summary>
    public static VistaMapa Crear(MapaPista? mapa, DatosClase? datos, double ancho = MedidasUi.MapaAncho,
        double alto = MedidasUi.MapaAlto, double margen = 18)
    {
        if (mapa is null || mapa.Puntos.Count < 2) return VistaMapa.Vacio;
        var pts = mapa.Puntos.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Z) && double.IsFinite(p.D)).OrderBy(p => p.D).ToList();
        if (pts.Count < 2) return VistaMapa.Vacio;

        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
        double minZ = pts.Min(p => -p.Z), maxZ = pts.Max(p => -p.Z);
        double w = Math.Max(maxX - minX, 1e-6), h = Math.Max(maxZ - minZ, 1e-6);
        double esc = Math.Min((ancho - 2 * margen) / w, (alto - 2 * margen) / h);
        double ox = (ancho - w * esc) / 2, oy = (alto - h * esc) / 2;
        PuntoPx Px(PuntoMapa p) => new(ox + (p.X - minX) * esc, oy + (-p.Z - minZ) * esc);

        var trazo = pts.Select(Px).ToList();
        var d = pts.Select(p => p.D).ToArray();
        double largo = mapa.Largo > 0 ? mapa.Largo : (datos?.Largo ?? 0);
        if (largo <= 0) largo = d[^1] + mapa.Paso;

        var vacio = new VistaMapa(ancho, alto, trazo, [], d, largo, "", "");
        var marcadores = new List<MarcadorMapa>();
        int iaVueltas = 0, vosVueltas = 0;
        if (datos is not null)
            for (int i = 0; i < datos.Curvas.Count; i++)
            {
                var c = datos.Curvas[i];
                bool hueco = Tipos.DeTexto(c.Tipo) != TipoCurva.Frenada;
                foreach (var (b, e) in new[] { (c.Ia, EtiquetaRef.Ia), (c.Vos, EtiquetaRef.Vos) })
                {
                    if (b is null) continue;
                    if (e == EtiquetaRef.Ia) iaVueltas = Math.Max(iaVueltas, b.Vueltas); else vosVueltas = Math.Max(vosVueltas, b.Vueltas);
                    var pos = vacio.Posicion(b.FrenarEn);
                    if (pos is null) continue;
                    var (x, y, ang) = pos.Value;
                    if (e == EtiquetaRef.Vos)
                    {
                        x += -Math.Sin(ang) * DesplazamientoVosPx;
                        y += Math.Cos(ang) * DesplazamientoVosPx;
                    }
                    marcadores.Add(new MarcadorMapa(e.Forma, hueco, e.Color, x, y, i));
                }
            }

        return new VistaMapa(ancho, alto, trazo, marcadores, d, largo,
            $"IA · {iaVueltas} {(iaVueltas == 1 ? "vuelta" : "vueltas")}", $"Vos · {vosVueltas} {(vosVueltas == 1 ? "vuelta" : "vueltas")}");
    }
}
