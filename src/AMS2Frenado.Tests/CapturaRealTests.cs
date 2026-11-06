using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>
/// Integracion con capturas reales de AMS2 (Watkins Glen GP, GT3_Gen2, 48 participantes). Hay dos conocidas:
/// una de ~9 vueltas limpias de la IA y 1 tuya, y una de qualy (~39 Hz, ~98 vueltas limpias de la IA y 2 tuyas).
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

    /// <summary>
    /// Zonas de frenada de Watkins Glen GP (metros de vuelta, aprox. donde frena la IA). Tolerancia: 70 m.
    /// </summary>
    public static readonly double[] ZonasFrenada = [150, 2050, 2550, 3100, 3800, 4150, 4650, 4900];
    public const double ToleranciaZonaM = 70;

    /// <summary>
    /// Invariantes sobre las curvas grabadas, validos para cualquiera de las capturas:
    /// 1) cada zona conocida tiene UNA curva de frenada con referencia de la IA (mide el detector y el agrupado);
    /// 2) la IA no produce ninguna curva fuera de esas zonas ni duplicadas (serian falsos positivos);
    /// 3) toda otra curva es "extra" y es solo tuya (sin bloque "ia"): son eventos reales de tu manejo medidos con
    ///    el pedal (un toque o levantada, una frenada extra o re-frenada de una vuelta distinta de la IA).
    ///    No se exige un numero fijo: depende de cuantas vueltas tuyas tenga la captura (con 2 vueltas, un evento
    ///    de una sola vuelta ya alcanza la mitad y forma curva). Cada una tiene a lo sumo tus vueltas limpias.
    /// </summary>
    internal static void VerificarCurvas(DatosClase datos, int vueltasVos)
    {
        var conIa = datos.Curvas.Where(c => c.Ia is not null).ToList();
        foreach (double zona in ZonasFrenada)
        {
            var en = conIa.Where(c => c.Tipo == "frenada"
                && Math.Abs(Geometria.DifCirc(c.Ia!.FrenarEn, zona, datos.Largo)) <= ToleranciaZonaM).ToList();
            Assert.True(en.Count == 1, $"zona {zona} m: {en.Count} curvas de frenada de la IA (se espera 1): "
                + string.Join(", ", conIa.Select(c => $"{c.Nombre}@{c.Ia!.FrenarEn:0}")));
        }
        // la IA no frena en otro lado de esta pista: cualquier otra curva con "ia" seria falso positivo
        Assert.Equal(ZonasFrenada.Length, conIa.Count);

        var extras = datos.Curvas.Where(c => c.Ia is null).ToList();
        foreach (var c in extras)
        {
            Assert.NotNull(c.Vos);
            Assert.InRange(c.Vos!.Vueltas, 1, Math.Max(1, vueltasVos));
        }
        // ninguna curva repetida: dos curvas no pueden compartir el mismo bloque de la IA ni el mismo vertice
        Assert.Equal(conIa.Count, conIa.Select(c => (c.Ia!.FrenarEn, c.Ia.Vueltas)).Distinct().Count());
        Assert.Equal(datos.Curvas.Count, datos.Curvas.Select(c => c.Nombre).Distinct().Count());
        for (int i = 1; i < datos.Curvas.Count; i++)
            Assert.True(datos.Curvas[i].Vertice - datos.Curvas[i - 1].Vertice > 1, "curvas con el mismo vertice");
    }

    private (Grabador g, Almacen al) Grabar(string ruta, Func<long, bool> salta)
    {
        var al = new Almacen(_dir);
        var g = new Grabador(al, 0);
        long k = 0;
        using (var rep = ReproductorCaptura.Abrir(ruta))
            while (true)
            {
                var r = rep.Leer();
                if (r.Estado == EstadoLectura.FinCaptura) break;
                if (r.Estado == EstadoLectura.Ok && salta(++k)) continue;
                g.Procesar(r, rep.SegundosActual);
            }
        g.Guardar();
        return (g, al);
    }

    [Fact]
    public void LaCapturaRealTieneLasOchoZonasDeFrenadaYSusExtrasSonSoloTuyos()
    {
        var ruta = Ruta();
        if (ruta is null) return;     // sin captura (CI): se salta

        var (g, al) = Grabar(ruta, _ => false);
        var estado = Assert.Single(g.Estados());
        Assert.Equal("GT3_Gen2", estado.Clase);
        Assert.True(estado.VueltasIa >= 6, $"vueltas validas de la IA: {estado.VueltasIa}");

        var datos = al.LeerClase(estado.Pista, estado.Variante, estado.Clase)!;
        VerificarCurvas(datos, estado.VueltasVos);

        var mapa = al.LeerMapa(estado.Pista, estado.Variante)!;
        Assert.InRange(mapa.Puntos.Count, 1000, 1100);        // 5440 m / 5 m
    }

    /// <summary>
    /// El detector no puede depender de la frecuencia de muestreo: descartando 1 de cada 3 muestras (la captura de
    /// qualy va a ~39 Hz, queda ~26 Hz, cerca de los ~27 Hz de la primera captura) las zonas deben ser las mismas
    /// y los puntos de frenada de la IA deben moverse poco.
    /// </summary>
    [Fact]
    public void LasZonasDeFrenadaNoDependenDeLaFrecuenciaDeMuestreo()
    {
        var ruta = Ruta();
        if (ruta is null) return;

        var (g, al) = Grabar(ruta, k => k % 3 == 0);
        var estado = Assert.Single(g.Estados());
        var datos = al.LeerClase(estado.Pista, estado.Variante, estado.Clase)!;
        VerificarCurvas(datos, estado.VueltasVos);

        var dir2 = Simulador.CarpetaTemporal();
        try
        {
            var al2 = new Almacen(dir2);
            var g2 = new Grabador(al2, 0);
            using (var rep = ReproductorCaptura.Abrir(ruta))
                while (true)
                {
                    var r = rep.Leer();
                    if (r.Estado == EstadoLectura.FinCaptura) break;
                    g2.Procesar(r, rep.SegundosActual);
                }
            g2.Guardar();
            var completo = al2.LeerClase(estado.Pista, estado.Variante, estado.Clase)!;
            foreach (var c in completo.Curvas.Where(c => c.Ia is not null))
            {
                var par = datos.Curvas.Single(x => x.Ia is not null
                    && Math.Abs(Geometria.DifCirc(x.Ia.FrenarEn, c.Ia!.FrenarEn, datos.Largo)) <= 15);
                Assert.InRange(Math.Abs(Geometria.DifCirc(par.Ia!.Vertice, c.Ia!.Vertice, datos.Largo)), 0, 15);
            }
        }
        finally { try { Directory.Delete(dir2, true); } catch { } }
    }
}
