using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using AMS2Frenado.Core;

namespace AMS2Frenado.App;

/// <summary>
/// Ventana de diagnostico (fase 2): equivale a `dump` del prototipo. Lee ~60 Hz y refresca
/// el texto ~4 veces por segundo. Sin probar en AMS2.
/// </summary>
public partial class VentanaDiagnostico : Window
{
    private readonly LectorTelemetria _lector = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _reloj = Stopwatch.StartNew();
    private GrabadorCaptura? _grabador;
    private string? _rutaCaptura;
    private double _ultimaPantalla = -1;

    public VentanaDiagnostico()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Paso();
        _timer.Start();
    }

    private void Paso()
    {
        ResultadoLectura r;
        try { r = _lector.Leer(); }
        catch (Exception e) { r = ResultadoLectura.SinJuego; TextoEstado.Text = "Error de lectura: " + e.Message; }

        if (r.Estado == EstadoLectura.Ok && _grabador is not null)
        {
            _grabador.Escribir(r.Datos, _reloj.Elapsed.TotalSeconds);
            TextoCaptura.Text = $"Grabando: {_grabador.Registros} registros -> {_rutaCaptura}";
        }

        double t = _reloj.Elapsed.TotalSeconds;
        if (t - _ultimaPantalla < 0.25) return;
        _ultimaPantalla = t;

        switch (r.Estado)
        {
            case EstadoLectura.Ok:
                TextoEstado.Text = "Leyendo AMS2 (memoria compartida v14)";
                TextoDump.Text = InstantaneaDiagnostico.De(r.Datos).ATexto();
                break;
            case EstadoLectura.VersionDistinta:
                TextoEstado.Text = $"Version de memoria compartida {r.Version}: este programa lee la {ConstantesShm.Version}. No se lee nada mas.";
                TextoDump.Text = "";
                break;
            case EstadoLectura.Inestable:
                TextoEstado.Text = "Lectura inestable (el juego estaba escribiendo); reintentando.";
                break;
            default:
                TextoEstado.Text = "No detecto AMS2 (memoria compartida $pcars2$ no disponible). " +
                    "En AMS2: Options > System > Shared Memory = Project CARS 2, UDP Frequency = OFF.";
                TextoDump.Text = "";
                break;
        }
    }

    private void AlPulsarCaptura(object sender, RoutedEventArgs e)
    {
        if (_grabador is null)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AMS2AsistenteFrenado", "capturas");
            Directory.CreateDirectory(dir);
            _rutaCaptura = Path.Combine(dir, $"captura-{DateTime.Now:yyyyMMdd-HHmmss}.ams2cap");
            _grabador = GrabadorCaptura.Crear(_rutaCaptura);
            BotonCaptura.Content = "Parar captura";
            TextoCaptura.Text = "Grabando...";
        }
        else
        {
            PararCaptura();
        }
    }

    private void PararCaptura()
    {
        if (_grabador is null) return;
        int n = _grabador.Registros;
        _grabador.Dispose();
        _grabador = null;
        BotonCaptura.Content = "Empezar captura cruda";
        TextoCaptura.Text = $"Captura guardada: {n} registros en {_rutaCaptura}";
    }

    private void AlCerrar(object? sender, EventArgs e)
    {
        _timer.Stop();
        PararCaptura();
        _lector.Dispose();
    }
}
