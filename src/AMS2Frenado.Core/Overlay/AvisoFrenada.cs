namespace AMS2Frenado.Core.Overlay;

/// <summary>
/// Lo que la ventana del aviso dibuja en este instante: opacidad 0..1 del simbolo (0 = nada) y una escala leve
/// que acompana a la opacidad (el simbolo "crece" un poco mientras aparece). <see cref="Pitar"/> solo se usa en
/// "Probar aviso" / "Probar pitido": es el cuadro en que la rampa de prueba llega al 100 % (T_aviso de la prueba).
/// </summary>
public readonly record struct CuadroAviso(double Opacidad, bool Pitar = false)
{
    public const double EscalaMinima = 0.85;
    public bool Visible => Opacidad > 0;
    /// <summary>0,85 con el simbolo recien visible, 1 cuando esta pleno.</summary>
    public double Escala => EscalaMinima + (1 - EscalaMinima) * Math.Clamp(Opacidad, 0, 1);
}

/// <summary>
/// Presentacion del simbolo de frenada (la decision la toma <see cref="DecisorAviso"/>): lleva la opacidad al objetivo que
/// da el decisor. La rampa YA es la entrada del simbolo, asi que al SUBIR la opacidad es el objetivo, sin fundido ni tope
/// propios (cualquier suavizado de entrada retrasaria el 100 % respecto del pitido). Solo al BAJAR hace un fundido corto de
/// <see cref="FundidoS"/>. Se llama a ~60 Hz con o sin muestra nueva, asi el fundido avanza con el reloj y no con la
/// telemetria (~27 Hz). Logica pura, sin ventanas. Sin probar en AMS2 en vivo.
/// </summary>
public sealed class AvisoFrenada
{
    public const double DuracionMinimaS = 1.0, DuracionMaximaS = 2.0, DuracionPorDefectoS = 1.5;
    public const double UmbralFrenoPorDefecto = DecisorAviso.UmbralFrenoPorDefecto;
    /// <summary>Fundido al desaparecer.</summary>
    public const double FundidoS = 0.080;

    private double _duracion = DuracionPorDefectoS;
    private bool _prueba, _pruebaSimbolo, _pruebaPitido, _pruebaPito;
    private double _tPrueba, _pruebaRampa, _pruebaAnticipacion;
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

    /// <summary>
    /// Prueba sin estar en pista ("Probar aviso" / "Probar pitido"): reproduce una pasada sintetica. La opacidad sube de 0 a 1
    /// durante <paramref name="rampaS"/> (con 0, de golpe); T_aviso de la prueba es el fin de la rampa y ahi sale
    /// <see cref="CuadroAviso.Pitar"/> (si <paramref name="pitar"/>); el "punto de frenado" llega <paramref name="anticipacionS"/>
    /// despues de T_aviso y el simbolo pleno sigue <see cref="DuracionS"/> desde ese punto. Ignora <see cref="Habilitado"/>
    /// salvo que <paramref name="simbolo"/> sea falso (solo pitido).
    /// </summary>
    public void Probar(double segundos, double rampaS = 0, double anticipacionS = 0, bool pitar = false, bool simbolo = true)
    {
        _prueba = true; _tPrueba = segundos; _pruebaPito = false;
        _pruebaRampa = double.IsFinite(rampaS) ? Math.Max(rampaS, 0) : 0;
        _pruebaAnticipacion = double.IsFinite(anticipacionS) ? Math.Max(anticipacionS, 0) : 0;
        _pruebaPitido = pitar; _pruebaSimbolo = simbolo;
    }

    /// <summary>
    /// <paramref name="decision"/> es la ultima decision del <see cref="DecisorAviso"/> (<see cref="DecisionAviso.Nada"/> sin
    /// lectura valida). Devuelve el cuadro con la opacidad ya suavizada.
    /// </summary>
    public CuadroAviso Actualizar(DecisionAviso decision, double segundos)
    {
        double dt = _t is double t0 ? Math.Max(segundos - t0, 0) : 0;
        _t = segundos;

        bool pitar = false;
        double objetivo = Habilitado && segundos < decision.ExpiraEn ? Math.Clamp(decision.Progreso, 0, 1) : 0;
        if (_prueba)
        {
            double tAviso = _tPrueba + _pruebaRampa;
            if (segundos >= tAviso + _pruebaAnticipacion + _duracion) _prueba = false;
            else
            {
                double p = _pruebaRampa <= 0 || segundos >= tAviso ? 1 : Math.Clamp((segundos - _tPrueba) / _pruebaRampa, 0, 1);
                if (_pruebaSimbolo) objetivo = Math.Max(objetivo, p);
                if (segundos >= tAviso && !_pruebaPito) { _pruebaPito = true; pitar = _pruebaPitido; }
            }
        }

        _opacidad = objetivo >= _opacidad ? objetivo : Math.Max(objetivo, _opacidad - dt / FundidoS);
        return new CuadroAviso(_opacidad, pitar);
    }
}
