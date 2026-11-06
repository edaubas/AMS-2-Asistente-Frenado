using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AMS2Frenado.Core;
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
    private readonly Bandeja _bandeja = new();
    private readonly AtajoGlobal _atajo = new();
    private VentanaDiagnostico? _diagnostico;

    private bool _anclada = true;          // sin posicion propia: se ancla a la esquina elegida
    private string? _ultimoAviso;
    private double _ultimaReafirmacion;
    private bool _liberado;

    public ControladorApp()
    {
        _cfg = _almacenCfg.Cargar();
        _servicio = new ServicioOverlay(_almacen);
        AplicarAlServicio();

        _ventana.AplicarEscala(_cfg.Escala);
        _ventana.AplicarBloqueo(_cfg.Bloqueado, _cfg.AtajoEfectivo);
        _ventana.SizeChanged += (_, _) => { if (_anclada) Anclar(); };
        _ventana.Movida += AlMoverVentana;
        _ventana.EscalaCambiada += e => { _cfg.Escala = e; Guardar(); _bandeja.Actualizar(_cfg, _atajo.Actual); if (_anclada) Anclar(); };

        _atajo.Pulsado += () => Alternar(() => _cfg.Bloqueado = !_cfg.Bloqueado, aplicarBloqueo: true);
        ConectarBandeja();
        _timer.Tick += (_, _) => Tick();
    }

    public void Iniciar()
    {
        PosicionInicial();
        _ventana.Show();                      // se muestra siempre una vez (para tener hwnd y estilos); se oculta abajo si hace falta
        if (!_cfg.Visible) _ventana.Hide();

        bool atajoOk = _atajo.Registrar(_cfg.AtajoEfectivo);
        _bandeja.Actualizar(_cfg, _atajo.Actual);
        _timer.Start();

        if (!atajoOk)
            _bandeja.Globo("Atajo no disponible",
                $"No se pudo registrar {_cfg.AtajoEfectivo.Texto} (otro programa ya lo usa). La app sigue funcionando: " +
                "bloqueá y desbloqueá desde el ícono de la bandeja, o elegí otro atajo en el menú.", error: true);
        else if (!_cfg.AvisoInicialMostrado)
        {
            _bandeja.Globo("AMS2 Asistente de Frenado",
                $"El overlay está bloqueado: los clics lo atraviesan. El ícono está en la bandeja (en Windows 11 suele quedar " +
                $"oculto en la flecha ^ de la barra de tareas). Atajo para bloquear/desbloquear: {_cfg.AtajoEfectivo.Texto}.");
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
            if (v.Pitar) Pitar();
            if (v.Aviso is not null && v.Aviso != _ultimoAviso)
                _bandeja.Globo("Aviso", v.Aviso, error: true);
            _ultimoAviso = v.Aviso;

            if (_cfg.Visible)
            {
                var cuadro = _suavizador.Actualizar(v.Panel, t);
                _ventana.Pintar(v, cuadro, _cfg);
                if (t - _ultimaReafirmacion > 2) { _ultimaReafirmacion = t; _ventana.ReafirmarArriba(); }
            }
        }
        catch (Exception e)
        {
            // el overlay nunca debe tirar la app abajo en plena carrera
            Debug.WriteLine(e);
        }
    }

    private static void Pitar()
    {
        Task.Run(() => { try { Console.Beep(880, 120); } catch (Exception) { } });
    }

    // ---------------------------------------------------------------- config

    private void AplicarAlServicio()
    {
        _servicio.Modo = _cfg.Modo;
        _servicio.ReferenciaPrincipal = _cfg.ReferenciaPrincipal;
        _servicio.RojoSuave = _cfg.RojoSuave;
        _servicio.Pitido = _cfg.Pitido;
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
        if (aplicarBloqueo) _ventana.AplicarBloqueo(_cfg.Bloqueado, _cfg.AtajoEfectivo);
        if (_cfg.Visible && !_ventana.IsVisible) _ventana.Show();
        if (!_cfg.Visible && _ventana.IsVisible) _ventana.Hide();
        _bandeja.Actualizar(_cfg, _atajo.Actual);
        Guardar();
    }

    private void ConectarBandeja()
    {
        _bandeja.AlternarBloqueo += () => Alternar(() => _cfg.Bloqueado = !_cfg.Bloqueado, aplicarBloqueo: true);
        _bandeja.AlternarVisible += () => Alternar(() => _cfg.Visible = !_cfg.Visible);
        _bandeja.AlternarPitido += () => Alternar(() => _cfg.Pitido = !_cfg.Pitido);
        _bandeja.AlternarRojoSuave += () => Alternar(() => _cfg.RojoSuave = !_cfg.RojoSuave);
        _bandeja.AlternarMapa += () => Alternar(() => _cfg.MapaVisible = !_cfg.MapaVisible);
        _bandeja.AlternarMapaCompacto += () => Alternar(() => _cfg.MapaCompacto = !_cfg.MapaCompacto);
        _bandeja.ModoElegido += m => Alternar(() => _cfg.Modo = m);
        _bandeja.ReferenciaElegida += r => Alternar(() => _cfg.ReferenciaPrincipal = r);
        _bandeja.OpacidadElegida += a => Alternar(() => _cfg.Opacidad = a);
        _bandeja.EscalaElegida += s =>
        {
            Alternar(() => _cfg.Escala = s);
            _ventana.AplicarEscala(_cfg.Escala);
            if (_anclada) Anclar();
        };
        _bandeja.EsquinaElegida += e =>
        {
            Alternar(() => { _cfg.Esquina = e; _cfg.Posiciones.Remove(MonitorActual().clave); });
            _anclada = true;
            Anclar();
        };
        _bandeja.AtajoElegido += texto =>
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
        };
        _bandeja.AbrirCarpeta += () => _bandeja.AbrirEnExplorador(_almacen.RutaBase);
        _bandeja.AbrirDiagnostico += AbrirDiagnostico;
        _bandeja.Salir += Salir;
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
    private (string clave, AreaPantalla area) MonitorActual()
    {
        double sx = 1, sy = 1;
        try { var d = VisualTreeHelper.GetDpi(_ventana); sx = d.DpiScaleX; sy = d.DpiScaleY; } catch { }
        var scr = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        if (_ventana.IsVisible)
        {
            var centro = new System.Drawing.Point((int)((_ventana.Left + _ventana.ActualWidth / 2) * sx), (int)((_ventana.Top + _ventana.ActualHeight / 2) * sy));
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
        _ventana.Close();
        _lector.Dispose();
    }
}
