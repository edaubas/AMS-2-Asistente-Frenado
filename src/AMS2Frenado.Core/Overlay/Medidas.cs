namespace AMS2Frenado.Core.Overlay;

/// <summary>Colores del overlay (docs/UI-SPEC.md, 3.3). Cadenas "#RRGGBB" para que la logica no dependa de WPF.</summary>
public static class Paleta
{
    public const string Fondo = "#0C0E11";
    public const string Blanco = "#FFFFFF";
    public const string Secundario = "#DDE2E7";
    public const string Apoyo = "#A9B2BC";
    public const string Titulo = "#EEF1F4";
    public const string SinDatos = "#C3CAD1";
    public const string NaranjaIa = "#FF9A3D";
    public const string AzulVos = "#5AA9FF";
    public const string Ambar = "#FFC24D";
    public const string RojoFranja = "#C62F26";
    public const string FranjaNeutra = "#3B4652";
    public const string RojoSuaveFranja = "#5A1D19";
    public const string RojoSuaveTexto = "#FF9C94";
    public const string RojoSuaveContorno = "#C62F26";
    public const string ReglaFondo = "#232A31";
    public const string ReglaBase = "#33414E";
    public const string ReglaMarcas = "#4A5561";
    public const string ChipClaseFondo = "#2A3139", ChipClaseTexto = "#EEF1F4";
    public const string ChipMostrarFondo = "#1E3A2A", ChipMostrarTexto = "#8EE0A9";
    public const string ChipGrabandoFondo = "#4A1F1C", ChipGrabandoTexto = "#FF9C94", ChipGrabandoPunto = "#FF5A4E";
    public const string ChipDesbloqueadoFondo = "#17304D", ChipDesbloqueadoTexto = "#8CC3FF";
    public const string MapaTrazo = "#C3CAD1", MapaBorde = "#56616C", MapaHalo = "#0C0E11";
}

/// <summary>Medidas en px = DIP a 96 dpi (docs/UI-SPEC.md, 3.2).</summary>
public static class MedidasUi
{
    public const double AnchoBase = 320, AnchoMinimo = 240, AnchoMaximo = 480;
    public const double EscalaMinima = 0.75, EscalaMaxima = 1.5;
    public const double Margen = 24;
    public const double AltoFranja = 40;
    public const double MapaAncho = 296, MapaAlto = 190;
    public const double EscalaMapaCompacto = 0.75;

    /// <summary>Rango del fondo: el texto nunca se atenua (UI-SPEC 3.3).</summary>
    public const double AlfaMinimo = 0.80, AlfaMaximo = 0.95, AlfaPorDefecto = 0.88;
    public const double AlfaMapaMenos = 0.04;

    public static double LimitarEscala(double e) => double.IsFinite(e) ? Math.Clamp(e, EscalaMinima, EscalaMaxima) : 1.0;
    public static double LimitarAlfa(double a) => double.IsFinite(a) ? Math.Clamp(a, AlfaMinimo, AlfaMaximo) : AlfaPorDefecto;

    public static double AnchoDeEscala(double escala) => AnchoBase * LimitarEscala(escala);
    public static double EscalaDeAncho(double ancho) => LimitarEscala(ancho / AnchoBase);

    /// <summary>Slider 0..1 -> alfa del fondo 0,80..0,95.</summary>
    public static double AlfaDeSlider(double slider) => AlfaMinimo + Math.Clamp(slider, 0, 1) * (AlfaMaximo - AlfaMinimo);
    public static double SliderDeAlfa(double alfa) => (LimitarAlfa(alfa) - AlfaMinimo) / (AlfaMaximo - AlfaMinimo);

    /// <summary>Alfa efectivo del fondo del panel: 100 % mientras la ventana esta desbloqueada.</summary>
    public static double AlfaPanel(double alfaConfig, bool desbloqueada) => desbloqueada ? 1.0 : LimitarAlfa(alfaConfig);
    /// <summary>El mapa es 0,04 mas transparente que el panel (0,84 contra 0,88), sin salir del rango.</summary>
    public static double AlfaMapa(double alfaConfig, bool desbloqueada)
        => desbloqueada ? 1.0 : Math.Clamp(LimitarAlfa(alfaConfig) - AlfaMapaMenos, AlfaMinimo, AlfaMaximo);
}

/// <summary>Reglas de tiempo anti-parpadeo de la interfaz (UI-SPEC 3.5).</summary>
public static class TiemposUi
{
    public const double FundidoEntradaGuiaS = 0.120;
    public const double FundidoSalidaGuiaS = 0.200;
    public const double FundidoFranjaS = 0.080;
    /// <summary>Las cifras se actualizan como maximo 10 veces por segundo.</summary>
    public const double PeriodoCifrasS = 0.100;
}
