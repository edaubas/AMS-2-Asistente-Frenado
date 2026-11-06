using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using AMS2Frenado.App.Interop;
using AMS2Frenado.Core.Overlay;

namespace AMS2Frenado.App;

/// <summary>
/// Ventana chica SOLO para los botones (modo, engranaje, pin, cerrar), UI-SPEC 7.4 y 7.11. Es aparte de la ventana grande a
/// proposito: la grande queda click-through cuando esta fijada (WS_EX_TRANSPARENT) y esta no, sin WM_NCHITTEST (HTTRANSPARENT no
/// cruza procesos y AMS2 es otro). Rectangular y SIN AllowsTransparency; NOACTIVATE | TOOLWINDOW para no sacarle el foco al juego;
/// la ventana del overlay es su Owner. Se dibuja segun el <see cref="BarraBotones"/> del Core y se reconstruye solo si cambia el
/// estado. Crece hacia la izquierda y abajo (su borde derecho y su borde superior no se mueven): 292 de ancho con la pregunta
/// de cerrar, 220 con el popover de modo, 32 x 32 con el overlay oculto (la pastilla). Compila, SIN EJECUTAR.
/// </summary>
internal sealed class VentanaBarra : Window
{
    private const double AnchoPopover = 220;

    private readonly Border _raiz = new();
    private readonly ScaleTransform _escala = new(1, 1);
    private readonly RotateTransform _giroPin = new(0);
    private IntPtr _hwnd;
    private string _firma = "";
    private double _anguloPin;
    private Rectangle? _progreso;
    private double _anchoLogico = MedidasUi.BarraAncho, _altoLogico = MedidasUi.BarraAlto, _escalaActual = 1;

    public event Action<BotonBarra>? Pulsado;
    public event Action<ModoApp>? ModoElegido;
    public event Action? CierreConfirmado, CierreCancelado;

    public IntPtr Hwnd => _hwnd;
    public double EscalaBarra => _escalaActual;

