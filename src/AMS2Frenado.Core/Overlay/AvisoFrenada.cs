using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

/// <summary>Lo que la ventana del aviso dibuja en este instante: opacidad 0..1 del simbolo (0 = nada).</summary>
public readonly record struct CuadroAviso(double Opacidad)
{
    public bool Visible => Opacidad > 0;
}

/// <summary>
/// Aviso de frenada estilo "luz de freno": decide cuando mostrar y ocultar el simbolo rojo. Logica pura, sin ventanas.
///  - Aparece al entrar el <see cref="MotorGuia"/> en FRENA (el punto de frenado).
///  - Desaparece lo primero que ocurra: el usuario frena (mBrake del auto visto por encima de <see cref="UmbralFreno"/>),
///    pasan <see cref="DuracionS"/> segundos (1 a 2, por defecto 1,5), o la guia deja de estar en FRENA (paso el vertice).
///  - No vuelve a aparecer en la misma curva en la misma pasada: la curva queda "atendida" hasta que la guia cambie de
///    curva o vuelva a Lejos / SinDatos.
///  - Si ya venias frenando cuando llega el punto de frenado, no aparece (no tendria sentido avisar).
///  - Fundido de 80 ms al aparecer y al desaparecer; sin parpadeo ni pulso.
/// Sin probar en AMS2 en vivo: el umbral y la duracion se calibran en pista.
/// </summary>
public sealed class AvisoFrenada
{
    public const double DuracionMinimaS = 1.0, DuracionMaximaS = 2.0, DuracionPorDefectoS = 1.5;
    public const double UmbralFrenoPorDefecto = 0.15;
    public const double FundidoS = 0.080;
    /// <summary>Tope del paso de reloj que avanza el fundido: tras una pausa larga no salta de 0 a 1 de golpe.</summary>
    private const double MaxPasoRelojS = 0.040;

    private double _duracion = DuracionPorDefectoS;
    private bool _activo;
    private int _curvaActiva = -1;
    private double _tInicio;
    private int _atendida = -1;
    private bool _prueba;
    private double _tPrueba;
    private double _opacidad;
    private double? _t;

    public bool Habilitado { get; set; } = true;
    /// <summary>Pedal de freno (0..1) por encima del cual se considera que el usuario ya frena.</summary>
    public double UmbralFreno { get; set; } = UmbralFrenoPorDefecto;

    public double DuracionS
    {
        get => _duracion;
        set => _duracion = LimitarDuracion(value);
    }

    public static double LimitarDuracion(double s)
        => double.IsFinite(s) ? Math.Clamp(s, DuracionMinimaS, DuracionMaximaS) : DuracionPorDefectoS;

    /// <summary>Muestra el simbolo durante <see cref="DuracionS"/> sin estar en pista ("Probar aviso"). Ignora <see cref="Habilitado"/>.</summary>
    public void Probar(double segundos) { _prueba = true; _tPrueba = segundos; }

    /// <summary>Verdadero si la decision actual es mostrar (antes del fundido).</summary>
    public bool Mostrando => _activo || _prueba;

    /// <summary>
    /// Se llama en cada tick (~60 Hz). <paramref name="panel"/> es la ultima guia (null sin lectura valida) y
    /// <paramref name="freno"/> el pedal de freno del auto visto. Devuelve la opacidad ya con el fundido.
    /// </summary>
    public CuadroAviso Actualizar(PanelGuia? panel, double freno, double segundos)
    {
        double dt = _t is double t0 ? Math.Clamp(segundos - t0, 0, MaxPasoRelojS) : 0;
        _t = segundos;

        int curva = panel?.IndiceCurva ?? -1;
        bool enFrena = panel is { Estado: EstadoPanel.Frena };
        bool frenando = double.IsFinite(freno) && freno > UmbralFreno;

        // la curva atendida se olvida al cambiar de curva o al volver a Lejos / SinDatos
        if (_atendida >= 0 && (panel is null || curva != _atendida || panel.Estado is EstadoPanel.Lejos or EstadoPanel.SinDatos))
            _atendida = -1;

        if (_activo)
        {
            if (!Habilitado || !enFrena || curva != _curvaActiva || frenando || segundos - _tInicio >= _duracion)
                _activo = false;
        }
        else if (Habilitado && enFrena && curva != _atendida)
        {
            _atendida = curva;
            if (!frenando) { _activo = true; _curvaActiva = curva; _tInicio = segundos; }
        }

        if (_prueba && segundos - _tPrueba >= _duracion) _prueba = false;

        double objetivo = _activo || _prueba ? 1 : 0;
        double paso = dt / FundidoS;
        _opacidad = objetivo > _opacidad ? Math.Min(objetivo, _opacidad + paso) : Math.Max(objetivo, _opacidad - paso);
        return new CuadroAviso(_opacidad);
    }
}
