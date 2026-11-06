namespace AMS2Frenado.Core.Overlay;

/// <summary>
/// Lo que la ventana del aviso dibuja en este instante: opacidad 0..1 del simbolo (0 = nada) y una escala leve
/// que acompana a la opacidad (el simbolo "crece" un poco mientras aparece).
/// </summary>
public readonly record struct CuadroAviso(double Opacidad)
{
    public const double EscalaMinima = 0.85;
    public bool Visible => Opacidad > 0;
    /// <summary>0,85 con el simbolo recien visible, 1 cuando esta pleno.</summary>
    public double Escala => EscalaMinima + (1 - EscalaMinima) * Math.Clamp(Opacidad, 0, 1);
}

/// <summary>
/// Presentacion del simbolo de frenada (la decision la toma <see cref="DecisorAviso"/>): lleva la opacidad hacia el
/// objetivo que da el decisor. Al subir sigue la rampa casi sin demora (tope de <see cref="SubidaS"/> para absorber
/// saltos, por ejemplo un hueco de datos); al bajar hace un fundido corto de <see cref="FundidoS"/>. Se llama a ~60 Hz con
/// o sin muestra nueva, asi el fundido avanza con el reloj y no con la telemetria (~27 Hz). Logica pura, sin ventanas.
/// Sin probar en AMS2 en vivo.
/// </summary>
public sealed class AvisoFrenada
{
    public const double DuracionMinimaS = 1.0, DuracionMaximaS = 2.0, DuracionPorDefectoS = 1.5;
    public const double UmbralFrenoPorDefecto = DecisorAviso.UmbralFrenoPorDefecto;
    /// <summary>Fundido al desaparecer.</summary>
    public const double FundidoS = 0.080;
    /// <summary>Tiempo que tarda en ir de 0 a 1 como maximo al subir (una rampa normal dura ~2 s y no lo toca).</summary>
    public const double SubidaS = 0.080;
    /// <summary>Tope del paso de reloj que avanza el fundido: tras una pausa larga no salta de 0 a 1 de golpe.</summary>
    private const double MaxPasoRelojS = 0.040;

    private double _duracion = DuracionPorDefectoS;
    private bool _prueba;
    private double _tPrueba;
    private double _opacidad;
    private double? _t;

    public bool Habilitado { get; set; } = true;

    /// <summary>Duracion de "Probar aviso" y del simbolo pleno (1 a 2 s).</summary>
    public double DuracionS
    {
        get => _duracion;
        set => _duracion = LimitarDuracion(value);
    }

    public static double LimitarDuracion(double s)
        => double.IsFinite(s) ? Math.Clamp(s, DuracionMinimaS, DuracionMaximaS) : DuracionPorDefectoS;

    /// <summary>Muestra el simbolo pleno durante <see cref="DuracionS"/> sin estar en pista ("Probar aviso"). Ignora <see cref="Habilitado"/>.</summary>
    public void Probar(double segundos) { _prueba = true; _tPrueba = segundos; }

    /// <summary>
    /// <paramref name="decision"/> es la ultima decision del <see cref="DecisorAviso"/> (<see cref="DecisionAviso.Nada"/> sin
    /// lectura valida). Devuelve el cuadro con la opacidad ya suavizada.
    /// </summary>
    public CuadroAviso Actualizar(DecisionAviso decision, double segundos)
    {
        double dt = _t is double t0 ? Math.Clamp(segundos - t0, 0, MaxPasoRelojS) : 0;
        _t = segundos;

        if (_prueba && segundos - _tPrueba >= _duracion) _prueba = false;

        double objetivo = Habilitado && segundos < decision.ExpiraEn ? Math.Clamp(decision.Progreso, 0, 1) : 0;
        if (_prueba) objetivo = 1;

        _opacidad = objetivo > _opacidad
            ? Math.Min(objetivo, _opacidad + dt / SubidaS)
            : Math.Max(objetivo, _opacidad - dt / FundidoS);
        return new CuadroAviso(_opacidad);
    }
}
