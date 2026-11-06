using System.IO;
using System.Media;
using AMS2Frenado.Core.Overlay;

namespace AMS2Frenado.App;

/// <summary>
/// Pitido de FRENA. Reemplaza a Console.Beep, que en una app WinExe sin consola no es fiable: aca se genera un WAV
/// senoidal corto en memoria (Core, con pruebas) y se reproduce con <see cref="SoundPlayer"/> en un hilo aparte,
/// con <see cref="SystemSounds.Beep"/> como respaldo. Sin dependencias nativas nuevas. Compila, SIN EJECUTAR:
/// nunca se oyo en Windows. Un solo pitido a la vez: si uno suena todavia, el nuevo se descarta.
/// </summary>
internal sealed class Pitido
{
    private readonly byte[] _wav = GeneradorTono.Wav();
    private readonly object _candado = new();
    private bool _sonando;

    /// <summary>Devuelve enseguida; el sonido corre fuera del hilo de la interfaz.</summary>
    public void Reproducir()
    {
        lock (_candado)
        {
            if (_sonando) return;
            _sonando = true;
        }
        Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(_wav, writable: false);
                using var sp = new SoundPlayer(ms);
                sp.PlaySync();
            }
            catch (Exception)
            {
                try { SystemSounds.Beep.Play(); } catch (Exception) { /* sin audio: no pasa nada */ }
            }
            finally
            {
                lock (_candado) _sonando = false;
            }
        });
    }
}
