using AMS2Frenado.Core;
using Xunit;

namespace AMS2Frenado.Tests;

public class CapturaTests
{
    [Fact]
    public void RoundTripGrabarYReproducir()
    {
        var ms = new MemoryStream();
        using (var g = new GrabadorCaptura(ms, cerrarSalida: false))
        {
            for (uint i = 0; i < 5; i++)
            {
                var sm = Muestras.Valida(secuencia: 2 + 2 * i);
                sm.mParticipantInfo[1].mCurrentLapDistance = 1000f + i;
                Assert.True(g.Escribir(sm, i / 60.0));
            }
            Assert.Equal(5, g.Registros);
        }
        ms.Position = 0;
        using var rep = new ReproductorCaptura(ms);
        for (int i = 0; i < 5; i++)
        {
            var r = rep.Leer();
            Assert.Equal(EstadoLectura.Ok, r.Estado);
            Assert.Equal(1000f + i, r.Datos.mParticipantInfo[1].mCurrentLapDistance);
            Assert.Equal("Silverstone", Texto.Leer(r.Datos.mTrackLocation));
            Assert.Equal(i / 60.0, rep.SegundosActual, 6);
        }
        Assert.Equal(EstadoLectura.FinCaptura, rep.Leer().Estado);
    }

    [Fact]
    public void NoGrabaSecuenciasRepetidas()
    {
        using var g = new GrabadorCaptura(new MemoryStream(), cerrarSalida: false);
        Assert.True(g.Escribir(Muestras.Valida(2), 0));
        Assert.False(g.Escribir(Muestras.Valida(2), 0.01));
        Assert.True(g.Escribir(Muestras.Valida(4), 0.02));
        Assert.Equal(2, g.Registros);
    }

    [Fact]
    public void CabeceraInvalidaLanza()
        => Assert.Throws<InvalidDataException>(() => new ReproductorCaptura(new MemoryStream(new byte[64])));

    [Fact]
    public void ReproductorPuedeAlimentarAlLectorComoFuente()
    {
        // una captura es una IFuenteTelemetria: sirve para inyectarla en pruebas
        var ms = new MemoryStream();
        using (var g = new GrabadorCaptura(ms, false)) g.Escribir(Muestras.Valida(), 0);
        ms.Position = 0;
        IFuenteTelemetria fuente = new ReproductorCaptura(ms);
        Assert.Equal(EstadoLectura.Ok, fuente.Leer().Estado);
        Assert.Equal(EstadoLectura.FinCaptura, fuente.Leer().Estado);
    }
}
