using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

/// <summary>
/// Lo que el decisor resuelve para una muestra de telemetria.
/// <see cref="Progreso"/>: 0..1, cuanto se ve el simbolo (0 = nada, 1 = pleno en el punto de frenado).
/// <see cref="Pitar"/>: verdadero solo en la muestra en que se llega al punto de frenado y hay que pitar.
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
/// Reglas (por curva y por pasada):
///  - Solo curvas de FRENADA, y solo con el punto de frenado de la referencia principal elegida: si la curva no tiene
///    bloque de esa referencia (la guia cae a la otra para mostrar algo) no hay ni pitido ni simbolo.
///  - Rampa: el simbolo sube de 0 a 1 segun el tiempo que falta para el punto (metros / velocidad). Empieza a
///    <see cref="RampaS"/> segundos antes y llega a 1 justo en el punto. Nunca baja por ruido de velocidad (se guarda el maximo).
///  - Pleno: se mantiene en 1 hasta que el pedal pasa de <see cref="UmbralFreno"/>, pasan <see cref="DuracionS"/> segundos
///    desde el punto, o la guia deja de estar en FRENA (paso el vertice).
///  - Pitido: una sola vez, al llegar al punto, si no vas frenando.
///  - Ya frenando: si el pedal pasa del umbral dentro de la zona inmediata (ultimos <see cref="ZonaInmediataS"/> s o
///    <see cref="ZonaInmediataM"/> m antes del punto) o al llegar, esa pasada queda "atendida": ni simbolo ni pitido,
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

    private double _rampa = RampaPorDefectoS;
    private double _duracion = AvisoFrenada.DuracionPorDefectoS;

    private int _curva = -1;
    private bool _atendida;
    private double _maxProgreso;
    private double? _llegada;

    /// <summary>Pedal de freno (0..1) por encima del cual se considera que ya estas frenando.</summary>
    public double UmbralFreno { get; set; } = UmbralFrenoPorDefecto;
    public double ZonaInmediataS { get; set; } = ZonaInmediataSPorDefecto;
    public double ZonaInmediataM { get; set; } = ZonaInmediataMPorDefecto;

    /// <summary>Segundos antes del punto de frenado en que el simbolo empieza a verse (1 a 3).</summary>
    public double RampaS { get => _rampa; set => _rampa = LimitarRampa(value); }

    /// <summary>Segundos que el simbolo pleno queda despues del punto si no frenas (1 a 2).</summary>
    public double DuracionS { get => _duracion; set => _duracion = AvisoFrenada.LimitarDuracion(value); }

    public static double LimitarRampa(double s)
        => double.IsFinite(s) ? Math.Clamp(s, RampaMinS, RampaMaxS) : RampaPorDefectoS;

    public void Reiniciar()
    {
        _curva = -1; _atendida = false; _maxProgreso = 0; _llegada = null;
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
        double restante = metros / v;
        bool enZona = enFrena || metros <= ZonaInmediataM || restante <= ZonaInmediataS;

        if (_llegada is double ll)
        {
            // ya estaba en el punto: se apaga al frenar, por tiempo o si la guia sale de FRENA
            if (!enFrena || pisa || t - ll >= _duracion) _atendida = true;
        }
        else if (pisa && enZona) _atendida = true;
        if (_atendida) return DecisionAviso.Nada;

        bool pitar = false;
        if (enFrena && _llegada is null)
        {
            _llegada = t;
            pitar = !pisa;
        }

        _maxProgreso = Math.Max(_maxProgreso, enFrena ? 1 : ProgresoRampa(restante, _rampa));
        double progreso = pisa ? 0 : _maxProgreso;          // un toque de pedal lejos del punto oculta mientras dura
        double expira = _llegada is double l2 ? l2 + _duracion : double.PositiveInfinity;
        return new DecisionAviso(progreso, pitar, expira);
    }
}
