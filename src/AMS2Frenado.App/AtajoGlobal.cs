using System.Windows.Interop;
using AMS2Frenado.App.Interop;
using AMS2Frenado.Core.Overlay;

namespace AMS2Frenado.App;

/// <summary>
/// Atajo global (RegisterHotKey sin ventana: WM_HOTKEY llega a la cola del hilo de la interfaz).
/// Si el registro falla (otro programa ya lo tiene), <see cref="Registrar"/> devuelve false y la app sigue sin atajo.
/// Compila, SIN EJECUTAR.
/// </summary>
internal sealed class AtajoGlobal : IDisposable
{
    private const int Id = 0x4652;      // "FR"
    private bool _registrado;

    public event Action? Pulsado;
    public AtajoTeclado? Actual { get; private set; }

    public AtajoGlobal() => ComponentDispatcher.ThreadPreprocessMessage += AlMensaje;

    public bool Registrar(AtajoTeclado atajo)
    {
        Anular();
        try
        {
            _registrado = Win32.RegisterHotKey(IntPtr.Zero, Id, (uint)atajo.Modificadores | Win32.MOD_NOREPEAT, (uint)atajo.CodigoVirtual);
        }
        catch (Exception) { _registrado = false; }
        Actual = _registrado ? atajo : null;
        return _registrado;
    }

    private void Anular()
    {
        if (!_registrado) return;
        try { Win32.UnregisterHotKey(IntPtr.Zero, Id); } catch (Exception) { }
        _registrado = false;
        Actual = null;
    }

    private void AlMensaje(ref MSG msg, ref bool handled)
    {
        if (msg.message != Win32.WM_HOTKEY || (int)msg.wParam != Id) return;
        handled = true;
        Pulsado?.Invoke();
    }

    public void Dispose()
    {
        ComponentDispatcher.ThreadPreprocessMessage -= AlMensaje;
        Anular();
    }
}
