using System.Text;

namespace AMS2Frenado.Core.Overlay;

/// <summary>
/// Genera en memoria un WAV (PCM 16 bits, mono) con un tono senoidal corto, para el pitido. Asi no hace falta
/// ningun archivo de sonido ni dependencia nativa: la App lo reproduce con <c>System.Media.SoundPlayer</c>.
/// Entrada y salida con una rampa corta para evitar el "clic" al empezar y terminar.
/// </summary>
public static class GeneradorTono
{
    public const int FrecuenciaPorDefectoHz = 880;
    public const int DuracionPorDefectoMs = 150;

    public static byte[] Wav(int frecuenciaHz = FrecuenciaPorDefectoHz, int duracionMs = DuracionPorDefectoMs,
        double volumen = 0.6, int muestrasPorSegundo = 44100, int rampaMs = 8)
    {
        if (frecuenciaHz is < 20 or > 20000) throw new ArgumentOutOfRangeException(nameof(frecuenciaHz));
        if (duracionMs is < 10 or > 5000) throw new ArgumentOutOfRangeException(nameof(duracionMs));
        if (muestrasPorSegundo is < 8000 or > 96000) throw new ArgumentOutOfRangeException(nameof(muestrasPorSegundo));
        volumen = Math.Clamp(volumen, 0, 1);

        int n = (int)((long)muestrasPorSegundo * duracionMs / 1000);
        int rampa = Math.Min(n / 2, (int)((long)muestrasPorSegundo * rampaMs / 1000));
        int bytesDatos = n * 2;

        using var ms = new MemoryStream(44 + bytesDatos);
        using (var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            w.Write("RIFF"u8);
            w.Write(36 + bytesDatos);
            w.Write("WAVE"u8);
            w.Write("fmt "u8);
            w.Write(16);                          // largo del bloque fmt
            w.Write((short)1);                    // PCM
            w.Write((short)1);                    // mono
            w.Write(muestrasPorSegundo);
            w.Write(muestrasPorSegundo * 2);      // bytes por segundo
            w.Write((short)2);                    // alineacion de bloque
            w.Write((short)16);                   // bits por muestra
            w.Write("data"u8);
            w.Write(bytesDatos);
            for (int i = 0; i < n; i++)
            {
                double env = 1;
                if (rampa > 0)
                {
                    if (i < rampa) env = (double)i / rampa;
                    else if (i >= n - rampa) env = (double)(n - 1 - i) / rampa;
                }
                double v = Math.Sin(2 * Math.PI * frecuenciaHz * i / muestrasPorSegundo) * volumen * env;
                w.Write((short)Math.Round(v * short.MaxValue));
            }
        }
        return ms.ToArray();
    }
}
