using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AMS2Frenado.Core;
using AMS2Frenado.Core.Datos;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Overlay;
using Forms = System.Windows.Forms;

namespace AMS2Frenado.App;

/// <summary>
/// Une todo: lee la telemetria (~60 Hz), se la pasa al <see cref="ServicioOverlay"/> del Core, pinta la
/// ventana, atiende la bandeja y el atajo, y guarda la configuracion. La logica esta en el Core (con pruebas);
/// aca solo hay pegamento de Windows. Compila, SIN EJECUTAR: nunca corrio contra AMS2 ni en Windows.
/// </summary>
internal sealed class ControladorApp : IDisposable
{
    private readonly AlmacenConfig _almacenCfg = new();
    private readonly Almacen _almacen = new();
    private readonly ConfigApp _cfg;
    private readonly ServicioOverlay _servicio;
    private readonly LectorTelemetria _lector = new();
    private readonly Suavizador _suavizador = new();
    private readonly Stopwatch _reloj = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly VentanaOverlay _ventana = new();
    private readonly VentanaAviso _aviso = new();
    // fase 6: modelos del Core (con pruebas) y las dos ventanas nuevas, que se crean en Iniciar() cuando la ventana grande ya existe (Owner)
    private readonly BarraBotones _modeloBarra = new();
    private readonly MenuConfig _modeloMenu = new();
    private VentanaBarra _barra = null!;
    private VentanaMenu _menu = null!;
    private Rect? _ultimaReserva;
    private readonly Pitido _pitido = new();
    private readonly Bandeja _bandeja = new();
    private readonly AtajoGlobal _atajo = new();
    private VentanaDiagnostico? _diagnostico;

    private bool _anclada = true;          // sin posicion propia: se ancla a la esquina por defecto (arriba a la derecha)
    private string? _ultimoAviso;
    private double _ultimaReafirmacion;
    private bool _liberado;

    public ControladorApp()
    {
        _cfg = _almacenCfg.Cargar();
        _servicio = new ServicioOverlay(_almacen);
        AplicarAlServicio();

        _ventana.AplicarEscala(_cfg.Escala);
        SincronizarModeloBarra();
        _ventana.AplicarBloqueo(_cfg.Bloqueado, _modeloBarra);
        _aviso.AplicarEscala(_cfg.AvisoEscala);
        _aviso.AplicarBloqueo(_cfg.Bloqueado);
        _aviso.Movida += AlMoverAviso;
        _aviso.EscalaCambiada += e => { _cfg.AvisoEscala = e; Guardar(); _bandeja.Actualizar(_cfg, _atajo.Actual); RefrescarMenu(); };
        _ventana.SizeChanged += (_, _) => { if (_anclada) Anclar(); };
        _ventana.Movida += AlMoverVentana;
        _ventana.EscalaCambiada += e => { _cfg.Escala = e; Guardar(); _bandeja.Actualizar(_cfg, _atajo.Actual); RefrescarMenu(); if (_anclada) Anclar(); };

        _atajo.Pulsado += () => Alternar(() => _cfg.Bloqueado = !_cfg.Bloqueado, aplicarBloqueo: true);
        ConectarBandeja();
        _timer.Tick += (_, _) => Tick();
    }

