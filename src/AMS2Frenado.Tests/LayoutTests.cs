using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AMS2Frenado.Core;
using Xunit;

namespace AMS2Frenado.Tests;

public class LayoutTests
{
    [Fact]
    public void TamanoDelStructEs20700() => Assert.Equal(20700, Unsafe.SizeOf<SharedMemory>());

    [Fact]
    public void MarshalSizeOfCoincide()
    {
        Assert.Equal(20700, Marshal.SizeOf<SharedMemory>());
        Assert.Equal(100, Marshal.SizeOf<ParticipanteInfo>());
    }

    [Fact]
    public void LaCantidadDeCamposCoincideConElPrototipo()
    {
        // 158 campos de SharedMemory + 8 de ParticipanteInfo + 2 tamanos = 168 comprobaciones del prototipo
        Assert.Equal(158, typeof(SharedMemory).GetFields().Length);
        Assert.Equal(8, typeof(ParticipanteInfo).GetFields().Length);
    }

    [Fact]
    public void TodosLosOffsetsDeSharedMemoryCoinciden()
    {
        Assert.Equal(OffsetsEsperados.SharedMemory.Length, typeof(SharedMemory).GetFields().Length);
        foreach (var (campo, offset, _) in OffsetsEsperados.SharedMemory)
            Assert.True(offset == (int)Marshal.OffsetOf<SharedMemory>(campo), $"offset de {campo}");
    }

    [Fact]
    public void TodosLosOffsetsDeParticipanteCoinciden()
    {
        foreach (var (campo, offset, _) in OffsetsEsperados.Participante)
            Assert.True(offset == (int)Marshal.OffsetOf<ParticipanteInfo>(campo), $"offset de {campo}");
    }

    [Fact]
    public void OffsetsClave()
    {
        Assert.Equal(0, (int)Marshal.OffsetOf<SharedMemory>("mVersion"));
        Assert.Equal(28, (int)Marshal.OffsetOf<SharedMemory>("mParticipantInfo"));
        Assert.Equal(OffsetsEsperados.SharedMemory.First(c => c.Campo == "mSequenceNumber").Offset,
            (int)Marshal.OffsetOf<SharedMemory>("mSequenceNumber"));
    }

    [Fact]
    public void ElOffsetEnMemoriaRealCoincideConMarshal()
    {
        // verifica que el layout real (no solo el marshal) pone mSequenceNumber donde dice la tabla
        var sm = new SharedMemory { mSequenceNumber = 0xA1B2C3D4 };
        var bytes = MemoryMarshal.AsBytes(new Span<SharedMemory>(ref sm));
        int off = OffsetsEsperados.SharedMemory.First(c => c.Campo == "mSequenceNumber").Offset;
        Assert.Equal(0xA1B2C3D4u, BitConverter.ToUInt32(bytes.Slice(off, 4)));
    }
}
