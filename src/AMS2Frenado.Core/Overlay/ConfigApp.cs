using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

public enum ModoApp { Mostrar, Grabar }

public sealed class PosicionVentana
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>
/// Configuracion persistente (%APPDATA%\AMS2AsistenteFrenado\config.json). Los carteles por pista no van aca:
/// viven en el mapa.json de cada pista (campo opcional "carteles", ver docs/CARTELES.md).
/// </summary>
public sealed class ConfigApp
{
    public const int FormatoActual = 1;

    public ModoApp Modo { get; set; } = ModoApp.Mostrar;
    public bool Bloqueado { get; set; } = true;
    public bool Visible { get; set; } = true;
    /// <summary>Pitido al entrar en FRENA. Apagado por defecto.</summary>
    public bool Pitido { get; set; }
    public ReferenciaGuia ReferenciaPrincipal { get; set; } = ReferenciaGuia.Ia;
    /// <summary>Ultima esquina elegida (y la que se usa si no hay posicion guardada para el monitor).</summary>
    public Esquina Esquina { get; set; } = Esquina.ArribaDerecha;
    /// <summary>Escala de 0,75 a 1,5 sobre el ancho base de 320 px.</summary>
    public double Escala { get; set; } = 1.0;
    /// <summary>Alfa del fondo, 0,80 a 0,95. El texto nunca se atenua.</summary>
    public double Opacidad { get; set; } = MedidasUi.AlfaPorDefecto;
    public bool MapaVisible { get; set; } = true;
    public bool MapaCompacto { get; set; }
    /// <summary>Opcion "Rojo suave" de la franja de FRENA (UI-SPEC 3.5).</summary>
    public bool RojoSuave { get; set; }
    public string Atajo { get; set; } = AtajoTeclado.PorDefecto.Texto;
    public bool AvisoInicialMostrado { get; set; }
    /// <summary>Posicion de la ventana en DIP por monitor (clave: nombre del monitor). Vacio: se usa la esquina.</summary>
    public Dictionary<string, PosicionVentana> Posiciones { get; set; } = [];

    public AtajoTeclado AtajoEfectivo => AtajoTeclado.Parsear(Atajo) ?? AtajoTeclado.PorDefecto;

    /// <summary>Lleva todos los valores a rangos validos (lo que venga de un archivo editado a mano).</summary>
    public void Normalizar()
    {
        Escala = MedidasUi.LimitarEscala(Escala);
        Opacidad = MedidasUi.LimitarAlfa(Opacidad);
        if (!Enum.IsDefined(Modo)) Modo = ModoApp.Mostrar;
        if (!Enum.IsDefined(ReferenciaPrincipal)) ReferenciaPrincipal = ReferenciaGuia.Ia;
        if (!Enum.IsDefined(Esquina)) Esquina = Esquina.ArribaDerecha;
        var atajo = AtajoTeclado.Parsear(Atajo) ?? AtajoTeclado.PorDefecto;
        Atajo = atajo.Texto;
        Posiciones ??= [];
        foreach (var k in Posiciones.Where(kv => kv.Value is null || !double.IsFinite(kv.Value.X) || !double.IsFinite(kv.Value.Y)).Select(kv => kv.Key).ToList())
            Posiciones.Remove(k);
    }
}

/// <summary>Lee y escribe config.json. Lectura tolerante campo por campo; escritura atomica (.tmp + reemplazo) con .bak.</summary>
public sealed class AlmacenConfig
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Ruta { get; }
    /// <summary>Verdadero si la ultima lectura encontro el archivo danado (se uso el respaldo o los valores por defecto).</summary>
    public bool LecturaDanada { get; private set; }
    /// <summary>Verdadero si no existia config.json (primer arranque).</summary>
    public bool EsPrimerArranque { get; private set; }

    public AlmacenConfig(string? ruta = null)
        => Ruta = ruta ?? Path.Combine(Almacen.RutaPorDefecto(), "config.json");

    public ConfigApp Cargar()
    {
        LecturaDanada = false;
        EsPrimerArranque = !File.Exists(Ruta) && !File.Exists(Ruta + ".bak");
        if (EsPrimerArranque) return Nueva();

        var c = Leer(Ruta);
        if (c is null)
        {
            LecturaDanada = true;
            c = Leer(Ruta + ".bak");
        }
        return c ?? Nueva();
    }

    private static ConfigApp Nueva() { var c = new ConfigApp(); c.Normalizar(); return c; }

    private static ConfigApp? Leer(string ruta)
    {
        try
        {
            if (!File.Exists(ruta)) return null;
            if (JsonNode.Parse(File.ReadAllText(ruta, Encoding.UTF8)) is not JsonObject o) return null;
            var d = new ConfigApp();
            var c = new ConfigApp
            {
                Modo = Campo(o, "modo", d.Modo),
                Bloqueado = Campo(o, "bloqueado", d.Bloqueado),
                Visible = Campo(o, "visible", d.Visible),
                Pitido = Campo(o, "pitido", d.Pitido),
                ReferenciaPrincipal = Campo(o, "referenciaPrincipal", d.ReferenciaPrincipal),
                Esquina = Campo(o, "esquina", d.Esquina),
                Escala = Campo(o, "escala", d.Escala),
                Opacidad = Campo(o, "opacidad", d.Opacidad),
                MapaVisible = Campo(o, "mapaVisible", d.MapaVisible),
                MapaCompacto = Campo(o, "mapaCompacto", d.MapaCompacto),
                RojoSuave = Campo(o, "rojoSuave", d.RojoSuave),
                Atajo = Campo(o, "atajo", d.Atajo),
                AvisoInicialMostrado = Campo(o, "avisoInicialMostrado", d.AvisoInicialMostrado),
                Posiciones = Campo(o, "posiciones", new Dictionary<string, PosicionVentana>()),
            };
            c.Normalizar();
            return c;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private static T Campo<T>(JsonObject o, string nombre, T porDefecto)
    {
        try
        {
            var n = o[nombre];
            return n is null ? porDefecto : n.Deserialize<T>(Opciones) ?? porDefecto;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return porDefecto;
        }
    }

    /// <summary>Escritura atomica. Puede lanzar IOException; quien llama decide si avisar.</summary>
    public void Guardar(ConfigApp config)
    {
        config.Normalizar();
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
        string tmp = Ruta + ".tmp";
        var json = new JsonObject { ["formato"] = ConfigApp.FormatoActual };
        var cuerpo = JsonSerializer.SerializeToNode(config, Opciones)!.AsObject();
        foreach (var kv in cuerpo.ToList()) { cuerpo.Remove(kv.Key); json[kv.Key] = kv.Value; }
        File.WriteAllText(tmp, json.ToJsonString(Opciones), new UTF8Encoding(false));
        if (File.Exists(Ruta))
        {
            if (Leer(Ruta) is not null) File.Copy(Ruta, Ruta + ".bak", overwrite: true);   // no pisamos un respaldo bueno con uno danado
            File.Replace(tmp, Ruta, destinationBackupFileName: null);
        }
        else File.Move(tmp, Ruta);
    }
}
