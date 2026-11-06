using System.Text;

namespace AMS2Frenado.Core.Overlay;

/// <summary>Formato de una linea del registro de diagnostico (texto plano, una linea por evento, sin datos personales).</summary>
public static class FormatoRegistro
{
    public const int MaxLinea = 300;

    /// <summary>"2026-11-06 11:35:51.123 [categoria] mensaje". Sin saltos de linea; recortada a <see cref="MaxLinea"/> caracteres.</summary>
    public static string Linea(DateTime hora, string categoria, string mensaje)
    {
        string texto = $"{hora:yyyy-MM-dd HH:mm:ss.fff} [{Limpiar(categoria)}] {Limpiar(mensaje)}";
        return texto.Length <= MaxLinea ? texto : texto[..(MaxLinea - 1)] + "…";
    }

    /// <summary>Cambia saltos de linea y tabulaciones por espacios y reemplaza la carpeta del usuario por "~" (no se guarda el nombre de usuario).</summary>
    public static string Limpiar(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        foreach (var carpeta in CarpetasPersonales())
            s = s.Replace(carpeta, "~", StringComparison.OrdinalIgnoreCase);
        return s;
    }

    private static IEnumerable<string> CarpetasPersonales()
    {
        string perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (perfil.Length > 3) yield return perfil;
        string usuario = Environment.UserName;
        if (usuario.Length > 2) yield return usuario;
    }
}

/// <summary>
/// Registro de texto opcional (APAGADO por defecto) para diagnosticar problemas que solo se ven en Windows: marcas de tiempo de
/// arranque, ventanas, cambios de estado de la lectura, ticks lentos (hilo de interfaz bloqueado) y errores atrapados.
/// Nunca lanza: si no se puede escribir, se descarta. Rotacion simple: al llegar a <see cref="TamanoMaximo"/> bytes, el archivo
/// pasa a "registro.log.1" (pisando el anterior) y se empieza otro, asi nunca ocupa mas de ~2 x el maximo.
/// Es seguro llamarlo desde cualquier hilo.
/// </summary>
public sealed class RegistroDiagnostico
{
    public const long TamanoMaximoPorDefecto = 1_000_000;
    /// <summary>Un tick de la interfaz que dure mas que esto se anota como lento.</summary>
    public const double UmbralTickMs = 100;

    private readonly object _candado = new();
    private readonly Func<DateTime> _reloj;
    private readonly Dictionary<string, string> _ultimos = [];
    private long _tamano = -1;

    public string Ruta { get; }
    public long TamanoMaximo { get; }
    private volatile bool _activo;
    /// <summary>Apagado por defecto. Al encender se anota una marca; al apagar, otra.</summary>
    public bool Activo
    {
        get => _activo;
        set
        {
            if (value == _activo) return;
            if (value) { _activo = true; _ultimos.Clear(); Escribir("registro", "activado"); }
            else { Escribir("registro", "desactivado"); _activo = false; }
        }
    }

    public RegistroDiagnostico(string ruta, long tamanoMaximo = TamanoMaximoPorDefecto, Func<DateTime>? reloj = null)
    {
        Ruta = ruta;
        TamanoMaximo = Math.Max(200, tamanoMaximo);
        _reloj = reloj ?? (() => DateTime.Now);
    }

    /// <summary>Anota una linea (si esta activo).</summary>
    public void Escribir(string categoria, string mensaje)
    {
        if (!_activo) return;
        string linea = FormatoRegistro.Linea(_reloj(), categoria, mensaje) + "\n";
        lock (_candado)
        {
            try
            {
                int bytes = Encoding.UTF8.GetByteCount(linea);
                if (_tamano < 0) _tamano = File.Exists(Ruta) ? new FileInfo(Ruta).Length : 0;
                if (_tamano + bytes > TamanoMaximo && _tamano > 0) Rotar();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Ruta))!);
                File.AppendAllText(Ruta, linea, new UTF8Encoding(false));
                _tamano += bytes;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _tamano = -1;       // se vuelve a medir en el proximo intento; el registro nunca tira la app
            }
        }
    }

    private void Rotar()
    {
        File.Move(Ruta, Ruta + ".1", overwrite: true);
        _tamano = 0;
    }

    /// <summary>Anota solo si el valor de esa clave cambio desde la ultima vez (por ejemplo el estado de la lectura). Devuelve si anoto.</summary>
    public bool Cambio(string categoria, string clave, string valor)
    {
        if (!_activo) return false;
        lock (_candado)
        {
            if (_ultimos.TryGetValue(clave, out var anterior) && anterior == valor) return false;
            _ultimos[clave] = valor;
        }
        Escribir(categoria, $"{clave}: {valor}");
        return true;
    }

    /// <summary>Anota un tick solo si duro <see cref="UmbralTickMs"/> o mas. Devuelve si anoto.</summary>
    public bool Tick(double milisegundos)
    {
        if (!_activo || milisegundos < UmbralTickMs) return false;
        Escribir("tick-lento", $"{milisegundos:0} ms");
        return true;
    }

    /// <summary>Anota un error atrapado: tipo y mensaje, sin la pila (puede traer rutas) y con la carpeta del usuario reemplazada.</summary>
    public void Error(string donde, Exception e) => Escribir("error", $"{donde}: {e.GetType().Name}: {e.Message}");
}
