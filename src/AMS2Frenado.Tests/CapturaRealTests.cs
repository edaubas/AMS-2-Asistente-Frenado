using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>
/// Integracion con una captura real de AMS2 (Watkins Glen GP, GT3_Gen2, 48 participantes, ~27 Hz).
/// La ruta viene de la variable de entorno AMS2_CAPTURA_PRUEBA; si no esta definida o el archivo no
/// existe (CI) la prueba termina sin hacer nada. La captura NO esta en el repo.
/// Es evidencia de una captura real, no de una grabacion en vivo con la app: sin probar en AMS2 en vivo.
/// </summary>
public sealed class CapturaRealTests : IDisposable
{
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static string? Ruta()
    {
        var r = Environment.GetEnvironmentVariable("AMS2_CAPTURA_PRUEBA");
        return !string.IsNullOrWhiteSpace(r) && File.Exists(r) ? r : null;
    }

    [Fact]
    public void LaCapturaRealDaLasOchoZonasDeFrenadaEsperadas()
    {
        var ruta = Ruta();
        if (ruta is null) return;     // sin captura (CI): se salta

        var al = new Almacen(_dir);
        var g = new Grabador(al, 0);
        using (var rep = ReproductorCaptura.Abrir(ruta))
            while (true)
            {
                var r = rep.Leer();
                if (r.Estado == EstadoLectura.FinCaptura) break;
                g.Procesar(r, rep.SegundosActual);
            }
        g.Guardar();

        var estado = Assert.Single(g.Estados());
        Assert.Equal("GT3_Gen2", estado.Clase);
        Assert.True(estado.VueltasIa >= 6, $"vueltas validas de la IA: {estado.VueltasIa}");

        var datos = al.LeerClase(estado.Pista, estado.Variante, estado.Clase)!;
        // zonas de frenado esperadas (metros de distancia de vuelta, ~ punto de frenado)
        double[] esperadas = [150, 2050, 2550, 3100, 3800, 4150, 4650, 4900];
        foreach (double e in esperadas)
            Assert.Contains(datos.Curvas, c => c.Tipo == "frenada" && c.Ia is not null
                && Math.Abs(Geometria.DifCirc(c.Ia.FrenarEn, e, datos.Largo)) <= 70);
        Assert.Equal(8, datos.Curvas.Count(c => c.Tipo == "frenada" && c.Ia is not null));

        var mapa = al.LeerMapa(estado.Pista, estado.Variante)!;
        Assert.InRange(mapa.Puntos.Count, 1000, 1100);        // 5440 m / 5 m
    }
}
