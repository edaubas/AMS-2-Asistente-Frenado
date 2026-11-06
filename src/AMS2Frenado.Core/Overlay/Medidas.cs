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
    public const string ReglaFondo = "#232A31";
    public const string ReglaBase = "#33414E";
    public const string ReglaMarcas = "#4A5561";
    public const string ChipClaseFondo = "#2A3139", ChipClaseTexto = "#EEF1F4";
    public const string ChipMostrarFondo = "#1E3A2A", ChipMostrarTexto = "#8EE0A9";
    public const string ChipGrabandoFondo = "#4A1F1C", ChipGrabandoTexto = "#FF9C94", ChipGrabandoPunto = "#FF5A4E";
    public const string ChipDesbloqueadoFondo = "#17304D", ChipDesbloqueadoTexto = "#8CC3FF";
    // ---- fase 6 (UI-SPEC 7.2): superficies opacas, bordes, acento rojo y textos de los controles
    /// <summary>Fondo opaco del menu de configuracion.</summary>
    public const string Menu = "#0F1216";
    /// <summary>Tarjetas y botones sobre el menu (reposo / hover / presionado).</summary>
    public const string Sup = "#161A1F", SupHover = "#222831", SupPresionado = "#2F3842";
    public const string Borde = "#2E3640", BordeBoton = "#5A6673";
    /// <summary>Pista del deslizador (3,56:1 sobre Sup).</summary>
    public const string PistaDeslizador = "#66727F";
    /// <summary>Texto de etiquetas rojas chicas y pin fijado (4,97:1 sobre el panel claro del peor caso).</summary>
    public const string RojoEtiqueta = "#FF8A80";
    /// <summary>Rojo de hover del boton CERRAR.</summary>
    public const string RojoHover = "#D8382E";
    public const string PinFijadoFondo = "#3A1614";
    /// <summary>Segmento recorrido de la barra segmentada y borde del interruptor apagado.</summary>
    public const string SegmentoRecorrido = "#7C8794";
    public const string TeclaFondo = "#2A3139";
    /// <summary>Aviso de frenada: simbolo rojo con contorno oscuro fino (UI-SPEC 2.1).</summary>
    public const string AvisoRojo = "#E53935", AvisoContorno = "#0C0E11";
}

/// <summary>Medidas en px = DIP a 96 dpi (docs/UI-SPEC.md, 3.2).</summary>
public static class MedidasUi
{
    public const double AnchoBase = 320, AnchoMinimo = 240, AnchoMaximo = 480;
    public const double EscalaMinima = 0.75, EscalaMaxima = 1.5;
    public const double Margen = 24;
    public const double AltoFranja = 40;

    /// <summary>
    /// Rango del fondo: el texto nunca se atenua. Fase 6: el minimo sube de 0,80 a 0,85 (con la textura al pico y fondo blanco,
    /// con 0,80 fallan el azul, el naranja, el apoyo y la etiqueta roja; ver UI-SPEC 7.8). Un config.json viejo con menos
    /// de 0,85 se lleva a 0,85 al leerlo (<see cref="LimitarAlfa"/>).
    /// </summary>
    public const double AlfaMinimo = 0.85, AlfaMaximo = 0.95, AlfaPorDefecto = 0.88;

    /// <summary>Esquina cortada a 45 grados, abajo a la derecha (16 en el menu). Sin radios redondeados.</summary>
    public const double CorteEsquina = 14, CorteEsquinaMenu = 16;
    /// <summary>Textura "carbono": tile de 6 x 6 DIP con una diagonal blanca al 4 %.</summary>
    public const double TexturaLado = 6, TexturaAlfa = 0.04;
    /// <summary>Relleno de la tarjeta: arriba, lados y abajo (18: el corte no pisa texto).</summary>
    public const double RellenoArriba = 12, RellenoLados = 14, RellenoAbajo = 18;
    public const double AltoCabecera = 72, SeparacionTarjetas = 8;
    /// <summary>Barra segmentada: 16 segmentos de 10 de alto con separacion de 3.</summary>
    public const int SegmentosBarra = 16;
    public const double AltoSegmento = 10, SeparacionSegmento = 3;

    /// <summary>Botones de la barra: 28 dibujados, 32 clicables; ventana de botones de 140 x 32 (292 x 32 al confirmar la X).</summary>
    public const double BotonVisible = 28, BotonClicable = 32, BarraAncho = 140, BarraAncha = 292, BarraAlto = 32;
    /// <summary>Por debajo de esta escala del overlay los botones se agrandan (escala inversa) para no bajar de 28 px reales.</summary>
    public const double EscalaMinimaBotones = 0.875;
    public const double MenuAncho = 624;

    /// <summary>
    /// Escala que se le aplica a la barra de botones para que el area clicable de sus botones mida 28 px reales como minimo:
    /// con escala del overlay menor a 0,875 vale 0,875 / escala; si no, 1. La barra sigue la escala del overlay y, debajo de 0,875, se queda en 0,875.
    /// </summary>
    public static double EscalaBarra(double escalaOverlay)
    {
        double e = LimitarEscala(escalaOverlay);
        return e < EscalaMinimaBotones ? EscalaMinimaBotones / e : 1.0;
    }
    /// <summary>
    /// Lado real (px) del area clicable de un boton (32 a escala 1) con la escala del overlay y la inversa de la barra: nunca
    /// baja de 28 (= 32 x 0,875). Ojo: el dibujo de 28 puede quedar en 24,5 px reales; lo que se garantiza es el area clicable.
    /// </summary>
    public static double AreaClicableRealPx(double escalaOverlay) => BotonClicable * LimitarEscala(escalaOverlay) * EscalaBarra(escalaOverlay);

    public static double LimitarEscala(double e) => double.IsFinite(e) ? Math.Clamp(e, EscalaMinima, EscalaMaxima) : 1.0;
    public static double LimitarAlfa(double a) => double.IsFinite(a) ? Math.Clamp(a, AlfaMinimo, AlfaMaximo) : AlfaPorDefecto;

    public static double AnchoDeEscala(double escala) => AnchoBase * LimitarEscala(escala);
    public static double EscalaDeAncho(double ancho) => LimitarEscala(ancho / AnchoBase);

    /// <summary>Slider 0..1 -> alfa del fondo 0,85..0,95.</summary>
    public static double AlfaDeSlider(double slider) => AlfaMinimo + Math.Clamp(slider, 0, 1) * (AlfaMaximo - AlfaMinimo);
    public static double SliderDeAlfa(double alfa) => (LimitarAlfa(alfa) - AlfaMinimo) / (AlfaMaximo - AlfaMinimo);

    /// <summary>Alfa efectivo del fondo del panel: 100 % mientras la ventana esta desbloqueada.</summary>
    public static double AlfaPanel(double alfaConfig, bool desbloqueada) => desbloqueada ? 1.0 : LimitarAlfa(alfaConfig);
}

/// <summary>Medidas de la ventana del aviso de frenada (px = DIP a 96 dpi, UI-SPEC 2.1).</summary>
public static class MedidasAviso
{
    /// <summary>Lado del simbolo a escala 100 %.</summary>
    public const double LadoBase = 120;
    public const double EscalaMinima = 0.5, EscalaMaxima = 3.0;

    public static double LimitarEscala(double e) => double.IsFinite(e) ? Math.Clamp(e, EscalaMinima, EscalaMaxima) : 1.0;
    public static double LadoDeEscala(double escala) => LadoBase * LimitarEscala(escala);
    public static double EscalaDeLado(double lado) => LimitarEscala(lado / LadoBase);
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