    public void Iniciar()
    {
        PosicionInicial();
        PosicionInicialAviso();
        _ventana.Show();                      // la ventana grande queda siempre abierta: "ocultar" solo esconde su contenido, asi la barra (su Owner) puede dejar la pastilla
        _ventana.AplicarVisible(_cfg.Visible);
        _barra = new VentanaBarra(_ventana);
        _barra.Pulsado += AlPulsarBoton;
        _barra.ModoElegido += AlElegirModoPopover;
        _barra.CierreConfirmado += () => { if (_modeloBarra.ConfirmarCierre() == AccionBarra.CerrarApp) Salir(); };
        _barra.CierreCancelado += () => { _modeloBarra.CancelarCierre(); SincronizarBarra(); };
        _menu = new VentanaMenu { Reloj = () => _reloj.Elapsed.TotalSeconds };
        ConectarMenu();
        SincronizarBarra();
        _barra.Show();
        _menu.Owner = _barra;
        // la ventana del aviso queda abierta y transparente (opacidad 0): asi no hay parpadeo al aparecer, solo el fundido
        _aviso.Show();

        bool atajoOk = _atajo.Registrar(_cfg.AtajoEfectivo);
        _bandeja.Actualizar(_cfg, _atajo.Actual);
        RefrescarMenu();
        _timer.Start();

        if (!atajoOk)
            _bandeja.Globo("Atajo no disponible",
                $"No se pudo registrar {_cfg.AtajoEfectivo.Texto} (otro programa ya lo usa). La app sigue funcionando: " +
                "fijá y liberá el overlay con el botón de la chincheta (o desde la bandeja), o elegí otro atajo en ⚙ › Atajo de bloqueo.", error: true);
        else if (!_cfg.AvisoInicialMostrado)
        {
            _bandeja.Globo("AMS2 Asistente de Frenado",
                $"El overlay está fijado: los clics lo atraviesan, salvo los botones de arriba (⇄ modo, ⚙ ajustes, chincheta, ✕). " +
                $"Atajo para fijar/liberar: {_cfg.AtajoEfectivo.Texto}. La bandeja queda como respaldo (en Windows 11 suele quedar oculta en la flecha ^).");
        }
        if (!_cfg.AvisoInicialMostrado) { _cfg.AvisoInicialMostrado = true; Guardar(); }
        if (_almacenCfg.LecturaDanada)
            _bandeja.Globo("Configuración dañada", "No se pudo leer config.json; se usaron valores anteriores o por defecto.", error: true);
    }

    // ---------------------------------------------------------------- tick

    private void Tick()
    {
        try
        {
            double t = _reloj.Elapsed.TotalSeconds;
            ResultadoLectura r;
            try { r = _lector.Leer(); }
            catch (Exception) { r = ResultadoLectura.SinJuego; }

            var v = _servicio.Paso(r, t);
            if (v.Pitar) _pitido.Reproducir();
            if (v.Aviso is not null && v.Aviso != _ultimoAviso)
                _bandeja.Globo("Aviso", v.Aviso, error: true);
            _ultimoAviso = v.Aviso;

            if (_cfg.Visible)
            {
                var cuadro = _suavizador.Actualizar(v.Panel, t);
                _ventana.Pintar(v, cuadro, _cfg);
            }
            if (t - _ultimaReafirmacion > 2)
            {
                _ultimaReafirmacion = t; _ventana.ReafirmarArriba(); _aviso.ReafirmarArriba(); _barra.ReafirmarArriba();
                if (_menu.IsVisible) _menu.Topmost = true;
            }
            TickFase6(t);
            // el aviso se actualiza en cada tick (60 Hz) aunque la telemetria llegue a ~27 Hz, para que el fundido de 80 ms sea fluido.
            // El simbolo y el pitido de una prueba salen del mismo cuadro; el de pista, de la misma muestra (v.Pitar, arriba).
            var cuadroAviso = _servicio.ActualizarAviso(t);
            _aviso.Pintar(cuadroAviso.Opacidad, cuadroAviso.Escala);
            if (cuadroAviso.Pitar) _pitido.Reproducir();
        }
        catch (Exception e)
        {
            // el overlay nunca debe tirar la app abajo en plena carrera
            Debug.WriteLine(e);
        }
    }

    // ---------------------------------------------------------------- config

    private void AplicarAlServicio()
    {
        _servicio.Modo = _cfg.Modo;
        _servicio.ReferenciaPrincipal = _cfg.ReferenciaPrincipal;
        _servicio.Pitido = _cfg.Pitido;
        // "Ocultar overlay" tambien apaga el aviso de frenada
        _servicio.AvisoActivo = _cfg.AvisoActivo && _cfg.Visible;
        _servicio.AvisoDuracionS = _cfg.AvisoDuracionS;
        _servicio.AvisoRampaS = _cfg.AvisoRampaS;
        _servicio.AvisoAnticipacionS = _cfg.AvisoAnticipacionS;
    }

