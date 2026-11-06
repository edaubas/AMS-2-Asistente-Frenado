using System.Text;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>El WAV del pitido se arma en memoria (Core). Que suene con SoundPlayer compila pero no se ejecuto.</summary>
public sealed class PitidoTests
{
    [Fact]
    public void ElWavTieneCabeceraPcmMono16BitsCorrecta()
    {
        var w = GeneradorTono.Wav(880, 150, muestrasPorSegundo: 44100);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(w, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(w, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(w, 12, 4));
        Assert.Equal(16, BitConverter.ToInt32(w, 16));
        Assert.Equal(1, BitConverter.ToInt16(w, 20));          // PCM
        Assert.Equal(1, BitConverter.ToInt16(w, 22));          // mono
        Assert.Equal(44100, BitConverter.ToInt32(w, 24));
        Assert.Equal(88200, BitConverter.ToInt32(w, 28));
        Assert.Equal(2, BitConverter.ToInt16(w, 32));
        Assert.Equal(16, BitConverter.ToInt16(w, 34));
        Assert.Equal("data", Encoding.ASCII.GetString(w, 36, 4));
        int datos = BitConverter.ToInt32(w, 40);
        Assert.Equal(44 + datos, w.Length);
        Assert.Equal(36 + datos, BitConverter.ToInt32(w, 4));
        Assert.Equal(44100 * 150 / 1000 * 2, datos);
    }

    [Fact]
    public void ElTonoTieneLaFrecuenciaPedidaAmplitudAcotadaYRampasSinClic()
    {
        var w = GeneradorTono.Wav(880, 150, volumen: 0.5);
        int n = (w.Length - 44) / 2;
        var m = new short[n];
        Buffer.BlockCopy(w, 44, m, 0, n * 2);
        int max = m.Max(x => Math.Abs((int)x));
        Assert.InRange(max, 0.45 * short.MaxValue, 0.5 * short.MaxValue + 1);
        Assert.True(Math.Abs((int)m[0]) < 200 && Math.Abs((int)m[^1]) < 200, "empieza y termina en silencio (sin clic)");

        // cruces por cero en la zona central: 880 Hz -> 2 por ciclo
        int ini = n / 4, fin = 3 * n / 4, cruces = 0;
        for (int i = ini + 1; i < fin; i++) if ((m[i - 1] < 0) != (m[i] < 0)) cruces++;
        double hz = cruces / 2.0 / ((fin - ini) / 44100.0);
        Assert.InRange(hz, 860, 900);
    }

    [Theory]
    [InlineData(5, 150)]
    [InlineData(880, 1)]
    [InlineData(880, 99999)]
    public void ParametrosFueraDeRangoSeRechazan(int hz, int ms)
        => Assert.Throws<ArgumentOutOfRangeException>(() => GeneradorTono.Wav(hz, ms));

    [Fact]
    public void ElVolumenSeLimitaYElTonoPorDefectoEsCortoYAudible()
    {
        var w = GeneradorTono.Wav(volumen: 7);
        int n = (w.Length - 44) / 2; var m = new short[n]; Buffer.BlockCopy(w, 44, m, 0, n * 2);
        Assert.True(m.Max(x => (int)x) <= short.MaxValue);
        var d = GeneradorTono.Wav();
        Assert.Equal(GeneradorTono.FrecuenciaPorDefectoHz, 880);
        Assert.InRange((d.Length - 44) / 2 / 44100.0, 0.1, 0.25);
    }
}
