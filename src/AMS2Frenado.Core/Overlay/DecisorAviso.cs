using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

/// <summary>
/// Lo que el decisor resuelve para una muestra de telemetria.
/// <see cref="Progreso"/>: 0..1, cuanto se ve el simbolo (0 = nada, 1 = pleno en T_aviso).
/// <see cref="Pitar"/>: verdadero solo en la muestra en que se llega a T_aviso (punto menos anticipacion) y hay que pitar;
/// en esa misma muestra el progreso es 1.
/// <see cref="ExpiraEn"/>: instante (reloj de la telemetria) en que el simbolo pleno se apaga solo; infinito si no corre.
/// </summary>
public readonly record struct DecisionAviso(double Progreso, bool Pitar, double ExpiraEn)
{
    public static readonly DecisionAviso Nada = new(0, false, double.PositiveInfinity);
}

/// <summary>
/// Decide cuando avisar la frenada. Es la unica fuente de verdad: el pitido y el simbolo salen de aca.
/// Logica pura, sin ventanas ni reloj propio (el tiempo llega por parametro). Sin probar en AMS2 en vivo.
///
/// INSTANTE DE AVISO (T_aviso): es el punto de frenado menos la anticipacion. Con A = <see cref="AnticipacionS"/>
/// (+ <see cref="CompensacionLatenciaS"/>), T_aviso es el momento en que faltan A segundos para el punto, con el mismo
/// "tiempo restante" (metros / velocidad actual) que usa la rampa. De ese unico instante dependen los dos: la opacidad del
/// simbolo llega a 1,0 en T_aviso y el pitido se pide en T_aviso, en la MISMA muestra (misma variable
/// <c>llegoAviso</c> en <see cref="Evaluar"/>). La rampa termina en T_aviso (empieza <see cref="RampaS"/> antes).
///
/// Reglas (por curva y por pasada):
///  - Solo curvas de FRENADA, y solo con el punto de frenado de la referencia principal elegida: si la curva no tiene
///    bloque de esa referencia (la guia cae a la otra para mostrar algo) no hay ni pitido ni simbolo.
///  - Rampa: el simbolo sube de 0 a 1 segun el tiempo que falta para T_aviso. Empieza a <see cref="RampaS"/> segundos
///    antes de T_aviso y llega a 1 justo en T_aviso. Nunca baja por ruido de velocidad (se guarda el maximo).
///  - Pleno: se mantiene en 1 hasta que el pedal pasa de <see cref="UmbralFreno"/>, pasan <see cref="DuracionS"/> segundos
///    desde el PUNTO de frenado (no desde T_aviso), o la guia deja de estar en FRENA (paso el vertice).
///  - Pitido: una sola vez, en T_aviso, si no vas frenando.
///  - Ya frenando: si el pedal pasa del umbral dentro de la zona inmediata (ultimos <see cref="ZonaInmediataS"/> s o
///    <see cref="ZonaInmediataM"/> m antes de T_aviso) o al llegar a T_aviso, esa pasada queda "atendida": ni simbolo ni pitido,
///    aunque despues sueltes el pedal. Un toque de pedal mas lejos del punto solo oculta el simbolo mientras dura.
///  - La pasada se olvida al cambiar de curva o al volver a Lejos / SinDatos / sin lectura.
/// </summary>
public sealed class DecisorAviso
{
    public const double UmbralFrenoPorDefecto = 0.15;
    public const double RampaMinS = 1.0, RampaMaxS = 3.0, RampaPorDefectoS = 2.0;
    public const double ZonaInmediataSPorDefecto = 1.0, ZonaInmediataMPorDefecto = 40;
    /// <summary>Piso de velocidad (m/s) para calcular el tiempo restante: parado o casi parado no hay rampa por tiempo.</summary>
    public const double VelocidadMinMs = 3.0;
    /// <summary>Tolerancia (s) para dar por alcanzado T_aviso: evita que el redondeo deje la rampa en 0,9999999 un instante antes del pitido.</summary>
    public const double ToleranciaLlegadaS = 1e-6;
    /// <summary>Anticipacion de T_aviso respecto del punto de frenado: 0 a 2,0 s, paso 0,1 s (0 = suena y llega al 100 % en el punto).</summary>
    public const double AnticipacionMinS = 0, AnticipacionMaxS = 2.0, AnticipacionPasoS = 0.1, AnticipacionPorDefectoS = 0;
    /// <summary>Pasos discretos del submenu provisorio de la bandeja (hasta que exista el deslizador).</summary>
    public static readonly IReadOnlyList<double> AnticipacionesSugeridasS = [0, 0.2, 0.4, 0.6, 0.8, 1.0, 1.5, 2.0];
    /// <summary>
    /// Compensacion de latencia fija por defecto (s): 0. Hay una latencia inevitable entre que el decisor pide el aviso y lo que
    /// se ve/oye (cuantizacion a la muestra de telemetria, ~26-37 ms; ventana transparente con composicion de Windows, 1-2
    /// cuadros a 60 Hz; arranque de <c>SoundPlayer</c>, decenas de ms). NO esta medida en pista ni en Windows: queda en 0 hasta
    /// calibrar con una grabacion de pantalla + audio. Ver <see cref="CompensacionLatenciaS"/>.
    /// </summary>
    public const double CompensacionLatenciaPorDefectoS = 0, CompensacionLatenciaMaxS = 0.25;

