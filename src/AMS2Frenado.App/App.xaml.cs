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

        // una sola instancia: dos copias se pelearian por el atajo y por la bandeja
        _unica = new Mutex(true, @"Local\AMS2AsistenteFrenado-instancia", out bool primera);
        _poseeMutex = primera;
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
            ev.Handled = true;      // un error de dibujo no debe cerrar el overlay en plena carrera
        };

        _controlador = new ControladorApp();
        _controlador.Iniciar();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controlador?.Dispose();
        if (_poseeMutex) _unica?.ReleaseMutex();
        _unica?.Dispose();
        base.OnExit(e);
    }
}