    public VentanaBarra(Window dueno)
    {
        Owner = dueno;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; Focusable = false;
        Title = "AMS2 Asistente de Frenado - Botones";
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        Background = Pincel.De(Paleta.Menu);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        _raiz.LayoutTransform = _escala;
        _raiz.Background = Pincel.De(Paleta.Menu);
        _raiz.BorderBrush = Pincel.De(Paleta.Borde);
        _raiz.BorderThickness = new Thickness(1);
        _raiz.HorizontalAlignment = HorizontalAlignment.Left; _raiz.VerticalAlignment = VerticalAlignment.Top;
        Content = _raiz;
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Win32.AplicarEstiloControl(_hwnd);
        };
    }

    /// <summary>
    /// Redibuja si cambio el estado y ajusta la escala (la de la barra: sigue al overlay y debajo de 0,875 se queda en 0,875).
    /// Devuelve verdadero si cambio el tamano logico (hay que reposicionar).
    /// </summary>
    public bool Refrescar(BarraBotones b, double escalaOverlay)
    {
        double bs = MedidasUi.LimitarEscala(escalaOverlay) * MedidasUi.EscalaBarra(escalaOverlay);
        string firma = string.Join("|", b.Fijado, b.Modo, b.MenuAbierto, b.PopoverModoAbierto, b.ConfirmandoCierre, b.Oculto, b.Atajo, b.AvisoCierre);
        bool cambioEscala = Math.Abs(bs - _escalaActual) > 1e-6;
        if (firma == _firma && !cambioEscala) return false;
        _escalaActual = bs;
        _escala.ScaleX = _escala.ScaleY = bs;
        if (firma != _firma) { _firma = firma; Reconstruir(b); }
        Width = _anchoLogico * bs; Height = _altoLogico * bs;
        return true;
    }

    /// <summary>Pone la ventana con su borde derecho en <paramref name="derecha"/> y su borde superior en <paramref name="arriba"/> (DIP de pantalla).</summary>
    public void Posicionar(double derecha, double arriba)
    {
        Left = derecha - Width; Top = arriba;
    }

    /// <summary>Barra fina que se consume mientras dura la pregunta de cerrar (indicador de tiempo, no un pulso).</summary>
    public void ActualizarProgreso(double fraccion)
    {
        if (_progreso is not null) _progreso.Width = Math.Max(0, (_anchoLogico - 2) * Math.Clamp(fraccion, 0, 1));
    }

    public void ReafirmarArriba() { if (_hwnd != IntPtr.Zero) Win32.ReafirmarArriba(_hwnd); }

    // ---------------------------------------------------------------- construccion

    private void Reconstruir(BarraBotones b)
    {
        _progreso = null;
        var contenido = new Grid();
        double alto = MedidasUi.BarraAlto;
        if (b.Oculto)
        {
            _anchoLogico = MedidasUi.BotonClicable; _altoLogico = MedidasUi.BotonClicable;
            contenido.Children.Add(BotonAjustes(b));
        }
        else if (b.ConfirmandoCierre)
        {
            _anchoLogico = MedidasUi.BarraAncha;
            alto = MedidasUi.BarraAlto + (b.AvisoCierre is null ? 0 : 18);
            _altoLogico = alto;
            contenido.Children.Add(FilaConfirmacion(b));
        }
        else
        {
            var pila = new StackPanel();
            var fila = FilaBotones(b);
            pila.Children.Add(fila);
            if (b.PopoverModoAbierto)
            {
                _anchoLogico = AnchoPopover;
                pila.Children.Add(Popover(b));
                _altoLogico = MedidasUi.BarraAlto + 4 + 20 + 3 * 46 + 6;
            }
            else { _anchoLogico = MedidasUi.BarraAncho; _altoLogico = alto; }
            contenido.Children.Add(pila);
        }
        _raiz.Width = _anchoLogico; _raiz.Height = _altoLogico;
        _raiz.Child = contenido;
    }

    private UIElement FilaBotones(BarraBotones b)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Height = MedidasUi.BarraAlto };
        fila.Children.Add(BotonModo(b));
        fila.Children.Add(BotonAjustes(b));
        fila.Children.Add(BotonPin(b));
        fila.Children.Add(new Rectangle { Width = 1, Height = 18, Fill = Pincel.De(Paleta.Borde), Margin = new Thickness(4, 7, 4, 7) });
        fila.Children.Add(BotonCerrar(b));
        return fila;
    }

    // ---- botones

    private Border Boton(UIElement icono, string tooltip, BotonBarra cual, string fondo, string? borde, string fondoHover)
    {
        var pincel = new SolidColorBrush(Pincel.Color(fondo));
        var visible = new Border
        {
            Width = MedidasUi.BotonVisible, Height = MedidasUi.BotonVisible, Background = pincel,
            BorderBrush = borde is null ? null : Pincel.De(borde), BorderThickness = new Thickness(borde is null ? 0 : 1),
            Child = icono, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        if (icono is FrameworkElement fe) { fe.HorizontalAlignment = HorizontalAlignment.Center; fe.VerticalAlignment = VerticalAlignment.Center; }
        // 28 visibles dentro de un area clicable de 32 x 32 (UI-SPEC 7.4)
        var area = new Border { Width = MedidasUi.BotonClicable, Height = MedidasUi.BotonClicable, Background = Brushes.Transparent, Child = visible, ToolTip = tooltip, Focusable = false };
        Color Hacia(string hex) => Pincel.Color(hex);
        void Pintar(string hex) => pincel.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Hacia(hex), Estilo.Dur(80)));
        bool abajo = false;
        area.MouseEnter += (_, _) => Pintar(fondoHover);
        area.MouseLeave += (_, _) => { abajo = false; Pintar(fondo); };
        area.MouseLeftButtonDown += (_, e) => { abajo = true; Pintar(Paleta.SupPresionado); area.CaptureMouse(); e.Handled = true; };
        area.MouseLeftButtonUp += (_, e) =>
        {
            area.ReleaseMouseCapture();
            bool dentro = abajo && area.IsMouseOver;
            abajo = false; Pintar(area.IsMouseOver ? fondoHover : fondo);
            e.Handled = true;
            if (dentro) Pulsado?.Invoke(cual);
        };
        return area;
    }

    private UIElement BotonModo(BarraBotones b)
        => Boton(Estilo.Icono(Estilo.Flechas, Paleta.Apoyo), b.TooltipModo, BotonBarra.Modo,
            b.PopoverModoAbierto ? Paleta.SupHover : Paleta.Menu, null, Paleta.SupHover);

    private UIElement BotonAjustes(BarraBotones b)
        => Boton(Estilo.Icono(Estilo.Engranaje, Paleta.Secundario, relleno: true), b.TooltipAjustes, BotonBarra.Ajustes,
            b.MenuAbierto ? Paleta.SupHover : Paleta.Sup, Paleta.BordeBoton, Paleta.SupHover);

    private UIElement BotonPin(BarraBotones b)
    {
        // fijado: chincheta vertical rellena, fondo rojo oscuro y borde rojo; libre: inclinada 45 grados y solo contorno
        var icono = Estilo.Icono(Estilo.Chincheta, b.Fijado ? Paleta.RojoEtiqueta : Paleta.Secundario, relleno: b.Fijado, trazo: 1.4);
        icono.RenderTransformOrigin = new Point(0.5, 0.5);
        icono.RenderTransform = _giroPin;
        double destino = b.PinAnguloGrados;
        if (Math.Abs(_anguloPin - destino) < 0.01) _giroPin.Angle = destino;
        else
        {
            _giroPin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(_anguloPin, destino, Estilo.Dur(120)));
            _anguloPin = destino;
        }
        return Boton(icono, b.TooltipPin, BotonBarra.Fijar,
            b.Fijado ? Paleta.PinFijadoFondo : Paleta.Sup, b.Fijado ? Paleta.RojoFranja : Paleta.BordeBoton, b.Fijado ? "#4A1F1C" : Paleta.SupHover);
    }

    private UIElement BotonCerrar(BarraBotones b)
        => Boton(Estilo.Icono(Estilo.Equis, Paleta.Apoyo), b.TooltipCerrar, BotonBarra.Cerrar, Paleta.Menu, null, Paleta.RojoFranja);

    // ---- pregunta de cerrar

    private UIElement FilaConfirmacion(BarraBotones b)
    {
        var raiz = new Grid();
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MedidasUi.BarraAlto) });
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var fila = new Grid { Margin = new Thickness(8, 0, 2, 0) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var pregunta = new TextBlock
        {
            Text = TextosBarra.PreguntaCerrar.ToUpperInvariant(), VerticalAlignment = VerticalAlignment.Center,
            FontFamily = (FontFamily)Application.Current.FindResource("Barlow"), FontSize = 16, FontWeight = FontWeights.SemiBold,
            Foreground = Pincel.De(Paleta.Blanco), TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(pregunta, 0);
        var cerrar = BotonTexto(TextosBarra.Cerrar, Paleta.RojoFranja, null, Paleta.RojoHover, Paleta.Blanco, 84, () => CierreConfirmado?.Invoke());
        cerrar.Margin = new Thickness(0, 0, 6, 0);
        Grid.SetColumn(cerrar, 1);
        // CANCELAR queda justo donde estaba la X: un doble clic accidental cancela
        var cancelar = BotonTexto(TextosBarra.Cancelar, Paleta.Sup, Paleta.BordeBoton, Paleta.SupHover, Paleta.Titulo, 92, () => CierreCancelado?.Invoke());
        Grid.SetColumn(cancelar, 2);
        fila.Children.Add(pregunta); fila.Children.Add(cerrar); fila.Children.Add(cancelar);
        Grid.SetRow(fila, 0);
        raiz.Children.Add(fila);
        if (b.AvisoCierre is { } aviso)
        {
            var t = new TextBlock
            {
                Text = aviso, Margin = new Thickness(8, 0, 8, 2), FontFamily = (FontFamily)Application.Current.FindResource("Inter"),
                FontSize = 12, Foreground = Pincel.De(Paleta.RojoEtiqueta), TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetRow(t, 1);
            raiz.Children.Add(t);
        }
        _progreso = new Rectangle { Height = 2, Fill = Pincel.De(Paleta.RojoFranja), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Width = _anchoLogico - 2 };
        Grid.SetRowSpan(_progreso, 2);
        raiz.Children.Add(_progreso);
        return raiz;
    }

    private Border BotonTexto(string texto, string fondo, string? borde, string fondoHover, string colorTexto, double ancho, Action alPulsar)
    {
        var pincel = new SolidColorBrush(Pincel.Color(fondo));
        var b = new Border
        {
            Width = ancho, Height = MedidasUi.BotonVisible, Background = pincel, Focusable = false,
            BorderBrush = borde is null ? null : Pincel.De(borde), BorderThickness = new Thickness(borde is null ? 0 : 1),
            Child = new TextBlock
            {
                Text = texto, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)Application.Current.FindResource("Inter"), FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = Pincel.De(colorTexto), IsHitTestVisible = false,
            },
        };
        void Pintar(string hex) => pincel.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Pincel.Color(hex), Estilo.Dur(80)));
        bool abajo = false;
        b.MouseEnter += (_, _) => Pintar(fondoHover);
        b.MouseLeave += (_, _) => { abajo = false; Pintar(fondo); };
        b.MouseLeftButtonDown += (_, e) => { abajo = true; b.CaptureMouse(); e.Handled = true; };
        b.MouseLeftButtonUp += (_, e) =>
        {
            b.ReleaseMouseCapture();
            bool dentro = abajo && b.IsMouseOver; abajo = false; e.Handled = true;
            if (dentro) alPulsar();
        };
        return b;
    }

    // ---- popover de modo: tres opciones, la actual marcada (no cicla)

    private UIElement Popover(BarraBotones b)
    {
        var pila = new StackPanel { Margin = new Thickness(6, 4, 6, 6) };
        pila.Children.Add(new TextBlock
        {
            Text = "MODO", Height = 20, FontFamily = (FontFamily)Application.Current.FindResource("Inter"), FontSize = 12,
            FontWeight = FontWeights.Bold, Foreground = Pincel.De(Paleta.RojoEtiqueta),
        });
        foreach (var (modo, nombre, ayuda, actual) in b.OpcionesModo)
        {
            var m = modo;
            var fondo = new SolidColorBrush(Pincel.Color(actual ? Paleta.Blanco : Paleta.Sup));
            var fila = new Border { Height = 44, Margin = new Thickness(0, 0, 0, 2), Background = fondo, Padding = new Thickness(10, 4, 8, 4), Focusable = false };
            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textos.Children.Add(new TextBlock
            {
                Text = (actual ? "✓ " : "") + nombre, FontFamily = (FontFamily)Application.Current.FindResource("Inter"), FontSize = 14,
                FontWeight = FontWeights.SemiBold, Foreground = Pincel.De(actual ? Paleta.Fondo : Paleta.Titulo),
            });
            textos.Children.Add(new TextBlock
            {
                Text = ayuda, FontFamily = (FontFamily)Application.Current.FindResource("Inter"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Pincel.De(actual ? "#3B4652" : Paleta.Apoyo),
            });
            fila.Child = textos;
            if (!actual)
            {
                fila.MouseEnter += (_, _) => fondo.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Pincel.Color(Paleta.SupHover), Estilo.Dur(80)));
                fila.MouseLeave += (_, _) => fondo.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Pincel.Color(Paleta.Sup), Estilo.Dur(80)));
            }
            fila.MouseLeftButtonUp += (_, e) => { e.Handled = true; ModoElegido?.Invoke(m); };
            pila.Children.Add(fila);
        }
        return pila;
    }
}