    private void Guardar()
    {
        try { _almacenCfg.Guardar(_cfg); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _bandeja.Globo("No se pudo guardar la configuración", e.Message, error: true);
        }
    }

    /// <summary>Aplica un cambio de configuracion, lo guarda y refresca servicio, ventana y menu.</summary>
    private void Alternar(Action cambio, bool aplicarBloqueo = false)
    {
        cambio();
        _cfg.Normalizar();
        AplicarAlServicio();
        if (aplicarBloqueo) AplicarBloqueoAVentanas();
        AplicarVisibleAVentana();
        _bandeja.Actualizar(_cfg, _atajo.Actual);
        RefrescarMenu();
        Guardar();
    }

    private void ConectarBandeja()
    {
        _bandeja.AlternarBloqueo += () => Alternar(() => _cfg.Bloqueado = !_cfg.Bloqueado, aplicarBloqueo: true);
        _bandeja.AlternarVisible += () => Alternar(() => _cfg.Visible = !_cfg.Visible);
        _bandeja.AlternarPitido += () => Alternar(() => _cfg.Pitido = !_cfg.Pitido);
        _bandeja.AlternarAviso += () => Alternar(() => _cfg.AvisoActivo = !_cfg.AvisoActivo);
        _bandeja.ProbarAviso += () => _servicio.ProbarAviso(_reloj.Elapsed.TotalSeconds);
        _bandeja.ProbarPitido += () => _servicio.ProbarPitido(_reloj.Elapsed.TotalSeconds);     // respeta la anticipacion: pita al terminar la rampa
        _bandeja.EscalaAvisoElegida += s => { Alternar(() => _cfg.AvisoEscala = s); _aviso.AplicarEscala(_cfg.AvisoEscala); };
        _bandeja.DuracionAvisoElegida += d => Alternar(() => _cfg.AvisoDuracionS = d);
        _bandeja.RampaAvisoElegida += d => Alternar(() => _cfg.AvisoRampaS = d);
        _bandeja.AnticipacionElegida += d => Alternar(() => _cfg.AvisoAnticipacionS = d);
        _bandeja.ModoElegido += m => Alternar(() => _cfg.Modo = m);
        _bandeja.ReferenciaElegida += r => Alternar(() => _cfg.ReferenciaPrincipal = r);
        _bandeja.OpacidadElegida += a => Alternar(() => _cfg.Opacidad = a);
        _bandeja.EscalaElegida += s =>
        {
            Alternar(() => _cfg.Escala = s);
            _ventana.AplicarEscala(_cfg.Escala);
            if (_anclada) Anclar();
        };
        _bandeja.AtajoElegido += ElegirAtajo;
        _bandeja.AbrirCarpeta += () => _bandeja.AbrirEnExplorador(_almacen.RutaBase);
        _bandeja.AbrirCarpetaCapturas += () => _bandeja.AbrirEnExplorador(CarpetaCapturas);
        _bandeja.ExportarDatos += ExportarDatos;
        _bandeja.ImportarDatos += ImportarDatos;
        _bandeja.AbrirDiagnostico += AbrirDiagnostico;
        _bandeja.Salir += Salir;
    }

    private void ElegirAtajo(string texto)
    {
        var nuevo = AtajoTeclado.Parsear(texto);
        if (nuevo is null) return;
        if (_atajo.Registrar(nuevo))
        {
            Alternar(() => _cfg.Atajo = nuevo.Texto, aplicarBloqueo: true);
        }
        else
        {
            _atajo.Registrar(_cfg.AtajoEfectivo);
            _bandeja.Globo("Atajo no disponible", $"{nuevo.Texto} ya lo usa otro programa. Se mantiene {_cfg.AtajoEfectivo.Texto}.", error: true);
        }
    }

    // ---------------------------------------------------------------- fase 6: barra de botones y menu de configuracion (compila, sin ejecutar)

    private double Ahora => _reloj.Elapsed.TotalSeconds;

