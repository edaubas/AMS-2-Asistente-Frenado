using System.Threading;
using System.Windows;

namespace AMS2Frenado.App;

public partial class App : Application
{
    private Mutex? _unica;
    private bool _poseeMutex;
    private ControladorApp? _controlador;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Any(a => string.Equals(a, Diag.Argumento, StringComparison.OrdinalIgnoreCase))) Diag.Registro.Activo = true;
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
            Diag.Registro.Escribir("error", "no atrapado (cierra el proceso): " + (ev.ExceptionObject as Exception)?.GetType().Name + ": " + (ev.ExceptionObject as Exception)?.Message);

        // una sola instancia: dos copias se pelearian por el atajo y por la bandeja
        _unica = new Mutex(true, @"Local\AMS2AsistenteFrenado-instancia", out bool primera);
        _poseeMutex = primera;
        Diag.Arranque(e.Args, primera);
        if (!primera)
        {
            MessageBox.Show("AMS2 Asistente de Frenado ya está en ejecución (buscá el ícono en la bandeja).",
                "AMS2 Asistente de Frenado", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ev) =>
        {
            System.Diagnostics.Debug.WriteLine(ev.Exception);
            Diag.Registro.Error("DispatcherUnhandledException", ev.Exception);
            ev.Handled = true;      // un error de dibujo no debe cerrar el overlay en plena carrera
        };

        _controlador = new ControladorApp();
        Diag.Registro.Escribir("arranque", "ControladorApp creado");
        _controlador.Iniciar();
        Diag.Registro.Escribir("arranque", "Iniciar() terminó");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Diag.Registro.Escribir("salida", "OnExit");
        _controlador?.Dispose();
        if (_poseeMutex) _unica?.ReleaseMutex();
        _unica?.Dispose();
        base.OnExit(e);
    }
}
