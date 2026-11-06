using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using AMS2Frenado.App.Interop;
using AMS2Frenado.Core.Overlay;

namespace AMS2Frenado.App;

/// <summary>
/// Overlay: ventana sin bordes, siempre arriba y semitransparente que solo dibuja lo que le dicen el
/// <see cref="VistaOverlay"/> y el <see cref="CuadroUi"/> del Core (no tiene logica de estados). Fase 6: tarjetas de carbono
/// con esquina cortada, cabecera ARRIBA (pista, clase, modo, FIJO / LIBRE y la tecla) y barra segmentada. Los botones
/// (modo, engranaje, pin, cerrar) NO estan aca: son la ventana <see cref="VentanaBarra"/>, que se posiciona sobre
/// <see cref="RectBarra"/>. Bloqueada, esta ventana conserva el click-through de la 5c (WS_EX_TRANSPARENT).
/// Compila, SIN EJECUTAR: nunca se abrio en Windows.
/// </summary>
public partial class VentanaOverlay : Window
{
    private IntPtr _hwnd;
    private bool _bloqueada = true;
    private double _escala = 1;
    private double _alfa = MedidasUi.AlfaPorDefecto;

    private string _firmaPanel = "";
    private double _anchoCifras;
    private bool _contenidoVisible = true;

    /// <summary>El usuario termino de mover la ventana (DragMove).</summary>
    public event Action? Movida;
    /// <summary>El usuario cambio la escala con la rueda o con el agarre.</summary>
    public event Action<double>? EscalaCambiada;

    public bool Desbloqueada => !_bloqueada;
    public IntPtr Hwnd => _hwnd;