    private void SincronizarModeloBarra()
    {
        _modeloBarra.Fijado = _cfg.Bloqueado;
        _modeloBarra.Modo = _cfg.Modo;
        _modeloBarra.Atajo = _cfg.AtajoEfectivo.Texto;
        _modeloBarra.Oculto = !_cfg.Visible;
        _modeloBarra.FijarMenuAbierto(_modeloMenu.Abierto);
    }

    /// <summary>Fijar / liberar: la ventana grande cambia de estilo (click-through) y las etiquetas; la de botones sigue activa siempre.</summary>
    private void AplicarBloqueoAVentanas()
    {
        SincronizarModeloBarra();
        _ventana.AplicarBloqueo(_cfg.Bloqueado, _modeloBarra);
        _aviso.AplicarBloqueo(_cfg.Bloqueado);
    }

    private void AplicarVisibleAVentana()
    {
        _ventana.AplicarVisible(_cfg.Visible);
        if (_modeloMenu.Abierto && _modeloBarra.Oculto != !_cfg.Visible) CerrarMenu();       // ocultar o mostrar el overlay cierra el menu
        SincronizarBarra();
    }

    /// <summary>Estimacion de donde cae el hueco de la barra cuando la ventana grande no tiene contenido visible (pastilla).</summary>
    private Rect ReservaEstimada()
    {
        double s = _cfg.Escala;
        double ancho = MedidasUi.AnchoDeEscala(s);
        return new Rect(_ventana.Left + ancho - (12 + 140) * s, _ventana.Top + (10 + 20) * s, 140 * s, 32 * s);
    }

    /// <summary>Redibuja la barra si cambio el estado y la pone sobre el hueco de la cabecera (borde derecho y superior fijos).</summary>
    private void SincronizarBarra()
    {
        SincronizarModeloBarra();
        _barra.Refrescar(_modeloBarra, _cfg.Escala);
        var r = _ventana.RectBarra() ?? ReservaEstimada();
        _ultimaReserva = r;
        double bs = _barra.EscalaBarra;
        // centrada en vertical dentro del hueco; si la barra es mas grande que el hueco (escala < 0,875) sobresale parejo
        _barra.Posicionar(r.Right, r.Top + (r.Height - MedidasUi.BarraAlto * bs) / 2);
    }

    private void TickFase6(double t)
    {
        _modeloBarra.Actualizar(t);
        SincronizarBarra();
        _barra.ActualizarProgreso(_modeloBarra.FraccionRestante(t));
        if (_modeloMenu.Abierto)
        {
            _menu.ActualizarSalir(t);
            if (_modeloMenu.Actualizar(t, _menu.MouseAdentro)) CerrarMenu(yaCerrado: true);
        }
        if (_modeloMenu.DebeGuardar(t)) Guardar();
    }

    private void AlPulsarBoton(BotonBarra boton)
    {
        switch (_modeloBarra.Pulsar(boton, Ahora))
        {
            case AccionBarra.AlternarFijado:
                _cfg.Bloqueado = _modeloBarra.Fijado;
                Alternar(() => { }, aplicarBloqueo: true);
                break;
            case AccionBarra.AlternarMenu:
                if (_modeloBarra.MenuAbierto) AbrirMenu(); else CerrarMenu();
                break;
        }
        SincronizarBarra();
    }

    private void AlElegirModoPopover(ModoApp modo)
    {
        if (_modeloBarra.ElegirModo(modo) == AccionBarra.CambiarModo) Alternar(() => _cfg.Modo = modo);
        SincronizarBarra();
    }

    private void RefrescarMenu() => _menu?.Actualizar(_cfg, _cfg.AtajoEfectivo.Texto, !_cfg.Visible);

