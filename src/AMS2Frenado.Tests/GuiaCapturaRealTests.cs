using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using Xunit;
using Xunit.Abstractions;

namespace AMS2Frenado.Tests;

/// <summary>
/// Integracion de la guia con la captura real de AMS2 (Watkins Glen GP, GT3_Gen2). Ruta en la variable de
/// entorno AMS2_CAPTURA_PRUEBA; sin ella (CI) la prueba se salta sin fallar. La captura NO esta en el repo.
/// Es un recorrido de una captura, no la app en vivo: sin probar en AMS2.
/// </summary>
public sealed class GuiaCapturaRealTests : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly string _dir = Simulador.CarpetaTemporal();
    public GuiaCapturaRealTests(ITestOutputHelper o) => _out = o;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static string? Ruta()
    {
        var r = Environment.GetEnvironmentVariable("AMS2_CAPTURA_PRUEBA");
        return !string.IsNullOrWhiteSpace(r) && File.Exists(r) ? r : null;
    }

    private sealed class Pasada
    {
        public string Nombre = "";
        public MotorGuia Motor = null!;
        public readonly List<(double t, double d, PanelGuia p)> Panel = [];
    }

    private static int Nivel(EstadoPanel e) => e switch
    {
        EstadoPanel.Lejos => 0, EstadoPanel.Aproximacion => 1, EstadoPanel.Inminente => 2, EstadoPanel.Frena => 3, _ => 1,
    };

    [Fact]
    public void LaGuiaRecorreLasOchoCurvasDeWatkinsGlenSinParpadear()
    {
        var ruta = Ruta();
        if (ruta is null) return;      // sin captura (CI): se salta

        // 1) grabar curvas con el grabador de la fase 3
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
        var datos = al.LeerClase(estado.Pista, estado.Variante, estado.Clase)!;
        var mapa = al.LeerMapa(estado.Pista, estado.Variante);
        // Las ocho zonas de frenada de la pista salen de la IA (muchas vueltas); las demas curvas son "extras"
        // solo tuyas (sin bloque "ia", ver CapturaRealTests.VerificarCurvas). La guia, si a la curva le falta la
        // referencia pedida, usa la otra: por eso tambien guia en los extras de frenada. Esos extras son
        // las curvas de tipo frenada que no son de las 8 zonas.
        CapturaRealTests.VerificarCurvas(datos, estado.VueltasVos);
        var frenadas = datos.Curvas.Where(c => c.Tipo == "frenada").ToList();
        Assert.Equal(CapturaRealTests.ZonasFrenada.Length, frenadas.Count(c => c.Ia is not null));

        // 2) recorrer la captura por la guia: auto visto (referencia IA y vos) y una IA
        var pasadas = new List<Pasada>
        {
            new() { Nombre = "visto/ref IA", Motor = Nuevo(datos, mapa, ReferenciaGuia.Ia) },
            new() { Nombre = "visto/ref vos", Motor = Nuevo(datos, mapa, ReferenciaGuia.Vos) },
        };
        var autosIa = new Pasada?[64];       // una guia por cada auto de la IA
        using (var rep = ReproductorCaptura.Abrir(ruta))
            while (true)
            {
                var r = rep.Leer();
                if (r.Estado == EstadoLectura.FinCaptura) break;
                if (r.Estado != EstadoLectura.Ok) continue;
                var sm = r.Datos;
                if (sm.mGameState != 2 || sm.mTrackLength < 100) continue;
                int visto = sm.mViewedParticipantIndex;
                int n = Math.Clamp(sm.mNumParticipants, 0, 64);
                double t = rep.SegundosActual;
                Alimentar(pasadas[0], sm, visto, true, t);
                Alimentar(pasadas[1], sm, visto, true, t);
                for (int i = 0; i < n; i++)
                {
                    if (i == visto || sm.mParticipantInfo[i].mIsActive == 0) continue;
                    var pa = autosIa[i] ??= new Pasada { Nombre = $"IA #{i}", Motor = Nuevo(datos, mapa, ReferenciaGuia.Ia) };
                    Alimentar(pa, sm, i, false, t);
                }
            }

        // todas las IA juntas: cada auto tiene su propia guia; se agrupan para el resumen
        var iaTodas = autosIa.Where(a => a is not null).Select(a => a!).Where(a => a.Panel.Count > 0).ToList();
        Assert.True(iaTodas.Count >= 20, $"autos de la IA: {iaTodas.Count}");
        var conFrenaIa = new HashSet<int>();
        int pasadasIa = 0;
        foreach (var pa in iaTodas)
            foreach (var corrida in Corridas(pa.Panel))
            {
                var sec = new List<EstadoPanel>();
                foreach (var x in corrida) if (sec.Count == 0 || sec[^1] != x.p.Estado) sec.Add(x.p.Estado);
                for (int i = 1; i < sec.Count; i++)
                    Assert.True(Nivel(sec[i]) > Nivel(sec[i - 1]),
                        $"{pa.Nombre} curva {corrida[0].p.NombreCurva}: parpadeo {string.Join("->", sec)} (t={corrida[0].t:0.0})");
                if (sec.Contains(EstadoPanel.Frena)) { pasadasIa++; conFrenaIa.Add(corrida[0].p.IndiceCurva); }
            }
        _out.WriteLine($"IA: {iaTodas.Count} autos, {pasadasIa} pasadas con Frena, curvas distintas {conFrenaIa.Count}");
        Assert.Equal(frenadas.Count, conFrenaIa.Count);

        // 3) por cada curva y cada pasada, la secuencia de estados es monotonica y sin repetidos
        foreach (var pa in pasadas)
        {
            Assert.NotEmpty(pa.Panel);
            var conFrena = new HashSet<int>();
            int pasos = 0;
            foreach (var corrida in Corridas(pa.Panel))
            {
                var sec = new List<EstadoPanel>();
                foreach (var x in corrida) if (sec.Count == 0 || sec[^1] != x.p.Estado) sec.Add(x.p.Estado);
                for (int i = 1; i < sec.Count; i++)
                    Assert.True(Nivel(sec[i]) > Nivel(sec[i - 1]),
                        $"{pa.Nombre} curva {corrida[0].p.NombreCurva}: parpadeo {string.Join("->", sec)} (t={corrida[0].t:0.0})");
                if (sec.Contains(EstadoPanel.Frena)) { pasos++; conFrena.Add(corrida[0].p.IndiceCurva); }
            }
            _out.WriteLine($"{pa.Nombre}: {pasos} pasadas con Frena, curvas distintas {conFrena.Count}");
            Assert.True(conFrena.Count >= frenadas.Count, $"{pa.Nombre}: curvas con Frena {conFrena.Count} de {frenadas.Count}");
        }

        // 4) tabla de la primera pasada completa por curva (auto visto, referencia IA)
        _out.WriteLine($"Curvas: {datos.Curvas.Count}, largo {datos.Largo} m, carteles {(mapa?.Carteles is null ? "por defecto" : string.Join(",", mapa.Carteles))}");
        foreach (var pa in pasadas)
        {
            _out.WriteLine($"== {pa.Nombre}");
            var vistas = new HashSet<int>();
            foreach (var corrida in Corridas(pa.Panel))
            {
                int k = corrida[0].p.IndiceCurva;
                var inicios = new List<string>();
                var vis = new HashSet<EstadoPanel>();
                foreach (var x in corrida)
                    if (vis.Add(x.p.Estado)) inicios.Add($"{x.p.Estado}@{x.d:0}m/{x.p.VentanaM:0}");
                var f = corrida.FirstOrDefault(x => x.p.Estado is EstadoPanel.Frena or EstadoPanel.Toque or EstadoPanel.Levanta).p;
                if (f is null || !vistas.Add(k)) continue;
                _out.WriteLine($"{f.NombreCurva,-9} {f.Tipo,-8} cartel principal={f.CartelPrincipal} IA={f.CartelIa} vos={f.CartelVos} sec={f.CartelSecundario?.ToString() ?? "-"} vmin={f.VelMinPrincipalKmh} km/h {f.Principal}  {string.Join("  ", inicios)}");
            }
        }
        foreach (var c in datos.Curvas)
            _out.WriteLine($"{c.Nombre,-9} {c.Tipo,-8} vertice {c.Vertice,7:0.0}  IA frenar {c.Ia?.FrenarEn:0.0}→{c.Ia?.Vertice:0.0} ({(c.Ia is null ? 0 : Geometria.Mod(c.Ia.Vertice - c.Ia.FrenarEn, datos.Largo)):0} m)  vos frenar {c.Vos?.FrenarEn:0.0}→{c.Vos?.Vertice:0.0} ({(c.Vos is null ? 0 : Geometria.Mod(c.Vos.Vertice - c.Vos.FrenarEn, datos.Largo)):0} m)");
    }

    private static MotorGuia Nuevo(DatosClase datos, MapaPista? mapa, ReferenciaGuia r)
    {
        var m = new MotorGuia { ReferenciaPrincipal = r };
        m.CargarDatos(datos, mapa);
        return m;
    }

    private static void Alimentar(Pasada pa, in SharedMemory sm, int i, bool visto, double t)
    {
        double d = sm.mParticipantInfo[i].mCurrentLapDistance;
        double kmh = (visto ? sm.mSpeed : sm.mSpeeds[i]) * 3.6;
        bool valida = sm.mPitModes[i] == 0 && double.IsFinite(d) && double.IsFinite(kmh);
        var p = pa.Motor.Actualizar(new EntradaGuia(t, d, kmh, sm.mTrackLength, valida));
        if (valida) pa.Panel.Add((t, d, p));
    }

    /// <summary>Tramos consecutivos que siguen a la misma curva.</summary>
    private static List<List<(double t, double d, PanelGuia p)>> Corridas(List<(double t, double d, PanelGuia p)> panel)
    {
        var res = new List<List<(double, double, PanelGuia)>>();
        foreach (var x in panel)
        {
            if (res.Count == 0 || res[^1][0].Item3.IndiceCurva != x.p.IndiceCurva) res.Add([]);
            res[^1].Add(x);
        }
        return res;
    }
}
