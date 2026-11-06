using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

/// <summary>Rombo = IA, circulo = vos (redundancia no cromatica, UI-SPEC 1.3).</summary>
public enum FormaMarcador { Rombo, Circulo }

public sealed record EtiquetaRef(FormaMarcador Forma, string Texto, string Color)
{
    public static readonly EtiquetaRef Ia = new(FormaMarcador.Rombo, "IA", Paleta.NaranjaIa);
    public static readonly EtiquetaRef Vos = new(FormaMarcador.Circulo, "VOS", Paleta.AzulVos);
    public static EtiquetaRef De(ReferenciaGuia r) => r == ReferenciaGuia.Ia ? Ia : Vos;
}

/// <summary>Marcador de la regla: fraccion 0..1 de la ventana (0 = ahora, 1 = el limite de la ventana).</summary>
public sealed record MarcadorRegla(FormaMarcador Forma, string Color, double Fraccion);

public sealed record ContadoresGrabacion(
    string Encabezado, string Ia, string IaDetalle, string Vos, string VosDetalle, string Pie);

public enum TipoTarjeta { Ninguna, SinDatos, Lejos, Guia, Grabando }

/// <summary>
/// Que se dibuja en la tarjeta de guia: textos, colores y formas ya decididos. La ventana WPF solo lo pinta
/// (no agrega logica de estado). Los colores son "#RRGGBB".
/// </summary>
public sealed record VistaPanel
{
    public TipoTarjeta Tipo { get; init; }
    public EstadoPanel Estado { get; init; }
    public int IndiceCurva { get; init; } = -1;

    // franja superior de 40 px (FRENA, TOQUE, LEVANTA)
    public string? TextoFranja { get; init; }
    public string? ColorFranja { get; init; }
    public string? ColorTextoFranja { get; init; }

    public string? CurvaTxt { get; init; }
    public string? VelocidadesTxt { get; init; }

    public IReadOnlyList<EtiquetaRef> EtiquetasPrincipal { get; init; } = [];
    /// <summary>Velocidad minima de Toque y Levanta ("132"). Null en las frenadas: el cartel ya no se muestra (docs/CARTELES.md).</summary>
    public string? NumeroPrincipal { get; init; }
    public string? UnidadPrincipal { get; init; }

    public EtiquetaRef? EtiquetaSecundaria { get; init; }
    public string? NumeroSecundario { get; init; }
    public string? UnidadSecundaria { get; init; }

    public bool MostrarRegla { get; init; }
    public double EscalaReglaM { get; init; }
    public IReadOnlyList<MarcadorRegla> Marcadores { get; init; } = [];

    /// <summary>Cuenta regresiva chica ("185 m"); en Lejos, los metros de la linea unica ("640 m").</summary>
    public string? Metros { get; init; }
    public string ColorMetros { get; init; } = Paleta.Secundario;

    // SIN DATOS / aviso / Lejos
    public string? Titulo { get; init; }
    public string ColorTitulo { get; init; } = Paleta.SinDatos;
    public string? Linea1 { get; init; }
    public string? Linea2 { get; init; }

    public ContadoresGrabacion? Contadores { get; init; }

    /// <summary>Texto que identifica todo lo dibujado: la ventana solo repinta si cambia.</summary>
    public string Firma => string.Join("|",
        Tipo, Estado, IndiceCurva, TextoFranja, ColorFranja, ColorTextoFranja, CurvaTxt, VelocidadesTxt,
        string.Join(",", EtiquetasPrincipal.Select(e => e.Texto)), NumeroPrincipal, UnidadPrincipal,
        EtiquetaSecundaria?.Texto, NumeroSecundario, UnidadSecundaria, MostrarRegla, EscalaReglaM,
        string.Join(",", Marcadores.Select(m => $"{m.Forma}:{m.Fraccion:0.###}")), Metros, ColorMetros,
        Titulo, ColorTitulo, Linea1, Linea2, Contadores);

    public static readonly VistaPanel Nada = new() { Tipo = TipoTarjeta.Ninguna };
}
