using System.IO.MemoryMappedFiles;

namespace AMS2Frenado.Core;

/// <summary>
/// Abre "$pcars2$" en SOLO LECTURA con MemoryMappedFile.OpenExisting. Solo funciona en Windows.
/// Sin probar en AMS2.
/// </summary>
public sealed unsafe class BloqueMmf : IBloqueMemoria
{
    private readonly string _nombre;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _vista;
    private byte* _ptr;

    public BloqueMmf(string nombre = ConstantesShm.NombreMapa) => _nombre = nombre;

    public bool Abrir()
    {
        if (_ptr != null) return true;
        try
        {
#pragma warning disable CA1416 // solo Windows; en otros SO lanza y se captura abajo
            _mmf = MemoryMappedFile.OpenExisting(_nombre, MemoryMappedFileRights.Read);
#pragma warning restore CA1416
            _vista = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            byte* p = null;
            _vista.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
            // el puntero ya incluye el offset del inicio de pagina de la vista
            _ptr = p + _vista.PointerOffset;
            if (_vista.Capacity < ConstantesShm.TamanoBytes)
            {
                // El bloque es mas chico que el struct: no es el de AMS2 v14.
                Cerrar();
                return false;
            }
            return true;
        }
        catch (Exception e) when (e is FileNotFoundException or PlatformNotSupportedException
                                      or UnauthorizedAccessException or IOException
                                      or ArgumentException or NotSupportedException)
        {
            Cerrar();
            return false;
        }
    }

    public uint LeerUInt(int offset) => Volatile.Read(ref *(uint*)(_ptr + offset));

    public void Copiar(Span<byte> destino)
        => new ReadOnlySpan<byte>(_ptr, destino.Length).CopyTo(destino);

    private void Cerrar()
    {
        if (_ptr != null) _vista?.SafeMemoryMappedViewHandle.ReleasePointer();
        _ptr = null;
        _vista?.Dispose(); _vista = null;
        _mmf?.Dispose(); _mmf = null;
    }

    public void Dispose() => Cerrar();
}