    private void AbrirMenu()
    {
        _modeloMenu.Abrir(Ahora);
        _modeloBarra.FijarMenuAbierto(true);
        RefrescarMenu();
        var (_, area) = MonitorDe(_barra);
        double alto = _menu.ActualHeight > 0 ? _menu.ActualHeight : 600;
        var rb = new AreaPantalla(_barra.Left, _barra.Top, _barra.Width, _barra.Height);
        bool una = Esquinas.MenuEnUnaColumna(area);
        double ancho = una ? 352 : MedidasUi.MenuAncho;
        var (x, y) = Esquinas.PosicionMenu(rb, area, ancho, alto);
        _menu.AbrirEn(x, y, una, area.Alto);
        // una sola vez, con el alto real ya medido: se corrige si se salia de la pantalla (despues no se mueve)
        _menu.Dispatcher.BeginInvoke(() =>
        {
            if (!_menu.IsVisible) return;
            var (x2, y2) = Esquinas.PosicionMenu(rb, area, _menu.ActualWidth, _menu.ActualHeight);
            _menu.Left = x2; _menu.Top = y2;
        }, DispatcherPriority.Loaded);
    }

    private void CerrarMenu(bool yaCerrado = false)
    {
        if (!yaCerrado) _modeloMenu.Cerrar();
        _modeloBarra.FijarMenuAbierto(false);
        _menu.CerrarAnimado();
        if (_modeloMenu.DebeGuardarYa()) Guardar();       // un deslizador movido hace menos de 500 ms: se guarda sin esperar
        SincronizarBarra();
    }

    /// <summary>Enlaza el menu con la misma logica de la bandeja (que queda como respaldo): una sola implementacion por opcion.</summary>
    private void ConectarMenu()
    {
        _menu.CerrarPulsado += () => { _modeloBarra.FijarMenuAbierto(false); CerrarMenu(); };
        _menu.AlternarFijar += () => Alternar(() => _cfg.Bloqueado = !_cfg.Bloqueado, aplicarBloqueo: true);
        _menu.AlternarAviso += () => Alternar(() => _cfg.AvisoActivo = !_cfg.AvisoActivo);
        _menu.AlternarPitido += () => Alternar(() => _cfg.Pitido = !_cfg.Pitido);
        _menu.ProbarAviso += () => _servicio.ProbarAviso(Ahora);
        _menu.ProbarPitido += () => _servicio.ProbarPitido(Ahora);
        _menu.AlternarVisible += () => Alternar(() => _cfg.Visible = !_cfg.Visible);
        _menu.ModoElegido += m => Alternar(() => _cfg.Modo = m);
        _menu.ReferenciaElegida += r => Alternar(() => _cfg.ReferenciaPrincipal = r);
        _menu.AtajoElegido += ElegirAtajo;
        _menu.PaginaPedida += p =>
        {
            if (p == PaginaMenu.Principal) _modeloMenu.Volver(); else _modeloMenu.IrA(p);
            _menu.MostrarPagina(_modeloMenu.Pagina);
        };
        _menu.AbrirCarpeta += () => _bandeja.AbrirEnExplorador(_almacen.RutaBase);
        _menu.AbrirCarpetaCapturas += () => _bandeja.AbrirEnExplorador(CarpetaCapturas);
        _menu.ExportarDatos += ExportarDatos;
        _menu.ImportarDatos += ImportarDatos;
        _menu.AbrirDiagnostico += AbrirDiagnostico;
        _menu.SalirConfirmado += Salir;
        // deslizadores: se aplican en vivo y el config.json se guarda 500 ms despues del ultimo cambio (no en cada movimiento)
        _menu.Deslizado += (def, valor) =>
        {
            def.Aplicar(_cfg, valor);
            AplicarAlServicio();
            if (def == MenuConfig.Tamano) { _ventana.AplicarEscala(_cfg.Escala); if (_anclada) Anclar(); }
            if (def == MenuConfig.TamanoAviso) _aviso.AplicarEscala(_cfg.AvisoEscala);
            _bandeja.Actualizar(_cfg, _atajo.Actual);
            _modeloMenu.MarcarCambio(Ahora);
        };
    }

    private void AbrirDiagnostico()
    {
        if (_diagnostico is not null) { _diagnostico.Activate(); return; }
        _diagnostico = new VentanaDiagnostico();
        _diagnostico.Closed += (_, _) => _diagnostico = null;
        _diagnostico.Show();
    }

    // ---------------------------------------------------------------- posicion

    /// <summary>Monitor bajo el centro de la ventana (o el principal) con su area de trabajo en DIP.</summary>
    private (string clave, AreaPantalla area) MonitorActual() => MonitorDe(_ventana);

