using System.Runtime.InteropServices;
using System.Text;
using AMS2Frenado.Core;

namespace AMS2Frenado.Tests;

/// <summary>Bloque de memoria en un byte[] con ganchos para simular al juego escribiendo.</summary>
internal sealed class BloqueFalso : IBloqueMemoria
{
    public byte[] Bytes;
    public bool Existe = true;
    /// <summary>Se llama en cada Copiar (para cambiar la secuencia "durante la copia").</summary>
    public Action<byte[]>? AlCopiar;
    public int Copias;

    public BloqueFalso(in SharedMemory sm) => Bytes = Serializar(sm);

    public static byte[] Serializar(in SharedMemory sm)
    {
        var b = new byte[ConstantesShm.TamanoBytes];
        MemoryMarshal.Write(b, in sm);
        return b;
    }

    public bool Abrir() => Existe;
    public uint LeerUInt(int offset) => BitConverter.ToUInt32(Bytes, offset);
    public void Copiar(Span<byte> destino)
    {
        Copias++;
        Bytes.AsSpan(0, destino.Length).CopyTo(destino);
        AlCopiar?.Invoke(Bytes);
    }
    public void Dispose() { }
}

internal static class Muestras
{
    public static void Texto(Span<byte> destino, string s)
    {
        destino.Clear();
        Encoding.UTF8.GetBytes(s, destino[..^1]);
    }

    /// <summary>Lectura valida v14 con una pista y 3 participantes.</summary>
    public static SharedMemory Valida(uint secuencia = 2)
    {
        var sm = new SharedMemory
        {
            mVersion = ConstantesShm.Version,
            mSequenceNumber = secuencia,
            mGameState = 2,
            mViewedParticipantIndex = 0,
            mNumParticipants = 3,
            mTrackLength = 5891f,
            mSpeed = 50f,
            mBrake = 0.4f,
            mThrottle = 0.1f,
            mGear = 3,
        };
        Texto(sm.mTrackLocation, "Silverstone");
        Texto(sm.mTrackVariation, "Grand Prix");
        Texto(sm.mCarName, "Mclaren 720S GT3");
        Texto(sm.mCarClassName, "GT3");
        string[] nombres = ["Yo", "Piloto IA 1", "Piloto IA 2"];
        for (int i = 0; i < 3; i++)
        {
            sm.mParticipantInfo[i].mIsActive = 1;
            Texto(sm.mParticipantInfo[i].mName, nombres[i]);
            sm.mParticipantInfo[i].mCurrentLapDistance = 100f * (i + 1);
            sm.mParticipantInfo[i].mRacePosition = (uint)(i + 1);
            sm.mParticipantInfo[i].mLapsCompleted = 2;
            sm.mSpeeds[i] = 40f + i;
            Texto(sm.mCarClassNames[i], i == 2 ? "GT4" : "GT3");
        }
        return sm;
    }
}
