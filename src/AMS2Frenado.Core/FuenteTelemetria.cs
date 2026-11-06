namespace AMS2Frenado.Core;

public enum EstadoLectura
{
    /// <summary>El juego no publico la memoria compartida (no esta corriendo o no esta activada).</summary>
    SinJuego,
    /// <summary>mVersion distinta de 14: no se leyo nada mas que la version.</summary>
    VersionDistinta,
    /// <summary>mSequenceNumber impar o cambiado durante la copia: lectura descartada.</summary>
    Inestable,
    /// <summary>Lectura valida en <see cref="ResultadoLectura.Datos"/>.</summary>
    Ok,
    /// <summary>Solo para reproductores: no quedan mas registros.</summary>
    FinCaptura,
}

public readonly record struct ResultadoLectura(EstadoLectura Estado, uint Version, SharedMemory Datos)
{
    public static ResultadoLectura SinJuego => new(EstadoLectura.SinJuego, 0, default);
    public static ResultadoLectura Fin => new(EstadoLectura.FinCaptura, 0, default);
}

/// <summary>Fuente de telemetria: el juego real, una captura grabada o un simulador.</summary>
public interface IFuenteTelemetria : IDisposable
{
    ResultadoLectura Leer();
}

/// <summary>
/// Acceso de bajo nivel a un bloque de memoria compartida (o su sustituto en pruebas).
/// Existe para poder probar la logica de version/secuencia sin Windows ni el juego.
/// </summary>
public interface IBloqueMemoria : IDisposable
{
    /// <summary>Intenta abrir el bloque; false si todavia no existe.</summary>
    bool Abrir();
    /// <summary>Lee un uint en el offset dado (version, secuencia).</summary>
    uint LeerUInt(int offset);
    /// <summary>Copia los primeros destino.Length bytes del bloque.</summary>
    void Copiar(Span<byte> destino);
}
