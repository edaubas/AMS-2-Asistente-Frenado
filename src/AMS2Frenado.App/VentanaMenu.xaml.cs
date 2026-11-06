using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AMS2Frenado.App.Interop;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;

namespace AMS2Frenado.App;

/// <summary>
/// Menu de configuracion dentro del overlay (UI-SPEC 7.5): ventana propia, opaca, de 624 de ancho en dos columnas, con paginas
/// internas ("Datos", "Atajo de bloqueo") en vez de submenus, solo para mouse (NOACTIVATE: no recibe teclado). Tiene TODAS las
/// opciones del menu de la bandeja, que queda como respaldo. No decide nada: publica eventos y el
/// <see cref="ControladorApp"/> los aplica a la configuracion y vuelve a llamar a <see cref="Actualizar"/>. Los rangos, pasos y
/// formatos salen de <see cref="MenuConfig"/> (Core, con pruebas). Compila, SIN EJECUTAR.
/// </summary>
internal sealed partial class VentanaMenu : Window
{
    private sealed class FilaDeslizador
    {
        public required DeslizadorDef Def;
        public required Slider Control;
        public required TextBlock Valor;
    }

    private readonly List<FilaDeslizador> _filas = [];
    private readonly Dictionary<ModoApp, System.Windows.Controls.Primitives.ToggleButton> _modos = [];
    private readonly Dictionary<ReferenciaGuia, System.Windows.Controls.Primitives.ToggleButton> _refs = [];
    private readonly Dictionary<string, Button> _atajos = [];
    private readonly ConfirmacionTemporizada _salir = new();
    private bool _poniendo;
    private bool _grabando;
    private IntPtr _hwnd;

    public event Action? CerrarPulsado, AlternarFijar, AlternarAviso, AlternarPitido, ProbarAviso, ProbarPitido, AlternarVisible;
    public event Action? AbrirCarpeta, AbrirCarpetaCapturas, ExportarDatos, ImportarDatos, AbrirDiagnostico, SalirConfirmado;
    public event Action<PaginaMenu>? PaginaPedida;
    public event Action<ModoApp>? ModoElegido;
    public event Action<ReferenciaGuia>? ReferenciaElegida;
    public event Action<string>? AtajoElegido;
    public event Action<DeslizadorDef, double>? Deslizado;
    /// <summary>El usuario soltó la ventana despues de arrastrarla por la cabecera: (Left, Top) en DIP.</summary>
    public event Action<double, double>? PosicionElegida;

    /// <summary>Area de trabajo (DIP) del monitor que contiene el punto dado (DIP de pantalla). Lo pone el controlador.</summary>
    public Func<double, double, AreaPantalla>? AreaEn { get; set; }
    public bool Arrastrando => _arrastre.Activo;
    private readonly ArrastreVentana _arrastre = new();
    private UIElement? _agarre;

    /// <summary>Reloj en segundos (el del controlador) para la pregunta de salir.</summary>
    public Func<double> Reloj { get; set; } = () => 0;
    public bool MouseAdentro => IsVisible && IsMouseOver;
    public IntPtr Hwnd => _hwnd;

    public VentanaMenu()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => { _hwnd = new WindowInteropHelper(this).Handle; Win32.AplicarEstiloControl(_hwnd); };
        TxtTituloMenu.Text = Estilo.Espaciado("Ajustes");
        TxtTituloDatos.Text = Estilo.Espaciado("Datos");
        TxtTituloAtajo.Text = Estilo.Espaciado("Atajo de bloqueo");
        TxtPreguntaSalir.Text = "¿SALIR?";
        TxtAtajoOtro.Text = MenuConfig.AtajoOtro;
        foreach (var cab in new UIElement[] { CabPrincipal, CabDatos, CabAtajo }) ConectarArrastre(cab);

