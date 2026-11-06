using System.Runtime.InteropServices;
using AMS2Frenado.Core;
using Xunit;

namespace AMS2Frenado.Tests;

public class LectorTests
{
    private static int OffSecuencia => (int)Marshal.OffsetOf<SharedMemory>(nameof(SharedMemory.mSequenceNumber));

    [Fact]
    public void LecturaValidaDevuelveLosDatos()
    {
        var lector = new LectorTelemetria(new BloqueFalso(Muestras.Valida()));
        var r = lector.Leer();
        Assert.Equal(EstadoLectura.Ok, r.Estado);
        Assert.Equal("Silverstone", Texto.Leer(r.Datos.mTrackLocation));
        Assert.Equal(5891f, r.Datos.mTrackLength);
        Assert.Equal(100f, r.Datos.mParticipantInfo[0].mCurrentLapDistance);
    }

    [Fact]
    public void SinBloqueDevuelveSinJuego()
    {
        var b = new BloqueFalso(Muestras.Valida()) { Existe = false };
        Assert.Equal(EstadoLectura.SinJuego, new LectorTelemetria(b).Leer().Estado);
    }

    [Fact]
    public void VersionCeroEsSinJuego()
    {
        var sm = Muestras.Valida(); sm.mVersion = 0;
        Assert.Equal(EstadoLectura.SinJuego, new LectorTelemetria(new BloqueFalso(sm)).Leer().Estado);
    }

    [Fact]
    public void VersionDistintaNoLeeNadaMas()
    {
        var sm = Muestras.Valida(); sm.mVersion = 13;
        var b = new BloqueFalso(sm);
        var r = new LectorTelemetria(b).Leer();
        Assert.Equal(EstadoLectura.VersionDistinta, r.Estado);
        Assert.Equal(13u, r.Version);
        Assert.Equal(0, b.Copias);                       // no copio el bloque
        Assert.Equal(0f, r.Datos.mTrackLength);          // ni devuelve datos
    }

    [Fact]
    public void SecuenciaImparSeDescarta()
    {
        var b = new BloqueFalso(Muestras.Valida(secuencia: 3));
        var r = new LectorTelemetria(b).Leer();
        Assert.Equal(EstadoLectura.Inestable, r.Estado);
        Assert.True(b.Copias > 1);   // reintento
    }

    [Fact]
    public void SecuenciaQueCambiaDuranteLaCopiaSeReintenta()
    {
        var b = new BloqueFalso(Muestras.Valida(secuencia: 2));
        // la primera copia "pisa" la secuencia (el juego escribio a mitad de la copia)
        b.AlCopiar = bytes =>
        {
            if (b.Copias == 1) BitConverter.TryWriteBytes(bytes.AsSpan(OffSecuencia), 4u);
        };
        var r = new LectorTelemetria(b).Leer();
        Assert.Equal(EstadoLectura.Ok, r.Estado);
        Assert.Equal(2, b.Copias);
        Assert.Equal(4u, r.Datos.mSequenceNumber);
    }

    [Fact]
    public void SecuenciaQueCambiaSiempreEsInestable()
    {
        var b = new BloqueFalso(Muestras.Valida(secuencia: 2));
        uint n = 2;
        b.AlCopiar = bytes => BitConverter.TryWriteBytes(bytes.AsSpan(OffSecuencia), n += 2);
        Assert.Equal(EstadoLectura.Inestable, new LectorTelemetria(b).Leer().Estado);
    }

    [Fact]
    public void BloqueMmfSinJuegoDevuelveSinJuego()
    {
        // En Linux/CI (o Windows sin AMS2) no existe $pcars2$: no debe lanzar excepcion.
        using var lector = new LectorTelemetria(new BloqueMmf("$pcars2$_inexistente_prueba"));
        Assert.Equal(EstadoLectura.SinJuego, lector.Leer().Estado);
    }

    [Fact]
    public void DiagnosticoResumeLaLectura()
    {
        var d = InstantaneaDiagnostico.De(Muestras.Valida());
        Assert.Equal("Silverstone", d.Pista);
        Assert.Equal("Grand Prix", d.Variante);
        Assert.Equal("GT3", d.Clase);
        Assert.Equal(3, d.Participantes.Count);
        Assert.Equal(50f * 3.6f, d.Participantes[0].VelocidadKmh, 3);   // el visto usa mSpeed
        Assert.Equal(41f * 3.6f, d.Participantes[1].VelocidadKmh, 3);   // la IA usa mSpeeds[i]
        Assert.Equal("GT4", d.Participantes[2].Clase);
        Assert.Contains("Silverstone", d.ATexto());
    }

    [Fact]
    public void LosArraysDeTextoYParticipantesCaenEnSuOffset()
    {
        // layout real en memoria, no solo Marshal: un campo del final y uno del medio
        var sm = Muestras.Valida();
        var bytes = BloqueFalso.Serializar(sm);
        int offTrack = (int)Marshal.OffsetOf<SharedMemory>(nameof(SharedMemory.mTrackLocation));
        Assert.Equal("Silverstone", Texto.Leer(bytes.AsSpan(offTrack, 64)));
        int offSpeeds = (int)Marshal.OffsetOf<SharedMemory>(nameof(SharedMemory.mSpeeds));
        Assert.Equal(41f, BitConverter.ToSingle(bytes, offSpeeds + 4));
        int offClases = (int)Marshal.OffsetOf<SharedMemory>(nameof(SharedMemory.mCarClassNames));
        Assert.Equal("GT4", Texto.Leer(bytes.AsSpan(offClases + 2 * 64, 64)));
    }
}
