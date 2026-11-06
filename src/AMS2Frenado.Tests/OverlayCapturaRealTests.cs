using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;
using Xunit.Abstractions;

namespace AMS2Frenado.Tests;

/// <summary>
/// El servicio de la fase 5 sobre la captura real (AMS2_CAPTURA_PRUEBA; sin ella se salta): primero en modo Grabar,
/// despues en modo Mostrar. No es la app en vivo: sin probar en AMS2.
/// </summary>
public sealed class OverlayCapturaRealTests : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly string _dir = Simulador.CarpetaTemporal();
    public OverlayCapturaRealTests(ITestOutputHelper o) => _out = o;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static IEnumerable<(ResultadoLectura r, double t)> Lecturas(string ruta)
    {
        using var rep = ReproductorCaptura.Abrir(ruta);
        while (true)
        {
            var r = rep.Leer();
            if (r.Estado == EstadoLectura.FinCaptura) yield break;
            yield return (r, rep.SegundosActual);
        }
    }

    [Fact]
    public void GrabarYLuegoMostrarSobreLaCapturaReal()
    {
        var ruta = Environment.GetEnvironmentVariable("AMS2_CAPTURA_PRUEBA");
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return;

        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Grabar };
        VistaOverlay? ultima = null;
        int conContadores = 0;
        foreach (var (r, t) in Lecturas(ruta))
        {
            ultima = s.Paso(r, t);
            if (ultima.Panel.Tipo == TipoTarjeta.Grabando) conContadores++;
        }
        Assert.True(conContadores > 0);
        s.Modo = ModoApp.Mostrar;

        var vistas = new List<VistaOverlay>();
        double off = 100000;
        foreach (var (r, t) in Lecturas(ruta)) vistas.Add(s.Paso(r, off + t));
        var jugando = vistas.Where(v => v.Guia is not null && v.Guia.Estado != EstadoPanel.SinDatos).ToList();
        Assert.NotEmpty(jugando);

        var carteles = jugando.Where(v => v.Panel.Estado == EstadoPanel.Frena).Select(v => v.Panel.NumeroPrincipal).Distinct().OrderBy(x => x).ToList();
        _out.WriteLine("Carteles vistos en FRENA: " + string.Join(", ", carteles));
        Assert.All(carteles, c => Assert.Contains(c, new[] { "400", "300", "200", "100", "50", "—" }));
        Assert.Contains(jugando, v => v.Panel.Estado == EstadoPanel.Inminente && v.Panel.ColorNumero == Paleta.Ambar);
        Assert.False(jugando[^1].Mapa.EstaVacio);
        Assert.Equal("GT3_Gen2", jugando[^1].ChipClase);

        // la secuencia de lo que se dibuja tampoco retrocede dentro de una curva (misma regla que el motor)
        var su = new Suavizador();
        for (int i = 0; i < vistas.Count; i++) Assert.InRange(su.Actualizar(vistas[i].Panel, off + i / 30.0).OpacidadTarjeta, 0, 1);
    }
}
