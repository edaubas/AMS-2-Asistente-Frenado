using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AMS2Frenado.Core;

/// <summary>
/// Lee el bloque $pcars2$ y lo copia a <see cref="SharedMemory"/>.
/// Reglas (handover): si mVersion != 14 no se lee nada mas; si mSequenceNumber es impar
/// o cambia durante la copia, la lectura se descarta (se reintenta unas veces).
/// </summary>
public sealed class LectorTelemetria : IFuenteTelemetria
{
    private static readonly int OffsetSecuencia = (int)Marshal.OffsetOf<SharedMemory>(nameof(SharedMemory.mSequenceNumber));
    private const int Reintentos = 6;

    private readonly IBloqueMemoria _bloque;
    private readonly byte[] _buffer = new byte[ConstantesShm.TamanoBytes];

    public LectorTelemetria(IBloqueMemoria? bloque = null) => _bloque = bloque ?? new BloqueMmf();

    public ResultadoLectura Leer()
    {
        if (!_bloque.Abrir()) return ResultadoLectura.SinJuego;

        uint version = _bloque.LeerUInt(0);          // mVersion es el primer campo
        if (version == 0) return ResultadoLectura.SinJuego;   // bloque aun sin inicializar
        if (version != ConstantesShm.Version)
            return new(EstadoLectura.VersionDistinta, version, default);   // no leemos mas

        for (int i = 0; i < Reintentos; i++)
        {
            uint s1 = _bloque.LeerUInt(OffsetSecuencia);
            _bloque.Copiar(_buffer);
            uint s2 = _bloque.LeerUInt(OffsetSecuencia);
            if (s1 == s2 && (s1 & 1) == 0)
            {
                var datos = MemoryMarshal.Read<SharedMemory>(_buffer);
                // doble chequeo de la version ya copiada (por si cambio entre lecturas)
                if (datos.mVersion != ConstantesShm.Version)
                    return new(EstadoLectura.VersionDistinta, datos.mVersion, default);
                return new(EstadoLectura.Ok, version, datos);
            }
        }
        return new(EstadoLectura.Inestable, version, default);
    }

    public void Dispose() => _bloque.Dispose();
}
