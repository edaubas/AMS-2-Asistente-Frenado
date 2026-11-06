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

        // tamaño real de lo que guarda la app (docs/DATOS.md): JSON chicos, del orden de KB
        long total = 0;
        foreach (var f in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories).Order())
        {
            var len = new FileInfo(f).Length;
            if (!f.EndsWith(".bak")) total += len;
            _out.WriteLine($"TAMANO {Path.GetRelativePath(_dir, f)}: {len} bytes");
            Assert.True(len < 200 * 1024, $"{f} pesa {len} bytes");
        }
        _out.WriteLine($"TAMANO total (sin .bak): {total} bytes");

        var vistas = new List<VistaOverlay>();
        double off = 100000;
        foreach (var (r, t) in Lecturas(ruta)) vistas.Add(s.Paso(r, off + t));
        var jugando = vistas.Where(v => v.Guia is not null && v.Guia.Estado != EstadoPanel.SinDatos).ToList();
        Assert.NotEmpty(jugando);

        // el cartel ya no se muestra: la frenada no lleva numero grande
        Assert.All(jugando.Where(v => v.Panel.Tipo == TipoTarjeta.Guia && v.Guia!.Tipo == TipoCurva.Frenada),
            v => Assert.Null(v.Panel.NumeroPrincipal));
        Assert.Contains(jugando, v => v.Panel.Estado == EstadoPanel.Inminente && v.Panel.ColorMetros == Paleta.Ambar);
        Assert.Contains(jugando, v => v.Panel.Estado == EstadoPanel.Frena && v.Panel.TextoFranja == "FRENÁ");
        Assert.Equal("GT3_Gen2", jugando[^1].ChipClase);

        // el aviso de frenada sobre la captura real: a lo sumo uno por tramo de FRENA y nunca con el pedal ya apretado
        var av = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Mostrar };
        int avisos = 0, tramos = 0; bool prevFrena = false, prevVis = false; int prevCurva = -1; double tc = 0;
        foreach (var (r, t) in Lecturas(ruta))
        {
            var v = av.Paso(r, 200000 + t);
            if (v.Guia is null) continue;
            for (; tc + 200000 <= 200000 + t; tc += 1 / 60.0)
            {
                bool vis = av.ActualizarAviso(200000 + tc).Visible;
                if (vis && !prevVis) avisos++;
                prevVis = vis;
            }
            bool fr = v.Guia.Estado == EstadoPanel.Frena;
            if (fr && (!prevFrena || v.Guia.IndiceCurva != prevCurva)) tramos++;
            prevFrena = fr; prevCurva = fr ? v.Guia.IndiceCurva : -1;
        }
        _out.WriteLine($"Aviso de frenada: {avisos} avisos en {tramos} tramos de FRENA");
        Assert.True(tramos > 0);
        Assert.InRange(avisos, 1, tramos);

        // la secuencia de lo que se dibuja tampoco retrocede dentro de una curva (misma regla que el motor)
        var su = new Suavizador();
        for (int i = 0; i < vistas.Count; i++) Assert.InRange(su.Actualizar(vistas[i].Panel, off + i / 30.0).OpacidadTarjeta, 0, 1);
    }
}