    private double _rampa = RampaPorDefectoS;
    private double _duracion = AvisoFrenada.DuracionPorDefectoS;
    private double _anticipacion = AnticipacionPorDefectoS;
    private double _compensacion = CompensacionLatenciaPorDefectoS;

    private int _curva = -1;
    private bool _atendida;
    private double _maxProgreso;
    private double? _llegada;          // instante (reloj) en que se alcanzo T_aviso
    private double _puntoEstimado;     // instante estimado del punto de frenado, mientras no se llego a FRENA
    private double? _puntoReal;        // instante en que la guia paso a FRENA

    /// <summary>Pedal de freno (0..1) por encima del cual se considera que ya estas frenando.</summary>
    public double UmbralFreno { get; set; } = UmbralFrenoPorDefecto;
    public double ZonaInmediataS { get; set; } = ZonaInmediataSPorDefecto;
    public double ZonaInmediataM { get; set; } = ZonaInmediataMPorDefecto;

    /// <summary>Segundos antes del punto de frenado en que el simbolo empieza a verse (1 a 3).</summary>
    public double RampaS { get => _rampa; set => _rampa = LimitarRampa(value); }

    /// <summary>Segundos que el simbolo pleno queda despues del punto si no frenas (1 a 2).</summary>
    public double DuracionS { get => _duracion; set => _duracion = AvisoFrenada.LimitarDuracion(value); }

    /// <summary>
    /// Segundos ANTES del punto de frenado en que suena el pitido y el simbolo llega al 100 % (0 a 2,0). La rampa
    /// se acomoda para terminar ahi. Con 0 coincide con el punto de frenado.
    /// </summary>
    public double AnticipacionS { get => _anticipacion; set => _anticipacion = LimitarAnticipacion(value); }

    /// <summary>
    /// Adelanto extra y fijo de T_aviso (0 a 0,25 s) para compensar la latencia propia de ventana y sonido. Se suma a la
    /// anticipacion y afecta a pitido y simbolo por igual (siguen en la misma muestra). Sin calibrar en pista: 0.
    /// </summary>
    public double CompensacionLatenciaS
    {
        get => _compensacion;
        set => _compensacion = double.IsFinite(value) ? Math.Clamp(value, 0, CompensacionLatenciaMaxS) : CompensacionLatenciaPorDefectoS;
    }

    public static double LimitarAnticipacion(double s)
        => double.IsFinite(s) ? Math.Clamp(s, AnticipacionMinS, AnticipacionMaxS) : AnticipacionPorDefectoS;