        // modo y referencia: segmentados (la opcion elegida va en blanco)
        foreach (var m in new[] { ModoApp.Grabar, ModoApp.GrabarSoloVos, ModoApp.Mostrar })
        {
            var t = Segmento(m == ModoApp.Grabar ? "GRABAR TODO" : m == ModoApp.GrabarSoloVos ? "SOLO VOS" : "MOSTRAR", () => ModoElegido?.Invoke(m));
            _modos[m] = t; SegModo.Children.Add(t);
        }
        foreach (var r in new[] { ReferenciaGuia.Ia, ReferenciaGuia.Vos })
        {
            var t = Segmento(r == ReferenciaGuia.Ia ? "◆ IA" : "● VOS", () => ReferenciaElegida?.Invoke(r));
            _refs[r] = t; SegReferencia.Children.Add(t);
        }
        // deslizadores: Tamano y Opacidad (overlay); Tamano del aviso, Anticipacion, Duracion y Rampa (aviso)
        foreach (var d in new[] { MenuConfig.Tamano, MenuConfig.Opacidad }) PilaOverlay.Children.Add(CrearFila(d));
        foreach (var d in new[] { MenuConfig.TamanoAviso, MenuConfig.Anticipacion, MenuConfig.Duracion, MenuConfig.Rampa }) PilaAviso.Children.Add(CrearFila(d));
        // atajos sugeridos (pagina interna)
        foreach (var a in MenuConfig.AtajosSugeridos)
        {
            var b = new Button { Style = (Style)FindResource("Fila"), Margin = new Thickness(0, 0, 0, 6), Content = a };
            var texto = a;
            b.Click += (_, _) => AtajoElegido?.Invoke(texto);
            _atajos[a] = b; ListaAtajos.Children.Add(b);
        }

