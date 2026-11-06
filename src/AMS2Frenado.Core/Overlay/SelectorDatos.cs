using AMS2Frenado.Core.Grabacion;

namespace AMS2Frenado.Core.Overlay;

/// <summary>Pista + variante + clase del auto visto, tal como las publica el juego.</summary>
public sealed record ContextoJuego(string Pista, string Variante, string Clase)
{
    /// <summary>Null si el juego todavia no publica pista o clase.</summary>
    public static ContextoJuego? De(in SharedMemory sm)
    {
        string pista = Texto.Leer(sm.mTrackLocation);
        string variante = Texto.Leer(sm.mTrackVariation);
        string clase = Texto.Leer(sm.mCarClassName);
        int visto = sm.mViewedParticipantIndex;
        if (clase.Length == 0 && visto >= 0 && visto < ConstantesShm.MaxParticipantes)
        {
            var arr = sm.mCarClassNames[visto];
            clase = Texto.Leer(arr);
        }
        return pista.Length == 0 || clase.Length == 0 ? null : new ContextoJuego(pista, variante, clase);
    }
}

/// <param name="MotivoClase">Por que no hay <paramref name="Clase"/>: no existe el archivo o esta danado (Ok si se leyo).</param>
public sealed record DatosCargados(DatosClase? Clase, MapaPista? Mapa, long Sello, MotivoLectura MotivoClase = MotivoLectura.Ok)
{
    public bool HayCurvas => Clase is { Curvas.Count: > 0 };
}

/// <summary>Elige y lee los archivos de datos de la pista + variante + clase actual.</summary>
public static class SelectorDatos
{
    public static DatosCargados Cargar(Almacen almacen, ContextoJuego ctx)
    {
        var clase = almacen.LeerClaseDetallado(ctx.Pista, ctx.Variante, ctx.Clase);
        var mapa = almacen.LeerMapa(ctx.Pista, ctx.Variante);
        return new DatosCargados(clase.Datos, mapa, Sello(almacen, ctx), clase.Motivo);
    }

    /// <summary>Cambia cuando cambia alguno de los dos archivos (para recargar solo si hace falta).</summary>
    public static long Sello(Almacen almacen, ContextoJuego ctx)
        => SelloArchivo(almacen.RutaClase(ctx.Pista, ctx.Variante, ctx.Clase))
         ^ (SelloArchivo(almacen.RutaMapa(ctx.Pista, ctx.Variante)) * 31);

    private static long SelloArchivo(string ruta)
    {
        try { return File.Exists(ruta) ? new FileInfo(ruta).LastWriteTimeUtc.Ticks ^ new FileInfo(ruta).Length : 0; }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }

    /// <summary>Clases con curvas guardadas para la pista y variante (nombres de archivo sin .json, sin mapa).</summary>
    public static IReadOnlyList<string> ClasesGuardadas(Almacen almacen, string pista, string variante)
    {
        try
        {
            string dir = almacen.CarpetaPista(pista, variante);
            if (!Directory.Exists(dir)) return [];
            return Directory.EnumerateFiles(dir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n is not null && !string.Equals(n, "mapa", StringComparison.OrdinalIgnoreCase))
                .Select(n => n!).Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
}