    public static double LimitarRampa(double s)
        => double.IsFinite(s) ? Math.Clamp(s, RampaMinS, RampaMaxS) : RampaPorDefectoS;

    public void Reiniciar()
    {
        _curva = -1; _atendida = false; _maxProgreso = 0; _llegada = null; _puntoReal = null; _puntoEstimado = 0;
    }

    /// <summary>Progreso de la rampa en funcion del tiempo restante al punto (0 fuera de la rampa, 1 en el punto).</summary>
    public static double ProgresoRampa(double tiempoRestanteS, double rampaS)
        => !double.IsFinite(tiempoRestanteS) ? 0 : Math.Clamp(1 - tiempoRestanteS / rampaS, 0, 1);

    /// <param name="panel">Ultima guia (null sin lectura valida).</param>
    /// <param name="elegida">Referencia principal elegida por el usuario: la unica que dispara aviso.</param>
    /// <param name="velocidadMs">Velocidad del auto visto, m/s.</param>
    /// <param name="freno">Pedal de freno del auto visto, 0..1.</param>
    /// <param name="t">Reloj en segundos (crece).</param>
    public DecisionAviso Evaluar(PanelGuia? panel, ReferenciaGuia elegida, double velocidadMs, double freno, double t)
    {
        bool hayCurva = panel is { Tipo: TipoCurva.Frenada, IndiceCurva: >= 0 }
            && panel.Estado is EstadoPanel.Aproximacion or EstadoPanel.Inminente or EstadoPanel.Frena;
        // la guia puede seguir mostrando una curva sin bloque de la referencia elegida (cae a la otra): ahi no se avisa
        bool aplica = hayCurva && panel!.Principal == elegida;
        if (!hayCurva || panel!.IndiceCurva != _curva)
        {
            Reiniciar();
            if (hayCurva) _curva = panel!.IndiceCurva;
        }
        if (!aplica) return DecisionAviso.Nada;

        bool pisa = double.IsFinite(freno) && freno > UmbralFreno;
        bool enFrena = panel!.Estado == EstadoPanel.Frena;
        double metros = enFrena ? 0 : (panel.MetrosHastaFrenar ?? double.PositiveInfinity);
        double v = double.IsFinite(velocidadMs) ? Math.Max(velocidadMs, VelocidadMinMs) : VelocidadMinMs;
        double restante = metros / v;                         // segundos hasta el punto de frenado
        double anticipacion = _anticipacion + _compensacion;
        double restanteAviso = restante - anticipacion;       // segundos hasta T_aviso (<= 0: ya se llego)
        // UNA sola condicion para el pitido y el 100 % del simbolo: la guia en FRENA (el punto) o T_aviso alcanzado
        bool llegoAviso = enFrena || restanteAviso <= ToleranciaLlegadaS;
        bool enZona = llegoAviso || metros - anticipacion * v <= ZonaInmediataM || restanteAviso <= ZonaInmediataS;

        if (enFrena) _puntoReal ??= t;
        else _puntoEstimado = t + restante;
        double puntoEn = _puntoReal ?? _puntoEstimado;

        if (_llegada is not null)
        {
            // ya se avisó: se apaga al frenar, por tiempo desde el PUNTO, o si la guia sale de FRENA (vertice / retroceso)
            if ((_puntoReal is not null && !enFrena) || pisa || t - puntoEn >= _duracion) _atendida = true;
        }
        else if (pisa && enZona) _atendida = true;
        if (_atendida) return DecisionAviso.Nada;

        bool pitar = false;
        if (llegoAviso && _llegada is null)
        {
            _llegada = t;
            pitar = !pisa;
        }

        _maxProgreso = Math.Max(_maxProgreso, llegoAviso ? 1 : ProgresoRampa(restanteAviso, _rampa));
        double progreso = pisa ? 0 : _maxProgreso;          // un toque de pedal lejos del punto oculta mientras dura
        double expira = _llegada is not null ? puntoEn + _duracion : double.PositiveInfinity;
        return new DecisionAviso(progreso, pitar, expira);
    }
}
