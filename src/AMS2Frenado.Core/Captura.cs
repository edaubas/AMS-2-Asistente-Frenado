using System.Runtime.InteropServices;
using System.Text;

namespace AMS2Frenado.Core;

/// <summary>
/// Formato de captura cruda (.ams2cap):
///   8 bytes "AMS2CAP1" | int32 version del struct | int32 tamano del struct
///   luego N registros: [double segundos desde el inicio][bytes del struct].
/// </summary>
internal static class FormatoCaptura
{
    public static readonly byte[] Magia = Encoding.ASCII.GetBytes("AMS2CAP1");
    public const int TamanoRegistro = sizeof(double) + ConstantesShm.TamanoBytes;
}

/// <summary>Guarda lecturas validas a un archivo. Solo escribe cuando cambia mSequenceNumber.</summary>
public sealed class GrabadorCaptura : IDisposable
{
    private readonly Stream _salida;
    private readonly bool _cerrarSalida;
    private uint _ultimaSecuencia = uint.MaxValue;
    private readonly byte[] _registro = new byte[FormatoCaptura.TamanoRegistro];

    public int Registros { get; private set; }

    public GrabadorCaptura(Stream salida, bool cerrarSalida = true)
    {
        _salida = salida;
        _cerrarSalida = cerrarSalida;
        _salida.Write(FormatoCaptura.Magia);
        Span<byte> cab = stackalloc byte[8];
        BitConverter.TryWriteBytes(cab[..4], (int)ConstantesShm.Version);
        BitConverter.TryWriteBytes(cab[4..], ConstantesShm.TamanoBytes);
        _salida.Write(cab);
    }

    public static GrabadorCaptura Crear(string ruta)
        => new(new FileStream(ruta, FileMode.Create, FileAccess.Write, FileShare.Read));

    /// <summary>Escribe la lectura; devuelve false si se omitio por repetida.</summary>
    public bool Escribir(in SharedMemory sm, double segundos)
    {
        if (sm.mSequenceNumber == _ultimaSecuencia) return false;
        _ultimaSecuencia = sm.mSequenceNumber;
        BitConverter.TryWriteBytes(_registro.AsSpan(0, sizeof(double)), segundos);
        MemoryMarshal.Write(_registro.AsSpan(sizeof(double)), in sm);
        _salida.Write(_registro);
        Registros++;
        return true;
    }

    public void Dispose()
    {
        _salida.Flush();
        if (_cerrarSalida) _salida.Dispose();
    }
}

/// <summary>Reproduce una captura como fuente de telemetria (para pruebas, sin el juego).</summary>
public sealed class ReproductorCaptura : IFuenteTelemetria
{
    private readonly Stream _entrada;
    private readonly byte[] _registro = new byte[FormatoCaptura.TamanoRegistro];

    /// <summary>Segundos (desde el inicio de la captura) del ultimo registro leido.</summary>
    public double SegundosActual { get; private set; }

    public ReproductorCaptura(Stream entrada)
    {
        _entrada = entrada;
        Span<byte> cab = stackalloc byte[16];
        _entrada.ReadExactly(cab);
        if (!cab[..8].SequenceEqual(FormatoCaptura.Magia))
            throw new InvalidDataException("No es una captura AMS2 (cabecera invalida).");
        int version = BitConverter.ToInt32(cab[8..12]);
        int tamano = BitConverter.ToInt32(cab[12..16]);
        if (version != ConstantesShm.Version || tamano != ConstantesShm.TamanoBytes)
            throw new InvalidDataException($"Captura de version {version} / {tamano} bytes; se espera {ConstantesShm.Version} / {ConstantesShm.TamanoBytes}.");
    }

    public static ReproductorCaptura Abrir(string ruta)
        => new(new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read));

    public ResultadoLectura Leer()
    {
        int n = _entrada.ReadAtLeast(_registro, _registro.Length, throwOnEndOfStream: false);
        if (n < _registro.Length) return ResultadoLectura.Fin;
        SegundosActual = BitConverter.ToDouble(_registro, 0);
        var sm = MemoryMarshal.Read<SharedMemory>(_registro.AsSpan(sizeof(double)));
        return new(EstadoLectura.Ok, sm.mVersion, sm);
    }

    public void Dispose() => _entrada.Dispose();
}
