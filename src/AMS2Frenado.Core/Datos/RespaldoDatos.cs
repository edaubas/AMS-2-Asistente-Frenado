using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace AMS2Frenado.Core.Datos;

/// <summary>Resultado de importar un .zip. <see cref="Omitidos"/> lleva "ruta: motivo".</summary>
public sealed record ResultadoImportacion(
    int Importados, int Respaldados, bool ConfigImportada, IReadOnlyList<string> Omitidos);

/// <summary>El .zip no se puede importar (ruta insegura, JSON danado, demasiado grande). No se toco nada.</summary>
public sealed class RespaldoInvalidoException(string mensaje) : Exception(mensaje);

/// <summary>
/// Exportar e importar los datos de la app (puntos de frenada, mapas y configuracion) como un .zip, para respaldar o
/// pasarlos a otra PC. Logica pura con archivos: se prueba en Linux.
///
/// Exportar: todo %APPDATA%\AMS2AsistenteFrenado\ SIN la carpeta "capturas" (son cientos de MB), sin los .bak ni .tmp.
/// Importar: primero se valida TODO el zip (rutas relativas y seguras dentro de la carpeta de datos, solo .json que se
/// puedan leer, tamaño razonable); si algo falla no se toca ningun archivo. Despues, cada archivo que ya existe se
/// copia a "&lt;archivo&gt;.bak" y se reemplaza de forma atomica. Lo que el zip no trae (capturas, config.json) no se toca.
/// Compila y pasa pruebas en Linux; los cuadros de dialogo de la App compilan sin ejecutar.
/// </summary>
public static class RespaldoDatos
{
    public const string CarpetaCapturas = "capturas";
    /// <summary>Tope de lo que se descomprime (los datos reales son del orden de KB).</summary>
    public const long MaxBytesDescomprimidos = 200L * 1024 * 1024;
    public const int MaxArchivos = 20_000;

    private static readonly char[] Prohibidos = ['<', '>', ':', '"', '|', '?', '*'];

    // ------------------------------------------------------------ exportar

    /// <summary>Crea el .zip con los datos (sin capturas, .bak ni .tmp). Devuelve la cantidad de archivos. Escritura atomica.</summary>
    public static int Exportar(string carpetaDatos, string rutaZip)
    {
        string raiz = Path.GetFullPath(carpetaDatos);
        string destino = Path.GetFullPath(rutaZip);
        if (!Directory.Exists(raiz)) throw new DirectoryNotFoundException("No hay carpeta de datos todavía: " + raiz);

        string tmp = destino + ".tmp";
        int n = 0;
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (string f in Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                {
                    string rel = Path.GetRelativePath(raiz, f).Replace('\\', '/');
                    if (!Incluible(rel)) continue;
                    if (string.Equals(Path.GetFullPath(f), destino, StringComparison.OrdinalIgnoreCase)) continue;
                    var e = zip.CreateEntry(rel, CompressionLevel.Optimal);
                    e.LastWriteTime = File.GetLastWriteTime(f);
                    using var origen = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var salida = e.Open();
                    origen.CopyTo(salida);
                    n++;
                }
            }
            File.Move(tmp, destino, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
        }
        return n;
    }

    private static bool Incluible(string relativa)
    {
        string primera = relativa.Split('/')[0];
        if (string.Equals(primera, CarpetaCapturas, StringComparison.OrdinalIgnoreCase) && relativa.Contains('/')) return false;
        return !EsTemporalOReplica(relativa);
    }

    private static bool EsTemporalOReplica(string r)
        => r.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || r.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------ importar

    /// <summary>
    /// Normaliza y valida la ruta de una entrada del zip. Devuelve la ruta relativa con '/' o lanza
    /// <see cref="RespaldoInvalidoException"/> si no es una ruta relativa segura (zip-slip).
    /// </summary>
    public static string RutaSegura(string nombre, string carpetaDatos)
    {
        string r = nombre.Replace('\\', '/');
        if (r.Length == 0 || r.Length > 400) throw Insegura(nombre);
        if (r.StartsWith('/') || (r.Length >= 2 && char.IsAsciiLetter(r[0]) && r[1] == ':')) throw Insegura(nombre);
        foreach (char c in r)
            if (c < 32 || Array.IndexOf(Prohibidos, c) >= 0) throw Insegura(nombre);
        var partes = r.Split('/');
        foreach (var p in partes)
        {
            if (p.Length == 0 || p == "." || p == ".." || p.TrimEnd('.', ' ').Length == 0 || p != p.Trim() || p.EndsWith('.'))
                throw Insegura(nombre);
        }
        // defensa extra: la ruta final tiene que quedar dentro de la carpeta de datos
        string raiz = Path.GetFullPath(carpetaDatos);
        string final = Path.GetFullPath(Path.Combine(raiz, string.Join(Path.DirectorySeparatorChar, partes)));
        string prefijo = raiz.EndsWith(Path.DirectorySeparatorChar) ? raiz : raiz + Path.DirectorySeparatorChar;
        if (!final.StartsWith(prefijo, StringComparison.Ordinal)) throw Insegura(nombre);
        return r;
    }

