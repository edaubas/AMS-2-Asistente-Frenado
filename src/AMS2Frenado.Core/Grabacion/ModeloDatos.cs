using System.Text.Json;
using System.Text.Json.Serialization;

namespace AMS2Frenado.Core.Grabacion;

/// <summary>Referencia de una curva (de la IA o tuya). Velocidades en km/h, distancias en metros.</summary>
public sealed class BloqueCurva
{
    [JsonPropertyName("frenar_en")] public double FrenarEn { get; set; }
    [JsonPropertyName("largo_frenada")] public double LargoFrenada { get; set; }
    [JsonPropertyName("vertice")] public double Vertice { get; set; }
    [JsonPropertyName("v_entrada_kmh")] public int VEntradaKmh { get; set; }
    [JsonPropertyName("v_vertice_kmh")] public int VVerticeKmh { get; set; }
    [JsonPropertyName("vueltas")] public int Vueltas { get; set; }
    /// <summary>Dispersion (MAD) del punto de frenado, en metros.</summary>
    [JsonPropertyName("dispersion_m")] public double DispersionM { get; set; }
    /// <summary>Dispersion (MAD) de la velocidad del vertice, en km/h.</summary>
    [JsonPropertyName("dispersion_v_kmh")] public double DispersionVKmh { get; set; }
}

public sealed class CurvaGuardada
{
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("nota")] public string? Nota { get; set; }
    /// <summary>Distancia de vuelta de la velocidad minima (el vertice), en metros.</summary>
    [JsonPropertyName("vertice")] public double Vertice { get; set; }
    /// <summary>"frenada", "toque" o "levanta".</summary>
    [JsonPropertyName("tipo")] public string Tipo { get; set; } = "frenada";
    [JsonPropertyName("ia")] public BloqueCurva? Ia { get; set; }
    [JsonPropertyName("vos")] public BloqueCurva? Vos { get; set; }
    /// <summary>Campos que el usuario agregue a mano: se conservan al regrabar.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Contenido de &lt;clase&gt;.json.</summary>
public sealed class DatosClase
{
    public const int FormatoActual = 1;
    [JsonPropertyName("formato")] public int Formato { get; set; } = FormatoActual;
    [JsonPropertyName("pista")] public string Pista { get; set; } = "";
    [JsonPropertyName("variante")] public string Variante { get; set; } = "";
    [JsonPropertyName("clase")] public string Clase { get; set; } = "";
    [JsonPropertyName("largo")] public double Largo { get; set; }
    [JsonPropertyName("curvas")] public List<CurvaGuardada> Curvas { get; set; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class PuntoMapa
{
    [JsonPropertyName("d")] public double D { get; set; }
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("z")] public double Z { get; set; }
}

/// <summary>Contenido de mapa.json: un punto cada <see cref="Paso"/> metros de distancia de vuelta.</summary>
public sealed class MapaPista
{
    public const int FormatoActual = 1;
    [JsonPropertyName("formato")] public int Formato { get; set; } = FormatoActual;
    [JsonPropertyName("pista")] public string Pista { get; set; } = "";
    [JsonPropertyName("variante")] public string Variante { get; set; } = "";
    [JsonPropertyName("largo")] public double Largo { get; set; }
    [JsonPropertyName("paso_m")] public double Paso { get; set; } = 5;
    /// <summary>Vueltas validas que se usaron para armarlo.</summary>
    [JsonPropertyName("vueltas")] public int Vueltas { get; set; }
    [JsonPropertyName("puntos")] public List<PuntoMapa> Puntos { get; set; } = [];
    /// <summary>
    /// Opcional, se edita a mano: carteles de frenada de la pista en metros (ej. [300, 200, 100, 50]).
    /// Si falta se usa 400/300/200/100/50. Se conserva al regrabar el mapa. Ver docs/CARTELES.md.
    /// </summary>
    [JsonPropertyName("carteles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? Carteles { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