    private static (string clave, AreaPantalla area) MonitorDe(Window w)
    {
        double sx = 1, sy = 1;
        try { var d = VisualTreeHelper.GetDpi(w); sx = d.DpiScaleX; sy = d.DpiScaleY; } catch { }
        var scr = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        if (w.IsVisible)
        {
            var centro = new System.Drawing.Point((int)((w.Left + w.ActualWidth / 2) * sx), (int)((w.Top + w.ActualHeight / 2) * sy));
            scr = Forms.Screen.FromPoint(centro);
        }
        var wa = scr.WorkingArea;
        return (scr.DeviceName, new AreaPantalla(wa.X / sx, wa.Y / sy, wa.Width / sx, wa.Height / sy));
    }

    private void PosicionInicial()
    {
        var (clave, area) = MonitorActual();
        _ventana.WindowStartupLocation = WindowStartupLocation.Manual;
        if (_cfg.Posiciones.TryGetValue(clave, out var p))
        {
            var (x, y) = Esquinas.AjustarDentro((p.X, p.Y), area, MedidasUi.AnchoDeEscala(_cfg.Escala), 200);
            _ventana.Left = x; _ventana.Top = y;
            _anclada = false;
        }
        else
        {
            var (x, y) = Esquinas.Posicionar(_cfg.Esquina, area, MedidasUi.AnchoDeEscala(_cfg.Escala), 200);
            _ventana.Left = x; _ventana.Top = y;
            _anclada = true;
        }
    }

    /// <summary>Aviso: posicion guardada por monitor o, la primera vez, centrado en horizontal y en el tercio superior.</summary>
    private void PosicionInicialAviso()
    {
        var (clave, area) = MonitorDe(_aviso);
        double lado = MedidasAviso.LadoDeEscala(_cfg.AvisoEscala);
        _aviso.WindowStartupLocation = WindowStartupLocation.Manual;
        if (_cfg.AvisoPosiciones.TryGetValue(clave, out var p))
        {
            var (x, y) = Esquinas.AjustarDentro((p.X, p.Y), area, lado, lado);
            _aviso.Left = x; _aviso.Top = y;
        }
        else
        {
            _aviso.Left = area.X + (area.Ancho - lado) / 2;
            _aviso.Top = area.Y + area.Alto * 0.30;
        }
    }

    private void AlMoverAviso()
    {
        var (clave, area) = MonitorDe(_aviso);
        var (x, y) = Esquinas.AjustarDentro((_aviso.Left, _aviso.Top), area, _aviso.ActualWidth, _aviso.ActualHeight);
        _cfg.AvisoPosiciones[clave] = new PosicionVentana { X = x, Y = y };
        Guardar();
    }

    private void Anclar()
    {
        var (_, area) = MonitorActual();
        double w = _ventana.ActualWidth > 0 ? _ventana.ActualWidth : MedidasUi.AnchoDeEscala(_cfg.Escala);
        double h = _ventana.ActualHeight > 0 ? _ventana.ActualHeight : 200;
        var (x, y) = Esquinas.Posicionar(_cfg.Esquina, area, w, h);
        _ventana.Left = x; _ventana.Top = y;
    }

    private void AlMoverVentana()
    {
        _anclada = false;
        var (clave, area) = MonitorActual();
        var (x, y) = Esquinas.AjustarDentro((_ventana.Left, _ventana.Top), area, _ventana.ActualWidth, _ventana.ActualHeight);
        _cfg.Posiciones[clave] = new PosicionVentana { X = x, Y = y };
        Guardar();
    }

    // ---------------------------------------------------------------- respaldo de datos (compila, sin ejecutar)

    private static string CarpetaCapturas => System.IO.Path.Combine(Almacen.RutaPorDefecto(), RespaldoDatos.CarpetaCapturas);

