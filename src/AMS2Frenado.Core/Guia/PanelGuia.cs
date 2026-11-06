using AMS2Frenado.Core.Grabacion;

namespace AMS2Frenado.Core.Guia;

public enum EstadoPanel { SinDatos, Lejos, Aproximacion, Inminente, Frena, Toque, Levanta }

/// <summary>IA (naranja) o vos (azul).</summary>
public enum ReferenciaGuia { Ia, Vos }

/// <summary>Resultado inmutable de la guia para un instante. El overlay (fase 5) solo lo dibuja.</summary>
public sealed record PanelGuia
{
    public EstadoPanel Estado { get; init; }
    /// <summary>Falso en Lejos: se ve solo el mapa y la linea "Proxima CURVA N". SinDatos si se ve (dice "SIN DATOS").</summary>
    public bool Visible { get; init; }
    /// <summary>Lo decide el modo (fase 5); la guia solo lo pasa. No cambia el estado.</summary>
    public bool Grabando { get; init; }

    public int IndiceCurva { get; init; } = -1;
    public CurvaGuardada? Curva { get; init; }
    public string NombreCurva { get; init; } = "";
    public TipoCurva? Tipo { get; init; }

    public ReferenciaGuia? Principal { get; init; }
    public ReferenciaGuia? Secundaria { get; init; }

    /// <summary>Metros que faltan para el punto de frenado (0 una vez que se llego). Dato secundario y para los estados.</summary>
    public double? MetrosHastaFrenar { get; init; }
    public double? MetrosHastaVertice { get; init; }
    /// <summary>Ventana por tiempo vigente, en metros.</summary>
    public double VentanaM { get; init; }

    /// <summary>Cartel de la IA / tuyo (solo curvas de frenada).</summary>
    public int? CartelIa { get; init; }
    public int? CartelVos { get; init; }
    public int? CartelPrincipal { get; init; }
    /// <summary>Null si no hay segunda referencia o si cae en el mismo cartel que la principal (se muestra uno solo).</summary>
    public int? CartelSecundario { get; init; }

    /// <summary>Velocidad minima (vertice) de cada referencia, km/h. Dato principal en Toque y Levanta.</summary>
    public int? VelMinPrincipalKmh { get; init; }
    public int? VelMinSecundariaKmh { get; init; }

    /// <summary>"FRENÁ", "TOQUE", "LEVANTÁ", "SIN DATOS" o vacio.</summary>
    public string TextoEstado { get; init; } = "";
    /// <summary>Numero grande: "200 m" (frenada) o "132 km/h" (toque y levanta).</summary>
    public string? TextoPrincipal { get; init; }
    public string? TextoSecundario { get; init; }
    /// <summary>Linea chica: "Próxima CURVA 3 · 410 m" en Lejos; metros hasta frenar en el resto (null en Levanta).</summary>
    public string? TextoMetros { get; init; }
}
