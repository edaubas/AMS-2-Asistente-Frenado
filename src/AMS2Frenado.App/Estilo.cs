using System.Windows;
using System.Windows.Media;
using AMS2Frenado.Core.Overlay;
using Trazado = System.Windows.Shapes.Path;

namespace AMS2Frenado.App;

/// <summary>
/// Recursos graficos compartidos de la fase 6 (UI-SPEC 7.2 y 7.11): la textura "carbono" (un unico DrawingBrush congelado,
/// tile de 6 x 6 DIP con una diagonal blanca al 4 %) y la geometria de la esquina cortada. Compila, SIN EJECUTAR.
/// </summary>
internal static class Estilo
{
    /// <summary>Textura de carbono: diagonal de 1 px mas dos puntas para que el tile cierre sin costura. Se comparte en todas las tarjetas.</summary>
    public static readonly DrawingBrush Carbono = CrearCarbono();

    private static DrawingBrush CrearCarbono()
    {
        double l = MedidasUi.TexturaLado;
        var pluma = new Pen(new SolidColorBrush(Color.FromArgb((byte)Math.Round(MedidasUi.TexturaAlfa * 255), 255, 255, 255)), 1);
        pluma.Freeze();
        var geo = Geometry.Parse("M0,6 L6,0 M-1,1 L1,-1 M5,7 L7,5");
        geo.Freeze();
        var dibujo = new GeometryDrawing(Brushes.Transparent, pluma, geo);
        var fondo = new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, l, l)));
        var grupo = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(0, 0, l, l)) };
        grupo.Children.Add(fondo);
        grupo.Children.Add(dibujo);
        grupo.Freeze();
        var pincel = new DrawingBrush(grupo)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, l, l),
            ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, l, l),
            Stretch = Stretch.None,
        };
        RenderOptions.SetCachingHint(pincel, CachingHint.Cache);
        pincel.Freeze();
        return pincel;
    }

    /// <summary>Rectangulo con la esquina de abajo a la derecha cortada a 45 grados.</summary>
    public static StreamGeometry EsquinaCortada(double ancho, double alto, double corte)
    {
        corte = Math.Min(corte, Math.Min(ancho, alto));
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
            c.LineTo(new Point(ancho, 0), true, false);
            c.LineTo(new Point(ancho, alto - corte), true, false);
            c.LineTo(new Point(ancho - corte, alto), true, false);
            c.LineTo(new Point(0, alto), true, false);
        }
        g.Freeze();
        return g;
    }

    /// <summary>Pincel de fondo opaco o con el alfa del overlay (el texto no se atenua: el alfa va solo en el pincel).</summary>
    public static Brush Fondo(string hex, double alfa) => Pincel.De(hex, alfa);

    // ---- iconos vectoriales propios de 16 x 16 (sin imagenes de terceros)

    public static Geometry Flechas { get; } = Congelar(Geometry.Parse("M2,5 H13 M10,2 L13,5 L10,8 M14,11 H3 M6,8 L3,11 L6,14"));
    public static Geometry Equis { get; } = Congelar(Geometry.Parse("M3.5,3.5 L12.5,12.5 M12.5,3.5 L3.5,12.5"));
    /// <summary>Chincheta vertical (cabeza, cuerpo y aguja). Se dibuja rellena (fijado) o solo con contorno (libre).</summary>
    public static Geometry Chincheta { get; } = Congelar(Geometry.Parse("M5,2 H11 L10,4.5 V8 L12,10 V11.5 H8.6 V15 H7.4 V11.5 H4 V10 L6,8 V4.5 Z"));
    public static Geometry Tilde { get; } = Congelar(Geometry.Parse("M3,8.5 L6.5,12 L13,4.5"));
    public static Geometry Volver { get; } = Congelar(Geometry.Parse("M10,2.5 L4.5,8 L10,13.5"));
    public static Geometry Flecha { get; } = Congelar(Geometry.Parse("M6,2.5 L11.5,8 L6,13.5"));
    public static Geometry Carpeta { get; } = Congelar(Geometry.Parse("M1.5,4 H6 L7.5,5.5 H14.5 V13 H1.5 Z"));

    /// <summary>Engranaje: 8 dientes y un agujero central (relleno EvenOdd).</summary>
    public static Geometry Engranaje { get; } = CrearEngranaje();

    private static Geometry CrearEngranaje()
    {
        const double cx = 8, cy = 8, rExt = 7.2, rInt = 5.3, rHueco = 2.4;
        var g = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var c = g.Open())
        {
            // 8 dientes: cada uno son 4 puntos (sube, meseta, baja, valle)
            var puntos = new List<Point>();
            for (int i = 0; i < 8; i++)
            {
                double a0 = i * Math.PI / 4;
                foreach (var (da, r) in new[] { (-0.30, rInt), (-0.17, rExt), (0.17, rExt), (0.30, rInt) })
                    puntos.Add(new Point(cx + r * Math.Cos(a0 + da), cy + r * Math.Sin(a0 + da)));
            }
            c.BeginFigure(puntos[0], true, true);
            foreach (var p in puntos.Skip(1)) c.LineTo(p, true, false);
            c.BeginFigure(new Point(cx + rHueco, cy), true, true);
            c.ArcTo(new Point(cx - rHueco, cy), new Size(rHueco, rHueco), 0, false, SweepDirection.Clockwise, true, false);
            c.ArcTo(new Point(cx + rHueco, cy), new Size(rHueco, rHueco), 0, false, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    private static Geometry Congelar(Geometry g) { g.Freeze(); return g; }

    /// <summary>Icono de 16 x 16 como Path (trazo o relleno).</summary>
    public static Trazado Icono(Geometry g, string color, bool relleno = false, double trazo = 1.6)
    {
        var b = Pincel.De(color);
        return new Trazado
        {
            Data = g, Width = 16, Height = 16, Stretch = Stretch.None,
            Stroke = relleno ? null : b, StrokeThickness = trazo,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            Fill = relleno ? b : Brushes.Transparent, IsHitTestVisible = false,
        };
    }

    /// <summary>Texto en mayusculas con un hair space entre letras: aproxima el espaciado de los titulos (WPF no tiene espaciado de letras).</summary>
    public static string Espaciado(string texto)
        => string.Join(' ', texto.ToUpperInvariant().Select(c => c.ToString()));

    /// <summary>Animaciones habilitadas en Windows: si no, todas las duraciones valen 0 (UI-SPEC 7.7).</summary>
    public static bool Animaciones => SystemParameters.ClientAreaAnimation;
    public static Duration Dur(int ms) => new(TimeSpan.FromMilliseconds(Animaciones ? ms : 0));
}
