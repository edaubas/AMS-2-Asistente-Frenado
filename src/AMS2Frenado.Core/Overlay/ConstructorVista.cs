using System.Globalization;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

/// <summary>Datos que la guia no trae y la vista necesita.</summary>
/// <param name="MotivoArchivo">Por que no se cargo el archivo de la clase (danado, inexistente); Ok si se leyo.</param>
public sealed record ContextoVista(string Clase = "", string Pista = "", string Variante = "", double LargoPista = 0,
    MotivoLectura MotivoArchivo = MotivoLectura.Ok);

public static class FormatoUi
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    /// <summary>Metros enteros ("185 m").</summary>
    public static string Metros(double m) => string.Create(Ci, $"{Math.Max(0, Math.Round(m, MidpointRounding.AwayFromZero)):0} m");

    /// <summary>En Lejos los metros se redondean a 10 ("640 m").</summary>
    public static string MetrosLejos(double m) => string.Create(Ci, $"{Math.Max(0, Math.Round(m / 10, MidpointRounding.AwayFromZero) * 10):0} m");

    public static string Velocidades(int entrada, int vertice) => string.Create(Ci, $"{entrada} → {vertice} km/h");
}

/// <summary>Convierte un <see cref="PanelGuia"/> en lo que se dibuja (UI-SPEC seccion 2). Logica pura.</summary>
public static class ConstructorVista
{
    /// <summary>Diferencia minima de velocidad para mostrar la secundaria en Toque y Levanta (UI-SPEC P15).</summary>
    public const int DifMinimaVelocidadKmh = 3;

    public static VistaPanel Construir(PanelGuia p, ContextoVista c) => p.Estado switch
    {
        EstadoPanel.SinDatos => SinDatos(p, c),
        EstadoPanel.Lejos => Lejos(p),
        _ => Guia(p, c),
    };

    /// <summary>SIN DATOS con la causa real: boxes, largo distinto, archivo danado, datos inservibles o sin curvas.</summary>
    public static VistaPanel SinDatos(PanelGuia p, ContextoVista c)
    {
        var ci = CultureInfo.InvariantCulture;
        string archivo = c.Clase.Length == 0 ? "la clase" : Almacen.NombreSeguro(c.Clase) + ".json";
        // un archivo que no se pudo leer manda sobre cualquier otra causa: el motor lo ve como "sin curvas"
        if (c.MotivoArchivo == MotivoLectura.Danado)
            return Aviso("SIN DATOS", "No se pudo leer el archivo de datos.",
                $"Revisá {archivo} en la carpeta de datos (Bandeja › Datos) o restaurá su copia .bak.");
        return p.Causa switch
        {
            CausaSinDatos.EnBoxes => Aviso("SIN DATOS", "En boxes: la guía arranca al salir a pista."),
            CausaSinDatos.LargoDistinto => Aviso("SIN DATOS",
                string.Create(ci, $"Largo de pista distinto al guardado (guardado {Math.Round(p.LargoGuardadoM):0} m, juego {Math.Round(p.LargoJuegoM):0} m)."),
                "Puede ser otra variante o una versión distinta de la pista. Regrabá la clase en Bandeja › Modo › Grabar todo."),
            CausaSinDatos.CurvasInvalidas => Aviso("SIN DATOS", "Datos sin curvas válidas.",
                $"Las curvas de {archivo} no tienen un frenado utilizable. Regrabá la clase."),
            _ => SinCurvas(c),
        };
    }

    /// <summary>No hay archivo (o no tiene curvas) para esta pista y clase.</summary>
    public static VistaPanel SinCurvas(ContextoVista c)
    {
        string donde = c.Pista.Length == 0 ? "esta pista"
            : c.Variante.Length == 0 ? c.Pista : $"{c.Pista} · {c.Variante}";
        string clase = c.Clase.Length == 0 ? "" : $" para {c.Clase}";
        return Aviso("SIN DATOS", $"No hay curvas guardadas{clase} en {donde}.",
            "Bandeja › Modo › Grabar todo y da unas 10 vueltas con IA en pista.");
    }

    public static VistaPanel SinDatos(ContextoVista c) => SinCurvas(c);

    public static VistaPanel EsperandoAms2()
        => Aviso("ESPERANDO AMS2", "No detecto AMS2.", "Activá Options › System › Shared Memory = Project CARS 2.");

    public static VistaPanel VersionNoCompatible(uint version)
        => Aviso("VERSIÓN NO COMPATIBLE", $"Versión de datos no compatible ({version}); se necesita la {ConstantesShm.Version}.",
            "No se lee nada más hasta que coincida.");

    public static VistaPanel EntrarAPista()
        => Aviso("SIN DATOS", "Esperando una sesión en pista.", "Salí a pista con AMS2 en marcha.");

    public static VistaPanel Aviso(string titulo, string linea1, string? linea2 = null) => new()
    {
        Tipo = TipoTarjeta.SinDatos, Estado = EstadoPanel.SinDatos, Titulo = titulo, Linea1 = linea1, Linea2 = linea2,
    };

    private static VistaPanel Lejos(PanelGuia p) => new()
    {
        Tipo = TipoTarjeta.Lejos, Estado = EstadoPanel.Lejos, IndiceCurva = p.IndiceCurva,
        Linea1 = "Próxima",
        CurvaTxt = p.NombreCurva.ToUpperInvariant(),
        Metros = FormatoUi.MetrosLejos(p.MetrosHastaFrenar ?? 0),
    };