        BtnCerrarMenu.Click += (_, _) => CerrarPulsado?.Invoke();
        SwFijar.Click += (_, _) => AlternarFijar?.Invoke();
        SwAviso.Click += (_, _) => AlternarAviso?.Invoke();
        SwPitido.Click += (_, _) => AlternarPitido?.Invoke();
        BtnProbarAviso.Click += (_, _) => ProbarAviso?.Invoke();
        BtnProbarPitido.Click += (_, _) => ProbarPitido?.Invoke();
        BtnOcultar.Click += (_, _) => AlternarVisible?.Invoke();
        BtnDatos.Click += (_, _) => PaginaPedida?.Invoke(PaginaMenu.Datos);
        BtnAtajo.Click += (_, _) => PaginaPedida?.Invoke(PaginaMenu.Atajo);
        BtnVolverDatos.Click += (_, _) => PaginaPedida?.Invoke(PaginaMenu.Principal);
        BtnVolverAtajo.Click += (_, _) => PaginaPedida?.Invoke(PaginaMenu.Principal);
        BtnAbrirDatos.Click += (_, _) => AbrirCarpeta?.Invoke();
        BtnAbrirCapturas.Click += (_, _) => AbrirCarpetaCapturas?.Invoke();
        BtnExportar.Click += (_, _) => ExportarDatos?.Invoke();
        BtnImportar.Click += (_, _) => ImportarDatos?.Invoke();
        BtnDiagnostico.Click += (_, _) => AbrirDiagnostico?.Invoke();
        // "Salir" usa el mismo doble paso que la X de la barra
        BtnSalir.Click += (_, _) => { _salir.Pedir(Reloj()); ActualizarSalir(Reloj()); };
        BtnSalirNo.Click += (_, _) => { _salir.Cancelar(); ActualizarSalir(Reloj()); };
        BtnSalirSi.Click += (_, _) => { if (_salir.Confirmar()) SalirConfirmado?.Invoke(); ActualizarSalir(Reloj()); };
    }

    // ---------------------------------------------------------------- mover la ventana desde la cabecera

    /// <summary>
    /// Arrastre MANUAL: la ventana es NOACTIVATE (nunca toma el foco, para no sacarselo a AMS2) y DragMove / WM_NCLBUTTONDOWN
    /// (HTCAPTION) arrancan el bucle modal de movimiento del sistema, que sobre una ventana sin activar y con AllowsTransparency
    /// no es fiable. Por eso: captura de mouse WPF en la cabecera + posicion del cursor con GetCursorPos (absoluta: mover la
    /// ventana no realimenta el calculo) + SetWindowPos con SWP_NOACTIVATE. Los limites salen de <see cref="ArrastreVentana"/>
    /// (Core, con pruebas). Los botones de la cabecera (cerrar, volver) marcan el clic como atendido y no arrancan el arrastre.
    /// Compila, SIN EJECUTAR.
    /// </summary>
    private void ConectarArrastre(UIElement cabecera)
    {
        cabecera.MouseLeftButtonDown += (s, e) =>
        {
            if (e.Handled || _hwnd == IntPtr.Zero || !Win32.CursorEnPantalla(out int px, out int py)) return;
            var (sx, sy) = EscalaDpi();
            _arrastre.Iniciar(px / sx, py / sy, Left, Top);
            _agarre = (UIElement)s;
            _agarre.CaptureMouse();
            e.Handled = true;
        };
        cabecera.MouseMove += (s, e) =>
        {
            if (!_arrastre.Activo || !ReferenceEquals(s, _agarre) || e.LeftButton != MouseButtonState.Pressed
                || !Win32.CursorEnPantalla(out int px, out int py)) return;
            var (sx, sy) = EscalaDpi();
            double cx = px / sx, cy = py / sy;
            var area = AreaEn?.Invoke(cx, cy) ?? new AreaPantalla(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            var (x, y) = _arrastre.Mover(cx, cy, area, ActualWidth, ActualHeight);
            Win32.MoverSinActivar(_hwnd, (int)Math.Round(x * sx), (int)Math.Round(y * sy));
        };
        cabecera.MouseLeftButtonUp += (s, _) => { if (ReferenceEquals(s, _agarre)) _agarre!.ReleaseMouseCapture(); };
        cabecera.LostMouseCapture += (s, _) => { if (ReferenceEquals(s, _agarre)) FinArrastre(); };
    }

    private void FinArrastre()
    {
        if (!_arrastre.Activo) return;
        _arrastre.Terminar();
        _agarre = null;
        if (!_arrastre.Desplazado) return;      // un clic en la cabecera sin mover no cuenta como posicion elegida
        var (x, y) = _arrastre.Posicion;
        PosicionElegida?.Invoke(x, y);
    }

    private (double X, double Y) EscalaDpi()
    {
        try { var d = VisualTreeHelper.GetDpi(this); return (d.DpiScaleX, d.DpiScaleY); }
        catch (Exception) { return (1, 1); }
    }

    private System.Windows.Controls.Primitives.ToggleButton Segmento(string texto, Action alElegir)
    {
        var t = new System.Windows.Controls.Primitives.ToggleButton
        {
            Style = (Style)FindResource("Segmento"), Content = texto, Margin = new Thickness(0, 0, 2, 0),
        };
        t.Click += (_, _) => alElegir();
        return t;
    }

    private UIElement CrearFila(DeslizadorDef d)
    {
        var pila = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        var cab = new Grid();
        cab.Children.Add(new TextBlock { Text = d.Etiqueta, Style = (Style)FindResource("Texto14") });
        var valor = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            FontFamily = (FontFamily)FindResource("Barlow"), FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Pincel.De(Paleta.Blanco),
        };
        cab.Children.Add(valor);
        pila.Children.Add(cab);
        var s = new Slider
        {
            Style = (Style)FindResource("Deslizador"), Minimum = d.Min, Maximum = d.Max, TickFrequency = d.Paso, SmallChange = d.Paso, LargeChange = d.Paso * 2,
            IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, Value = d.Min,
        };
        var fila = new FilaDeslizador { Def = d, Control = s, Valor = valor };
        s.ValueChanged += (_, e) =>
        {
            valor.Text = d.Formatear(e.NewValue);
            if (!_poniendo) Deslizado?.Invoke(d, d.Ajustar(e.NewValue));
        };
        pila.Children.Add(s);
        if (d.Ayuda is { } ayuda) pila.Children.Add(new TextBlock { Text = ayuda, Style = (Style)FindResource("Ayuda"), Margin = new Thickness(0, 0, 0, 2) });
        _filas.Add(fila);
        return pila;
    }

    // ---------------------------------------------------------------- estado

    /// <summary>Refleja la configuracion en los controles sin disparar eventos.</summary>
    public void Actualizar(ConfigApp cfg, string atajoActivo, bool overlayOculto)
    {
        _poniendo = true;
        try
        {
            foreach (var (m, t) in _modos) t.IsChecked = cfg.Modo == m;
            foreach (var (r, t) in _refs) t.IsChecked = cfg.ReferenciaPrincipal == r;
            TxtAyudaModo.Text = TextosBarra.AyudaModo(cfg.Modo);
            SwFijar.IsChecked = cfg.Bloqueado;
            SwAviso.IsChecked = cfg.AvisoActivo;
            SwPitido.IsChecked = cfg.Pitido;
            TxtTeclaFijar.Text = atajoActivo;
            TxtAtajoActual.Text = atajoActivo;
            BtnOcultar.Content = overlayOculto ? "MOSTRAR OVERLAY" : "OCULTAR OVERLAY";
            foreach (var f in _filas)
            {
                double v = f.Def.Leer(cfg);
                // si el usuario esta arrastrando la perilla, la configuracion ya tiene ese valor: no se la mueve de nuevo
                if (Math.Abs(f.Control.Value - v) > f.Def.Paso / 2) f.Control.Value = v;
                f.Valor.Text = f.Def.Formatear(v);
            }
            foreach (var (a, b) in _atajos)
                b.Content = (string.Equals(cfg.Atajo, a, StringComparison.OrdinalIgnoreCase) ? "✓  " : "    ") + a;
            _grabando = cfg.Modo != ModoApp.Mostrar;
        }
        finally { _poniendo = false; }
    }

    /// <summary>Pie de la pregunta de salir (doble paso con autocancelacion a los 5 s). Se llama en cada tick con el reloj del controlador.</summary>
    public void ActualizarSalir(double t)
    {
        _salir.Actualizar(t);
        bool activa = _salir.Activa;
        PieSalir.Visibility = activa ? Visibility.Visible : Visibility.Collapsed;
        PieNormal.Visibility = BtnSalir.Visibility = activa ? Visibility.Collapsed : Visibility.Visible;
        TxtPreguntaSalir.Text = _grabando ? "¿SALIR? SE CORTA LA GRABACIÓN" : "¿SALIR?";
        ProgresoSalir.Width = Math.Max(0, Pie.ActualWidth * _salir.FraccionRestante(t));
    }

    public void MostrarPagina(PaginaMenu p)
    {
        var destino = p switch { PaginaMenu.Datos => (UIElement)PagDatos, PaginaMenu.Atajo => PagAtajo, _ => PagPrincipal };
        foreach (var e in new UIElement[] { PagPrincipal, PagDatos, PagAtajo }) e.Visibility = e == destino ? Visibility.Visible : Visibility.Collapsed;
        destino.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Estilo.Dur(100)));     // fundido de 100 ms
        _salir.Cancelar();
    }

    // ---------------------------------------------------------------- abrir y cerrar

    /// <summary>
    /// Muestra el menu en (x, y). Si la pantalla es angosta cae a una columna con desplazamiento (P35). Se calcula una vez al
    /// abrir y no se mueve mientras esta abierto. Animacion: 120 ms de opacidad y 6 px de desplazamiento.
    /// </summary>
    public void AbrirEn(double x, double y, bool unaColumna, double altoMaximo)
    {
        Reflujo(unaColumna, altoMaximo);
        Left = x; Top = y;
        MostrarPagina(PaginaMenu.Principal);
        Show();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Estilo.Dur(120)));
        Sube.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, Estilo.Dur(120)));
    }

    public void CerrarAnimado()
    {
        if (!IsVisible) return;
        _salir.Cancelar();
        var a = new DoubleAnimation(1, 0, Estilo.Dur(80));
        a.Completed += (_, _) => { if (Opacity <= 0.01) Hide(); };
        BeginAnimation(OpacityProperty, a);
    }

    private void Reflujo(bool unaColumna, double altoMaximo)
    {
        if (unaColumna)
        {
            Width = 320 + 32;
            ColSep.Width = new GridLength(0); ColDer.Width = new GridLength(0); ColIzq.Width = new GridLength(288);
            Grid.SetColumn(ColumnaDerecha, 0); Grid.SetRow(ColumnaDerecha, 1);
            ColumnaDerecha.Margin = new Thickness(0, 16, 0, 0);
            Desplazamiento.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            Desplazamiento.MaxHeight = Math.Max(200, altoMaximo - 140);
        }
        else
        {
            Width = MedidasUi.MenuAncho;
            ColSep.Width = new GridLength(16); ColDer.Width = new GridLength(288); ColIzq.Width = new GridLength(288);
            Grid.SetColumn(ColumnaDerecha, 2); Grid.SetRow(ColumnaDerecha, 0);
            ColumnaDerecha.Margin = new Thickness(0);
            Desplazamiento.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Desplazamiento.MaxHeight = double.PositiveInfinity;
        }
    }
}