    private static RespaldoInvalidoException Insegura(string n)
        => new($"El archivo trae una ruta no permitida (\"{n}\"). No se importó nada.");

    public static ResultadoImportacion Importar(string rutaZip, string carpetaDatos)
    {
        string raiz = Path.GetFullPath(carpetaDatos);
        var omitidos = new List<string>();
        var aplicar = new List<(byte[] Datos, string Rel, string Destino)>();

        ZipArchive zip;
        try { zip = ZipFile.OpenRead(rutaZip); }
        catch (InvalidDataException) { throw new RespaldoInvalidoException("El archivo no es un .zip válido."); }

        using (zip)
        {
            if (zip.Entries.Count > MaxArchivos) throw new RespaldoInvalidoException("El .zip trae demasiados archivos.");

            // ---- 1) validar todo antes de tocar nada
            long total = 0;
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in zip.Entries)
            {
                if (e.FullName.EndsWith('/') || e.FullName.EndsWith('\\')) continue;         // carpeta
                string rel = RutaSegura(e.FullName, raiz);
                if (!vistos.Add(rel)) throw new RespaldoInvalidoException($"El .zip repite el archivo \"{rel}\". No se importó nada.");

                if (rel.StartsWith(CarpetaCapturas + "/", StringComparison.OrdinalIgnoreCase)) { omitidos.Add($"{rel}: las capturas no se importan"); continue; }
                if (EsTemporalOReplica(rel)) { omitidos.Add($"{rel}: copia interna (.bak/.tmp)"); continue; }
                if (!rel.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) { omitidos.Add($"{rel}: solo se importan archivos .json"); continue; }

                // se lee con tope sobre los bytes reales (el tamaño declarado en el zip puede mentir)
                var datos = Leer(e, rel, MaxBytesDescomprimidos - total);
                total += datos.Length;
                ValidarJson(datos, rel);
                aplicar.Add((datos, rel, Path.GetFullPath(Path.Combine(raiz, rel.Replace('/', Path.DirectorySeparatorChar)))));
            }

            // ---- 2) aplicar: respaldo .bak de lo existente y reemplazo atomico
            Directory.CreateDirectory(raiz);
            int importados = 0, respaldados = 0;
            bool config = false;
            foreach (var (datos, rel, destino) in aplicar)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
                string tmp = destino + ".tmp";
                File.WriteAllBytes(tmp, datos);
                if (File.Exists(destino))
                {
                    File.Copy(destino, destino + ".bak", overwrite: true);
                    respaldados++;
                }
                File.Move(tmp, destino, overwrite: true);
                importados++;
                if (string.Equals(rel, "config.json", StringComparison.OrdinalIgnoreCase)) config = true;
            }
            return new ResultadoImportacion(importados, respaldados, config, omitidos);
        }
    }

    private static byte[] Leer(ZipArchiveEntry e, string rel, long maximo)
    {
        try
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            var buf = new byte[81920];
            int n;
            while ((n = s.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > maximo) throw new RespaldoInvalidoException("El .zip descomprimido es demasiado grande.");
            }
            return ms.ToArray();
        }
        catch (InvalidDataException)
        {
            throw new RespaldoInvalidoException($"El archivo \"{rel}\" del .zip está dañado. No se importó nada.");
        }
    }

    private static void ValidarJson(byte[] datos, string rel)
    {
        try
        {
            using var doc = JsonDocument.Parse(datos, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException)
        {
            throw new RespaldoInvalidoException($"El archivo \"{rel}\" del .zip no es un JSON válido. No se importó nada.");
        }
    }
}