    private static VistaPanel Guia(PanelGuia p, ContextoVista c)
    {
        bool frenada = p.Tipo == TipoCurva.Frenada;
        bool hayFranja = p.Estado is EstadoPanel.Frena or EstadoPanel.Toque or EstadoPanel.Levanta;
        string? colorFranja = null, colorTextoFranja = null;
        if (hayFranja)
        {
            colorFranja = p.Estado == EstadoPanel.Frena ? Paleta.RojoFranja : Paleta.FranjaNeutra;
            colorTextoFranja = Paleta.Blanco;
        }

        string? numero, unidad, numeroSec = null, unidadSec = null;
        bool muestraSec;
        if (frenada)
        {
            // el cartel (200 m) ya no se muestra: se leia como cuenta regresiva (docs/CARTELES.md)
            numero = null; unidad = null; muestraSec = false;
        }
        else
        {
            numero = p.VelMinPrincipalKmh?.ToString(CultureInfo.InvariantCulture) ?? "—";
            unidad = "km/h";
            muestraSec = p.Secundaria is not null && p.VelMinSecundariaKmh is int vs && p.VelMinPrincipalKmh is int vp
                && Math.Abs(vs - vp) >= DifMinimaVelocidadKmh;
            if (muestraSec) { numeroSec = p.VelMinSecundariaKmh!.Value.ToString(CultureInfo.InvariantCulture); unidadSec = "km/h"; }
        }

        var principal = p.Principal ?? ReferenciaGuia.Ia;
        // dos referencias que dan lo mismo: un solo numero con etiqueta doble "IA · VOS" (siempre IA primero)
        IReadOnlyList<EtiquetaRef> etiquetas = !muestraSec && p.Secundaria is not null
            ? [EtiquetaRef.Ia, EtiquetaRef.Vos]
            : [EtiquetaRef.De(principal)];

        bool aproximando = p.Estado is EstadoPanel.Aproximacion or EstadoPanel.Inminente;
        // en FRENA el cuerpo conserva las velocidades de entrada y vertice, sin regla ni cuenta regresiva
        bool inminente = p.Estado == EstadoPanel.Inminente;
        string? metros = p.Estado switch
        {
            EstadoPanel.Aproximacion or EstadoPanel.Inminente or EstadoPanel.Toque => FormatoUi.Metros(p.MetrosHastaFrenar ?? 0),
            _ => null,     // FRENA y LEVANTA: sin cuenta regresiva ni texto extra
        };

        return new VistaPanel
        {
            Tipo = TipoTarjeta.Guia, Estado = p.Estado, IndiceCurva = p.IndiceCurva,
            TextoFranja = hayFranja ? p.TextoEstado : null,
            ColorFranja = colorFranja, ColorTextoFranja = colorTextoFranja,
            CurvaTxt = p.NombreCurva.ToUpperInvariant(),
            VelocidadesTxt = frenada && (aproximando || p.Estado == EstadoPanel.Frena) ? Velocidades(p, principal) : null,
            EtiquetasPrincipal = etiquetas,
            NumeroPrincipal = numero, UnidadPrincipal = unidad,
            EtiquetaSecundaria = muestraSec && p.Secundaria is { } s ? EtiquetaRef.De(s) : null,
            NumeroSecundario = numeroSec, UnidadSecundaria = unidadSec,
            MostrarRegla = aproximando,
            EscalaReglaM = aproximando ? Math.Round(p.VentanaM) : 0,
            Marcadores = aproximando ? Marcadores(p, principal, c.LargoPista) : [],
            Metros = metros,
            ColorMetros = inminente ? Paleta.Ambar : Paleta.Secundario,
        };
    }

    private static string? Velocidades(PanelGuia p, ReferenciaGuia principal)
    {
        var b = principal == ReferenciaGuia.Ia ? p.Curva?.Ia : p.Curva?.Vos;
        return b is null ? null : FormatoUi.Velocidades(b.VEntradaKmh, b.VVerticeKmh);
    }

    /// <summary>
    /// Posicion del punto de frenado de cada referencia sobre la regla. La principal usa los metros de la guia;
    /// la otra se corre por la diferencia entre sus puntos de frenado (circular, si se conoce el largo).
    /// La escala de la regla es la ventana vigente de la guia (UI-SPEC P13).
    /// </summary>
    private static List<MarcadorRegla> Marcadores(PanelGuia p, ReferenciaGuia principal, double largo)
    {
        var res = new List<MarcadorRegla>();
        if (p.Curva is null || p.VentanaM <= 0) return res;
        var bp = principal == ReferenciaGuia.Ia ? p.Curva.Ia : p.Curva.Vos;
        if (bp is null) return res;
        double basem = p.MetrosHastaFrenar ?? 0;
        foreach (var r in new[] { ReferenciaGuia.Ia, ReferenciaGuia.Vos })
        {
            if (r != principal && r != p.Secundaria) continue;
            var b = r == ReferenciaGuia.Ia ? p.Curva.Ia : p.Curva.Vos;
            if (b is null) continue;
            double m = r == principal ? basem
                : largo > 0 ? basem + Geometria.DifCirc(b.FrenarEn, bp.FrenarEn, largo) : basem;
            var e = EtiquetaRef.De(r);
            res.Add(new MarcadorRegla(e.Forma, e.Color, Math.Clamp(m / p.VentanaM, 0, 1)));
        }
        return res;
    }
}
