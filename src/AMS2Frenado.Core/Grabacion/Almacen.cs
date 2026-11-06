using System.Text;
using System.Text.Json;

namespace AMS2Frenado.Core.Grabacion;

/// <summary>
/// Lee y escribe los JSON de datos: una carpeta por pista y variante bajo la ruta base
/// (por defecto %APPDATA%\AMS2AsistenteFrenado\). La ruta base se inyecta para poder probar.
/// </summary>
public sealed class Almacen
{
    public static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HashSet<string> _respaldados = new(StringComparer.Ordinal);

    public string RutaBase { get; }

    public Almacen(string? rutaBase = null) => RutaBase = rutaBase ?? RutaPorDefecto();

    public static string RutaPorDefecto()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AMS2AsistenteFrenado");

    public string CarpetaPista(string pista, string variante)
        => Path.Combine(RutaBase, NombreSeguro(pista), NombreSeguro(string.IsNullOrWhiteSpace(variante) ? "_sin-variante" : variante));

    public string RutaMapa(string pista, string variante) => Path.Combine(CarpetaPista(pista, variante), "mapa.json");

    public string RutaClase(string pista, string variante, string clase)
        => Path.Combine(CarpetaPista(pista, variante), NombreSeguro(clase) + ".json");

    /// <summary>Nombre apto como carpeta o archivo en Windows y Linux.</summary>
    public static string NombreSeguro(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.Trim())
            sb.Append(c < 32 || "\\/:*?\"<>|".Contains(c) ? '_' : c);
        string r = sb.ToString().TrimEnd('.', ' ');
        if (r.Length > 100) r = r[..100];
        return r.Length == 0 ? "_" : r;
    }

    public DatosClase? LeerClase(string pista, string variante, string clase)
        => Leer<DatosClase>(RutaClase(pista, variante, clase));

    public MapaPista? LeerMapa(string pista, string variante)
        => Leer<MapaPista>(RutaMapa(pista, variante));

    /// <summary>Lee un JSON; si esta danado prueba el respaldo .bak; si tampoco sirve devuelve null.</summary>
    public static T? Leer<T>(string ruta) where T : class
        => LeerArchivo<T>(ruta) ?? LeerArchivo<T>(ruta + ".bak");

    private static T? LeerArchivo<T>(string ruta) where T : class
    {
        try
        {
            if (!File.Exists(ruta)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(ruta, Encoding.UTF8), OpcionesJson);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void GuardarClase(DatosClase datos)
        => GuardarAtomico(RutaClase(datos.Pista, datos.Variante, datos.Clase), datos);

    public void GuardarMapa(MapaPista mapa)
        => GuardarAtomico(RutaMapa(mapa.Pista, mapa.Variante), mapa);

    /// <summary>
    /// Escribe a un .tmp y lo reemplaza sobre el destino (no queda un archivo a medias).
    /// Antes del primer reemplazo de la sesion deja una copia .bak del archivo anterior,
    /// pero solo si ese archivo se lee bien (no pisamos un respaldo bueno con uno danado).
    /// </summary>
    public void GuardarAtomico<T>(string ruta, T datos) where T : class
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        string tmp = ruta + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(datos, OpcionesJson), new UTF8Encoding(false));

        if (File.Exists(ruta))
        {
            if (_respaldados.Add(ruta) && LeerArchivo<T>(ruta) is not null)
                File.Copy(ruta, ruta + ".bak", overwrite: true);
            File.Replace(tmp, ruta, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, ruta);
        }
    }
}
