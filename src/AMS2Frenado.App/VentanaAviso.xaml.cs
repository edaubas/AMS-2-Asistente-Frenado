using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Trazado = System.Windows.Shapes.Path;
using AMS2Frenado.App.Interop;
using AMS2Frenado.Core.Overlay;

namespace AMS2Frenado.App;

/// <summary>
/// Ventana aparte del aviso de frenada: el simbolo de freno (circulo con "!" y dos arcos a los costados), dibujado como
/// vector propio (no es ningun logo ni imagen de terceros). Sin bordes, siempre arriba; bloqueada es click-through;
/// desbloqueada se mueve (arrastrar), se redimensiona (rueda o agarre, 50 a 300 %). No decide cuando mostrarse: pinta la
/// opacidad que le da el <see cref="AvisoFrenada"/> del Core. Compila, SIN EJECUTAR: nunca se abrio en Windows.
/// </summary>
public partial class VentanaAviso : Window
{
    // Geometria en un cuadro de 120 x 120 (centro 60,60). Anillo r=30; arcos r=47 de -40 a +40 grados; "!" y punto.
    private const string Anillo = "M 90,60 A 30,30 0 1 1 30,60 A 30,30 0 1 1 90,60 Z";
    private const string ArcoDerecho = "M 96,29.8 A 47,47 0 0 1 96,90.2";
    private const string ArcoIzquierdo = "M 24,90.2 A 47,47 0 0 1 24,29.8";
    private const string Palo = "M 60,43 L 60,65";
    private const double Trazo = 7, Contornito = 2;     // trazo rojo 7 px; contorno oscuro de 2 px por lado

    private IntPtr _hwnd;
    private bool _bloqueada = true;
    private double _escala = 1;
    private double _opacidad;

    public event Action? Movida;
    public event Action<double>? EscalaCambiada;

    public IntPtr Hwnd => _hwnd;

    public VentanaAviso()
    {
        InitializeComponent();
        ConstruirSimbolo();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Win32.AplicarEstiloOverlay(_hwnd, _bloqueada);
        };
    }

    private void ConstruirSimbolo()
    {
        var rojo = Pincel.De(Paleta.AvisoRojo);
        var oscuro = Pincel.De(Paleta.AvisoContorno);
        var trazos = new[] { Anillo, ArcoDerecho, ArcoIzquierdo, Palo };
        var punto = new EllipseGeometry(new Point(60, 77), 4.2, 4.2);
        punto.Freeze();

        // primero todos los contornos oscuros y despues todo el rojo: ningun contorno tapa el rojo de otra pieza
        foreach (var g in trazos) Simbolo.Children.Add(Linea(g, oscuro, Trazo + 2 * Contornito));
        Simbolo.Children.Add(new Trazado { Data = Agrandar(punto, Contornito), Fill = oscuro });
        foreach (var g in trazos) Simbolo.Children.Add(Linea(g, rojo, Trazo));
        Simbolo.Children.Add(new Trazado { Data = punto, Fill = rojo });
    }

    private static Geometry Agrandar(EllipseGeometry e, double d)
    {
        var g = new EllipseGeometry(e.Center, e.RadiusX + d, e.RadiusY + d);
        g.Freeze();
        return g;
    }

    private static Trazado Linea(string datos, Brush pincel, double grosor)
    {
        var g = Geometry.Parse(datos);
        g.Freeze();
        return new Trazado
        {
            Data = g, Stroke = pincel, StrokeThickness = grosor, Fill = Brushes.Transparent,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
        };
    }

    // ---------------------------------------------------------------- bloqueo, escala, dibujo

    public void AplicarBloqueo(bool bloqueada)
    {
        _bloqueada = bloqueada;
        if (_hwnd != IntPtr.Zero) Win32.AplicarEstiloOverlay(_hwnd, bloqueada);
        var v = bloqueada ? Visibility.Collapsed : Visibility.Visible;
        FondoEdicion.Visibility = v; Contorno.Visibility = v; Chip.Visibility = v; Agarre.Visibility = v;
        Pintar(_opacidad);
    }

    public void AplicarEscala(double escala)
    {
        _escala = MedidasAviso.LimitarEscala(escala);
        Width = Height = MedidasAviso.LadoDeEscala(_escala);
    }

    public double Escala => _escala;

    /// <summary>Opacidad del simbolo (0..1) que da el Core. Desbloqueada se ve siempre, al menos a media opacidad, para poder ubicarla.</summary>
    public void Pintar(double opacidad)
    {
        _opacidad = opacidad;
        Simbolo.Opacity = _bloqueada ? opacidad : Math.Max(opacidad, 0.55);
    }

    public void ReafirmarArriba()
    {
        if (_hwnd != IntPtr.Zero) Win32.ReafirmarArriba(_hwnd);
    }

    // ---------------------------------------------------------------- mover y redimensionar

    private void AlPulsarMouse(object sender, MouseButtonEventArgs e)
    {
        if (_bloqueada || e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); } catch (InvalidOperationException) { return; }
        Movida?.Invoke();
    }

    private void AlRueda(object sender, MouseWheelEventArgs e)
    {
        if (_bloqueada) return;
        double nueva = MedidasAviso.LimitarEscala(_escala + (e.Delta > 0 ? 0.1 : -0.1));
        AplicarEscala(nueva);
        EscalaCambiada?.Invoke(nueva);
    }

    private void AlArrastrarAgarre(object sender, DragDeltaEventArgs e)
        => AplicarEscala(MedidasAviso.EscalaDeLado(Width + e.HorizontalChange));

    private void AlSoltarAgarre(object sender, DragCompletedEventArgs e) => EscalaCambiada?.Invoke(_escala);
}
