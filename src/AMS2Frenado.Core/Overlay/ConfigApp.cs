using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

/// <summary>
/// Mostrar: solo usa lo guardado. Grabar: graba IA y vos. GrabarSoloVos: graba unicamente al auto visto y no toca lo de
/// la IA ya guardado. En config.json: "mostrar", "grabar", "grabarSoloVos".
/// </summary>
public enum ModoApp { Mostrar, Grabar, GrabarSoloVos }

public sealed class PosicionVentana
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>
/// Configuracion persistente (%APPDATA%\AMS2AsistenteFrenado\config.json). Los carteles por pista no van aca:
/// viven en el mapa.json de cada pista (campo opcional "carteles", ver docs/CARTELES.md). Los campos que ya no
/// existen (mapaVisible, mapaCompacto, rojoSuave) se ignoran al leer un config.json de la fase 5.
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
    /// <summary>Esquina donde se ancla la primera vez (sin posicion guardada para el monitor). Ya no hay menu para cambiarla: se mueve a mano.</summary>
    public Esquina Esquina { get; set; } = Esquina.ArribaDerecha;
    /// <summary>Escala de 0,75 a 1,5 sobre el ancho base de 320 px.</summary>
    public double Escala { get; set; } = 1.0;
    /// <summary>Alfa del fondo, 0,80 a 0,95. El texto nunca se atenua.</summary>
    public double Opacidad { get; set; } = MedidasUi.AlfaPorDefecto;
    /// <summary>Aviso de frenada (simbolo de freno en ventana aparte). Activado por defecto.</summary>
    public bool AvisoActivo { get; set; } = true;
    /// <summary>Segundos maximos que el aviso queda en pantalla si no frenas (1 a 2).</summary>
    public double AvisoDuracionS { get; set; } = AvisoFrenada.DuracionPorDefectoS;
    /// <summary>Segundos antes del punto de frenado en que el simbolo empieza a aparecer (1 a 3).</summary>
    public double AvisoRampaS { get; set; } = DecisorAviso.RampaPorDefectoS;
    /// <summary>Segundos antes del punto de frenado en que suena el pitido y el simbolo llega al 100 % (0 a 2,0; paso 0,1; 0 = en el punto).</summary>
    public double AvisoAnticipacionS { get; set; } = DecisorAviso.AnticipacionPorDefectoS;
    /// <summary>Escala del simbolo, 0,5 a 3 sobre 120 px.</summary>
    public double AvisoEscala { get; set; } = 1.0;
    /// <summary>Posicion de la ventana del aviso en DIP por monitor. Vacio: centrado, en el tercio superior.</summary>
    public Dictionary<string, PosicionVentana> AvisoPosiciones { get; set; } = [];
    public string Atajo { get; set; } = AtajoTeclado.PorDefecto.Texto;
    public bool AvisoInicialMostrado { get; set; }
    /// <summary>Posicion de la ventana en DIP por monitor (clave: nombre del monitor). Vacio: se usa la esquina.</summary>
    public Dictionary<string, PosicionVentana> Posiciones { get; set; } = [];
    /// <summary>
    /// Ultima posicion (DIP de pantalla) donde el usuario dejo la ventana de ajustes arrastrandola por la cabecera; null = automatica
    /// (junto a la barra de botones). Se lleva dentro del monitor donde cae al usarla.
    /// </summary>
    public PosicionVentana? AjustesPosicion { get; set; }

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
        AvisoDuracionS = AvisoFrenada.LimitarDuracion(AvisoDuracionS);
        AvisoRampaS = DecisorAviso.LimitarRampa(AvisoRampaS);
        AvisoAnticipacionS = DecisorAviso.LimitarAnticipacion(AvisoAnticipacionS);
        AvisoEscala = MedidasAviso.LimitarEscala(AvisoEscala);
        Posiciones = LimpiarPosiciones(Posiciones);
        AvisoPosiciones = LimpiarPosiciones(AvisoPosiciones);
        if (AjustesPosicion is { } ap && (!double.IsFinite(ap.X) || !double.IsFinite(ap.Y))) AjustesPosicion = null;
    }

    private static Dictionary<string, PosicionVentana> LimpiarPosiciones(Dictionary<string, PosicionVentana>? d)
    {
        d ??= [];
        foreach (var k in d.Where(kv => kv.Value is null || !double.IsFinite(kv.Value.X) || !double.IsFinite(kv.Value.Y)).Select(kv => kv.Key).ToList())
            d.Remove(k);
        return d;
    }

    /// <summary>Copia todos los valores de otra configuracion (por ejemplo, tras importar un config.json).</summary>
    public void CopiarDe(ConfigApp o)
    {
        Modo = o.Modo; Bloqueado = o.Bloqueado; Visible = o.Visible; Pitido = o.Pitido;
        ReferenciaPrincipal = o.ReferenciaPrincipal; Esquina = o.Esquina; Escala = o.Escala; Opacidad = o.Opacidad;
        AvisoActivo = o.AvisoActivo; AvisoDuracionS = o.AvisoDuracionS; AvisoRampaS = o.AvisoRampaS; AvisoAnticipacionS = o.AvisoAnticipacionS; AvisoEscala = o.AvisoEscala;
        AvisoPosiciones = new Dictionary<string, PosicionVentana>(o.AvisoPosiciones);
        Atajo = o.Atajo; AvisoInicialMostrado = o.AvisoInicialMostrado;
        Posiciones = new Dictionary<string, PosicionVentana>(o.Posiciones);
        AjustesPosicion = o.AjustesPosicion is { } a ? new PosicionVentana { X = a.X, Y = a.Y } : null;
        Normalizar();
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
                AvisoActivo = Campo(o, "avisoActivo", d.AvisoActivo),
                AvisoDuracionS = Campo(o, "avisoDuracionS", d.AvisoDuracionS),
                AvisoRampaS = Campo(o, "avisoRampaS", d.AvisoRampaS),
                AvisoAnticipacionS = Campo(o, "avisoAnticipacionS", d.AvisoAnticipacionS),
                AvisoEscala = Campo(o, "avisoEscala", d.AvisoEscala),
                AvisoPosiciones = Campo(o, "avisoPosiciones", new Dictionary<string, PosicionVentana>()),
                Atajo = Campo(o, "atajo", d.Atajo),
                AvisoInicialMostrado = Campo(o, "avisoInicialMostrado", d.AvisoInicialMostrado),
                Posiciones = Campo(o, "posiciones", new Dictionary<string, PosicionVentana>()),
                AjustesPosicion = Campo<PosicionVentana?>(o, "ajustesPosicion", null),
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