    /// <summary>Ventana invisible y siempre arriba para que los cuadros de dialogo no queden detras de AMS2 en Borderless.</summary>
    private static T EnPrimerPlano<T>(Func<Window, T> f)
    {
        var dueno = new Window
        {
            Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Topmost = true,
            AllowsTransparency = true, Background = Brushes.Transparent, WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        dueno.Show();
        try { return f(dueno); }
        finally { dueno.Close(); }
    }

    private void ExportarDatos()
    {
        try
        {
            _servicio.GuardarGrabacion();        // lo grabado hasta ahora entra en el respaldo
            Guardar();
            string? destino = EnPrimerPlano(w =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Exportar datos de AMS2 Asistente de Frenado",
                    Filter = "Respaldo de datos (*.zip)|*.zip", DefaultExt = ".zip", AddExtension = true, OverwritePrompt = true,
                    FileName = $"AMS2-datos-{DateTime.Now:yyyyMMdd}.zip",
                };
                return dlg.ShowDialog(w) == true ? dlg.FileName : null;
            });
            if (destino is null) return;
            int n = RespaldoDatos.Exportar(_almacen.RutaBase, destino);
            _bandeja.Globo("Datos exportados", $"{n} archivos (sin capturas) en {destino}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _bandeja.Globo("No se pudo exportar", e.Message, error: true);
        }
    }

    private void ImportarDatos()
    {
        if (_cfg.Modo != ModoApp.Mostrar)
        {
            _bandeja.Globo("Importar datos", "Pasá primero a Mostrar (botón ⇄ del overlay): mientras se graba (todo o solo vos) el programa podría pisar lo importado.", error: true);
            return;
        }
        try
        {
            string? origen = EnPrimerPlano(w =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Importar datos de AMS2 Asistente de Frenado",
                    Filter = "Respaldo de datos (*.zip)|*.zip", CheckFileExists = true,
                };
                if (dlg.ShowDialog(w) != true) return null;
                var ok = MessageBox.Show(w,
                    "Se van a reemplazar los archivos de datos que traiga el .zip. De cada archivo que ya existe queda una copia .bak " +
                    "al lado. Las capturas no se tocan.\n\n¿Importar?",
                    "Importar datos", MessageBoxButton.YesNo, MessageBoxImage.Question);
                return ok == MessageBoxResult.Yes ? dlg.FileName : null;
            });
            if (origen is null) return;

            _servicio.GuardarGrabacion();
            var r = RespaldoDatos.Importar(origen, _almacen.RutaBase);
            _servicio.ForzarRecarga();
            if (r.ConfigImportada) RecargarConfigImportada();
            string extra = r.Omitidos.Count > 0 ? $" ({r.Omitidos.Count} omitidos)" : "";
            _bandeja.Globo("Datos importados", $"{r.Importados} archivos importados, {r.Respaldados} respaldados como .bak{extra}.");
        }
        catch (RespaldoInvalidoException e)
        {
            _bandeja.Globo("No se importó nada", e.Message, error: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _bandeja.Globo("No se pudo importar", e.Message, error: true);
        }
    }

    /// <summary>Despues de importar un config.json: se lo lleva a la configuracion en uso para que no se pise al guardar.</summary>
    private void RecargarConfigImportada()
    {
        _cfg.CopiarDe(new AlmacenConfig().Cargar());
        AplicarAlServicio();
        _ventana.AplicarEscala(_cfg.Escala);
        _aviso.AplicarEscala(_cfg.AvisoEscala);
        AplicarBloqueoAVentanas();
        if (!string.Equals(_atajo.Actual?.Texto, _cfg.AtajoEfectivo.Texto, StringComparison.OrdinalIgnoreCase)) _atajo.Registrar(_cfg.AtajoEfectivo);
        AplicarVisibleAVentana();
        RefrescarMenu();
        _bandeja.Actualizar(_cfg, _atajo.Actual);
        Guardar();
    }

    // ---------------------------------------------------------------- salida

    private void Salir()
    {
        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_liberado) return;
        _liberado = true;
        _timer.Stop();
        _servicio.GuardarGrabacion();         // lo grabado hasta ahora
        Guardar();
        _atajo.Dispose();
        _bandeja.Dispose();
        _diagnostico?.Close();
        _menu?.Close();
        _barra?.Close();
        _aviso.Close();
        _ventana.Close();
        _lector.Dispose();
    }
}