    public VentanaOverlay()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Win32.AplicarEstiloOverlay(_hwnd, _bloqueada);
        };
        Loaded += (_, _) => MedirCifras();
    }

    /// <summary>Reserva el ancho de "000" para que el cartel no mueva el layout al cambiar de cifras.</summary>
    private void MedirCifras()
    {
        try
        {
            var tf = new Typeface(TxtNumero.FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var ft = new FormattedText("000", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 64,
                Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            _anchoCifras = Math.Ceiling(ft.WidthIncludingTrailingWhitespace);
            TxtNumero.MinWidth = _anchoCifras;
        }
        catch { /* si la fuente no mide, queda sin ancho minimo */ }
    }

    // ---------------------------------------------------------------- bloqueo, escala, posicion

    /// <summary>Fija o libera: estilo click-through de la ventana grande, contorno y agarre, chip LIBRE y la etiqueta FIJO / LIBRE.</summary>
    public void AplicarBloqueo(bool bloqueada, BarraBotones barra)
    {
        _bloqueada = bloqueada;
        if (_hwnd != IntPtr.Zero) Win32.AplicarEstiloOverlay(_hwnd, bloqueada);
        ChipLibre.Visibility = barra.MostrarChipLibre ? Visibility.Visible : Visibility.Collapsed;
        TxtChipLibre.Text = barra.TextoChipLibre;
        Contorno.Visibility = bloqueada ? Visibility.Collapsed : Visibility.Visible;
        Agarre.Visibility = bloqueada ? Visibility.Collapsed : Visibility.Visible;
        TxtEstadoPin.Text = barra.EtiquetaPin;
        TxtEstadoPin.Foreground = Pincel.De(bloqueada ? Paleta.RojoEtiqueta : Paleta.ChipDesbloqueadoTexto);
        TxtTecla.Text = barra.Atajo;
        _firmaPanel = "";      // fuerza repintar con la opacidad nueva
    }

    /// <summary>"Ocultar overlay": se esconde el contenido pero NO la ventana, para que la barra (su Owner) pueda dejar la pastilla del engranaje.</summary>
    public void AplicarVisible(bool visible)
    {
        _contenidoVisible = visible;
        Raiz.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Rectangulo (DIP de pantalla) donde va la ventana de botones: el hueco ReservaBarra de la cabecera, ya con la escala del overlay.
    /// Null mientras el contenido esta oculto (se usa el ultimo conocido). Sin ejecutar.
    /// </summary>
    public Rect? RectBarra()
    {
        if (!_contenidoVisible || !IsVisible || !ReservaBarra.IsLoaded) return null;
        try
        {
            var r = ReservaBarra.TransformToAncestor(this).TransformBounds(new Rect(0, 0, ReservaBarra.ActualWidth, ReservaBarra.ActualHeight));
            return new Rect(Left + r.X, Top + r.Y, r.Width, r.Height);
        }
        catch (InvalidOperationException) { return null; }
    }

    public void AplicarEscala(double escala)
    {
        _escala = MedidasUi.LimitarEscala(escala);
        Escala.ScaleX = Escala.ScaleY = _escala;
    }

    public void ReafirmarArriba()
    {
        if (_hwnd != IntPtr.Zero) Win32.ReafirmarArriba(_hwnd);
    }

    private void AlPulsarMouse(object sender, MouseButtonEventArgs e)
    {
        if (_bloqueada || e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); } catch (InvalidOperationException) { return; }
        Movida?.Invoke();
    }

    private void AlRueda(object sender, MouseWheelEventArgs e)
    {
        if (_bloqueada) return;
        double nueva = MedidasUi.LimitarEscala(_escala + (e.Delta > 0 ? 0.05 : -0.05));
        AplicarEscala(nueva);
        EscalaCambiada?.Invoke(nueva);
    }

    private void AlArrastrarAgarre(object sender, DragDeltaEventArgs e)
    {
        // el desplazamiento llega en unidades de la ventana sin escalar: se pasa al ancho real
        double nueva = MedidasUi.LimitarEscala(_escala * (1 + e.HorizontalChange / MedidasUi.AnchoBase));
        AplicarEscala(nueva);
    }

    private void AlSoltarAgarre(object sender, DragCompletedEventArgs e) => EscalaCambiada?.Invoke(_escala);

    // ---------------------------------------------------------------- dibujo

    public void Pintar(VistaOverlay v, CuadroUi c, ConfigApp cfg)
    {
        _alfa = cfg.Opacidad;
        double aPanel = MedidasUi.AlfaPanel(cfg.Opacidad, !_bloqueada);
        // el alfa va solo en los pinceles de fondo (y de la textura): el texto queda al 100 %
        Tarjeta.Alfa = aPanel;
        TarjetaCabecera.Alfa = aPanel;

        PintarTarjeta(c);
        PintarCabecera(v);
    }

    private void PintarTarjeta(CuadroUi c)
    {
        var p = c.Panel;
        Tarjeta.Visibility = p.Tipo == TipoTarjeta.Ninguna ? Visibility.Collapsed : Visibility.Visible;
        Tarjeta.Opacity = c.OpacidadTarjeta;
        Franja.Opacity = c.OpacidadFranja;

        string firma = p.Firma;
        if (firma == _firmaPanel) return;
        _firmaPanel = firma;

        bool guia = p.Tipo == TipoTarjeta.Guia;
        bool aproximando = guia && p.TextoFranja is null;

        // franja
        Franja.Visibility = p.TextoFranja is null ? Visibility.Collapsed : Visibility.Visible;
        if (p.TextoFranja is not null)
        {
            Franja.Background = Pincel.De(p.ColorFranja ?? Paleta.FranjaNeutra);
            TxtFranja.Text = Estilo.Espaciado(p.TextoFranja);
            TxtFranja.Foreground = Pincel.De(p.ColorTextoFranja ?? Paleta.Blanco);
            TxtFranjaCurva.Text = p.CurvaTxt ?? "";
            TxtFranjaCurva.Foreground = Pincel.De(Paleta.Titulo);
        }
        // encabezado: curva + velocidades entrada -> vertice (en FRENA la curva ya esta en la franja)
        bool hayVel = guia && p.VelocidadesTxt is not null;
        FilaEncabezado.Visibility = aproximando || hayVel ? Visibility.Visible : Visibility.Collapsed;
        TxtCurva.Visibility = p.TextoFranja is null ? Visibility.Visible : Visibility.Collapsed;
        TxtCurva.Text = p.CurvaTxt ?? "";
        AcentoCurva.Visibility = aproximando ? Visibility.Visible : Visibility.Collapsed;
        TxtVelocidades.Text = p.VelocidadesTxt ?? "";

        PanelEtiquetas.Visibility = guia ? Visibility.Visible : Visibility.Collapsed;
        if (guia) ReponerEtiquetas(PanelEtiquetas, p.EtiquetasPrincipal);
        bool hayNumero = guia && p.NumeroPrincipal is not null;
        FilaNumero.Visibility = hayNumero ? Visibility.Visible : Visibility.Collapsed;
        if (hayNumero)
        {
            TxtNumero.Text = p.NumeroPrincipal ?? "";
            TxtUnidad.Text = p.UnidadPrincipal ?? "";
            bool sec = p.NumeroSecundario is not null;
            Secundario.Visibility = sec ? Visibility.Visible : Visibility.Collapsed;
            ReponerEtiquetas(PanelEtiquetaSec, p.EtiquetaSecundaria is null ? [] : [p.EtiquetaSecundaria]);
            TxtNumeroSec.Text = p.NumeroSecundario ?? "";
            TxtUnidadSec.Text = p.UnidadSecundaria ?? "";
        }

        CanvasRegla.Visibility = p.MostrarRegla ? Visibility.Visible : Visibility.Collapsed;
        if (p.MostrarRegla) DibujarRegla(p);
        TxtMetros.Visibility = guia && p.Metros is not null ? Visibility.Visible : Visibility.Collapsed;
        TxtMetros.Text = p.Metros ?? "";
        TxtMetros.Foreground = Pincel.De(p.ColorMetros);

        FilaLejos.Visibility = p.Tipo == TipoTarjeta.Lejos ? Visibility.Visible : Visibility.Collapsed;
        if (p.Tipo == TipoTarjeta.Lejos)
        {
            TxtLejosEtiqueta.Text = p.Linea1 ?? "";
            TxtLejosCurva.Text = p.CurvaTxt ?? "";
            TxtLejosMetros.Text = " · " + p.Metros;
        }

        PanelAviso.Visibility = p.Tipo == TipoTarjeta.SinDatos ? Visibility.Visible : Visibility.Collapsed;
        if (p.Tipo == TipoTarjeta.SinDatos)
        {
            TxtTitulo.Text = Estilo.Espaciado(p.Titulo ?? "");
            TxtTitulo.Foreground = Pincel.De(p.ColorTitulo);
            TxtLinea1.Text = p.Linea1 ?? "";
            TxtLinea2.Text = p.Linea2 ?? "";
            TxtLinea2.Visibility = string.IsNullOrEmpty(p.Linea2) ? Visibility.Collapsed : Visibility.Visible;
        }

        PanelContadores.Visibility = p.Tipo == TipoTarjeta.Grabando ? Visibility.Visible : Visibility.Collapsed;
        if (p.Contadores is { } k)
        {
            TxtEncabContadores.Text = k.Encabezado + (k.SoloVos ? " · grabando solo vos" : " · grabando");
            // solo vos: la columna de la IA y los ":" se ocultan y la tuya ocupa todo el ancho
            ColContIa.Visibility = TxtDosPuntos.Visibility = k.SoloVos ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetColumn(ColContVos, k.SoloVos ? 0 : 2);
            Grid.SetColumnSpan(ColContVos, k.SoloVos ? 3 : 1);
                        TxtContIa.Text = k.Ia; TxtContIaDet.Text = k.IaDetalle;
            TxtContVos.Text = k.Vos; TxtContVosDet.Text = k.VosDetalle;
            TxtPieContadores.Text = k.Pie;
        }
    }

    // ---- formas: rombo = IA, circulo = vos, siempre con la palabra al lado

    private static UIElement Forma(FormaMarcador f, string color, double tam, bool hueco = false)
    {
        var relleno = hueco ? Brushes.Transparent : Pincel.De(color);
        double trazo = hueco ? 2 : 0;
        if (f == FormaMarcador.Circulo)
            return new Ellipse { Width = tam, Height = tam, Fill = relleno, Stroke = Pincel.De(color), StrokeThickness = trazo };
        double m = tam / 2;
        return new Polygon
        {
            Points = [new Point(m, 0), new Point(tam, m), new Point(m, tam), new Point(0, m)],
            Fill = relleno, Stroke = Pincel.De(color), StrokeThickness = trazo,
        };
    }

    private void ReponerEtiquetas(StackPanel panel, IReadOnlyList<EtiquetaRef> etiquetas)
    {
        panel.Children.Clear();
        for (int i = 0; i < etiquetas.Count; i++)
        {
            var e = etiquetas[i];
            if (i > 0)
                panel.Children.Add(new TextBlock { Text = "·", Foreground = Pincel.De(Paleta.Apoyo), Margin = new Thickness(5, 0, 5, 0), FontFamily = (FontFamily)FindResource("Inter"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            var f = Forma(e.Forma, e.Color, 9);
            ((FrameworkElement)f).Margin = new Thickness(0, 0, 4, 0);
            ((FrameworkElement)f).VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(f);
            panel.Children.Add(new TextBlock
            {
                Text = e.Texto, Foreground = Pincel.De(e.Color), FontFamily = (FontFamily)FindResource("Inter"),
                FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center,
            });
        }
    }

    /// <summary>
    /// Barra segmentada (UI-SPEC 7.3): 16 segmentos de 10 de alto con separacion de 3. Segmento 0 blanco = ahora; los segmentos hasta el
    /// punto de frenado de la referencia principal, en gris; el resto, vacios; marca roja en el extremo. Rombo = IA y circulo = vos,
    /// como cabezas de 14 px con tallo de 8. Alto total 32.
    /// </summary>
    private void DibujarRegla(VistaPanel p)
    {
        var cv = CanvasRegla;
        cv.Children.Clear();
        double w = 292;       // ancho util de la tarjeta (320 - 2 x 14)
        int n = MedidasUi.SegmentosBarra;
        double sep = MedidasUi.SeparacionSegmento, alto = MedidasUi.AltoSegmento;
        double sw = (w - sep * (n - 1)) / n, top = 32 - alto;

        var principal = p.Marcadores.FirstOrDefault(m => p.EtiquetasPrincipal.Count > 0 && m.Forma == p.EtiquetasPrincipal[0].Forma)
            ?? (p.Marcadores.Count > 0 ? p.Marcadores[0] : null);
        int corte = principal is null ? n - 1 : Math.Clamp((int)Math.Round(principal.Fraccion * n), 1, n - 1);

        for (int i = 0; i < n; i++)
        {
            string color = i == 0 ? Paleta.Blanco : i < corte ? Paleta.SegmentoRecorrido : Paleta.ReglaFondo;
            var seg = new Rectangle { Width = sw, Height = alto, Fill = Pincel.De(color) };
            Canvas.SetLeft(seg, i * (sw + sep)); Canvas.SetTop(seg, top);
            cv.Children.Add(seg);
        }
        if (principal is not null)
        {
            var marca = new Rectangle { Width = 2, Height = alto + 4, Fill = Pincel.De(Paleta.RojoFranja) };
            Canvas.SetLeft(marca, corte * (sw + sep) - sep / 2 - 1); Canvas.SetTop(marca, top - 2);
            cv.Children.Add(marca);
        }
        foreach (var m in p.Marcadores)
        {
            double x = Math.Clamp(w * m.Fraccion, 7, w - 7);
            var tallo = new Rectangle { Width = 2, Height = 8, Fill = Pincel.De(m.Color) };
            Canvas.SetLeft(tallo, x - 1); Canvas.SetTop(tallo, 14);
            cv.Children.Add(tallo);
            var cab = Forma(m.Forma, m.Color, 14);
            Canvas.SetLeft(cab, x - 7); Canvas.SetTop(cab, 0);
            cv.Children.Add(cab);
        }
    }

    // ---------------------------------------------------------------- cabecera

    private void PintarCabecera(VistaOverlay v)
    {
        TxtTituloPista.Text = v.TituloPista;
        TxtTituloPista.ToolTip = v.TituloPista;
        TxtChipClase.Text = v.ChipClase;
        ChipClase.Visibility = v.ChipClase.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        bool grab = v.Grabando;
        ChipModo.Background = Pincel.De(grab ? Paleta.ChipGrabandoFondo : Paleta.ChipMostrarFondo);
        TxtChipModo.Foreground = Pincel.De(grab ? Paleta.ChipGrabandoTexto : Paleta.ChipMostrarTexto);
        TxtChipModo.Text = grab ? (v.SoloVos ? "GRABANDO · SOLO VOS" : "GRABANDO") : "MOSTRAR";
        PuntoChipModo.Visibility = grab ? Visibility.Visible : Visibility.Collapsed;
        TxtAviso.Visibility = v.Aviso is null ? Visibility.Collapsed : Visibility.Visible;
        TxtAviso.Text = v.Aviso ?? "";
    }
}
