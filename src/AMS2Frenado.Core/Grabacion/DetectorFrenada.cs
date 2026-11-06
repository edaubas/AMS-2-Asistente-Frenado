namespace AMS2Frenado.Core.Grabacion;

public enum ModoDetector
{
    /// <summary>Usa el pedal de freno (solo el auto visto).</summary>
    Pedal,
    /// <summary>Infiere la frenada por la caida de velocidad (la IA).</summary>
    Velocidad,
}

/// <summary>
/// Umbrales del detector. Los tiempos y el suavizado estan pensados para ~27 Hz (frecuencia
/// efectiva medida en una captura real de AMS2), no para 60 Hz como el prototipo.
/// </summary>
public sealed record ParametrosDetector
{
    /// <summary>Velocidad minima (m/s) para considerar una frenada.</summary>
    public double VMin { get; init; } = 20.0;
    /// <summary>Caida minima (m/s) entre el inicio y el vertice para emitir el evento.</summary>
    public double DvMin { get; init; } = 30 / 3.6;
    public double BOn { get; init; } = 0.25;
    public double BOff { get; init; } = 0.10;
    /// <summary>Desaceleracion (m/s2) que dispara / sostiene la frenada por velocidad.</summary>
    public double AOn { get; init; } = 3.0;
    public double AOff { get; init; } = 1.0;
    /// <summary>Segundos soltado el freno antes de dar por terminada la frenada.</summary>
    public double TOff { get; init; } = 0.2;
    /// <summary>Metros despues de soltar en los que se sigue buscando el vertice.</summary>
    public double PostM { get; init; } = 120.0;
    /// <summary>Constante de tiempo (s) del suavizado de la aceleracion. alfa = 1 - exp(-dt/tau).</summary>
    public double TauAcel { get; init; } = 0.06;
    /// <summary>Un hueco de mas de esto (s) entre muestras reinicia el detector.</summary>
    public double MaxHueco { get; init; } = 0.3;
    /// <summary>Subida de velocidad (m/s) sobre el minimo que da por cerrado el vertice.</summary>
    public double Subida { get; init; } = 3.0;
    public int Prioridad { get; init; } = 3;

    /// <summary>Frenadas: caida de 30 km/h o mas.</summary>
    public static ParametrosDetector Fuerte { get; } = new();

    /// <summary>Toques y levantadas: umbrales mas bajos, caida de 8 km/h o mas.</summary>
    public static ParametrosDetector Suave { get; } = new()
    {
        DvMin = 8 / 3.6,
        AOn = 1.0,
        AOff = 0.4,
        TOff = 0.25,
        Prioridad = 2,
    };
}

/// <summary>
/// Detecta frenadas de un participante (port de Detector de prototipo/frenado.py).
/// El inicio se toma cuando la senal supera el umbral debil, no el fuerte, para reducir el retraso.
/// </summary>
public sealed class DetectorFrenada
{
    private enum Estado { Libre, Frenando, Post }

    private readonly double _largo;
    private readonly ModoDetector _modo;
    private readonly ParametrosDetector _p;

    private Estado _estado = Estado.Libre;
    private bool _tieneAnterior;
    private double _prevD, _prevT, _prevV;
    private double _u;                   // distancia acumulada sin saltos de vuelta
    private double? _a;                  // aceleracion suavizada (m/s2)
    private (double u, double d, double v)? _onset;

    private double _iniU, _iniD, _iniV, _vMinPos, _dMin, _finU;
    private double? _offT, _offU;
    private int _vuelta;

    public DetectorFrenada(double largo, ModoDetector modo, ParametrosDetector? p = null)
    {
        _largo = largo;
        _modo = modo;
        _p = p ?? ParametrosDetector.Fuerte;
    }

    /// <summary>Descarta el estado (hueco de datos, cambio de pista). Un evento en curso se pierde.</summary>
    public void Reiniciar()
    {
        _estado = Estado.Libre;
        _tieneAnterior = false;
        _a = null;
        _onset = null;
    }

    /// <summary>Procesa una muestra; agrega a <paramref name="salida"/> los eventos que terminaron.</summary>
    public void Actualizar(double d, double v, double? freno, double t, int vuelta, List<EventoFrenada> salida)
    {
        if (_modo == ModoDetector.Pedal && freno is null)
            throw new ArgumentException("modo pedal: este participante no tiene dato de freno");

        if (_tieneAnterior && (t - _prevT > _p.MaxHueco || t <= _prevT))
        {
            // hueco grande (o reloj hacia atras): lo que estaba en curso ya no es confiable
            if (_estado == Estado.Post) Cerrar(salida);
            Reiniciar();
        }

        double dt = t - _prevT;
        _u = !_tieneAnterior ? d : _u + Geometria.DifCirc(d, _prevD, _largo);
        if (_tieneAnterior)
        {
            double aRaw = (v - _prevV) / dt;
            double alfa = 1 - Math.Exp(-dt / _p.TauAcel);
            _a = _a is null ? aRaw : _a + alfa * (aRaw - _a);
        }
        _tieneAnterior = true;
        _prevD = d; _prevT = t; _prevV = v;

        bool on, off;
        if (_modo == ModoDetector.Pedal)
        {
            on = freno!.Value >= _p.BOn && v >= _p.VMin;
            off = freno.Value < _p.BOff;
        }
        else
        {
            on = _a is not null && _a <= -_p.AOn && v >= _p.VMin;
            off = _a is null || _a > -_p.AOff;
        }

        if (off) _onset = null;
        else _onset ??= (_u, d, v);

        if (_estado == Estado.Libre)
        {
            if (on && _onset is not null) Empezar(d, v, vuelta);
            return;
        }

        if (v < _vMinPos) { _vMinPos = v; _dMin = d; }

        if (_estado == Estado.Frenando)
        {
            if (off)
            {
                if (_offT is null) { _offT = t; _offU = _u; }
                else if (t - _offT >= _p.TOff)
                {
                    _finU = _offU!.Value;
                    _estado = Estado.Post;
                }
            }
            else _offT = null;
            return;
        }

        // Post: seguimos buscando la velocidad minima (el vertice)
        if (on && _onset is not null)            // otra frenada encadenada
        {
            Cerrar(salida);
            Empezar(d, v, vuelta);
        }
        else if (_u - _finU >= _p.PostM || v > _vMinPos + _p.Subida)
        {
            Cerrar(salida);
        }
    }

    private void Empezar(double d, double v, int vuelta)
    {
        var (u0, d0, v0) = _onset!.Value;
        _estado = Estado.Frenando;
        _iniU = u0; _iniD = d0; _iniV = v0;
        _vuelta = vuelta;
        _vMinPos = v; _dMin = d;
        _offT = null; _offU = null;
    }

    private void Cerrar(List<EventoFrenada> salida)
    {
        _estado = Estado.Libre;
        double caida = _iniV - _vMinPos;
        if (caida < _p.DvMin) return;     // toque de freno demasiado chico
        salida.Add(new EventoFrenada(_iniD, _finU - _iniU, _dMin, _iniV, _vMinPos, _vuelta,
            Tipos.Clasificar(caida), _p.Prioridad));
    }
}

/// <summary>
/// Levantada por acelerador (solo el auto visto): suelta el acelerador sin tocar el freno, un rato corto.
/// Si aparece el freno el episodio se descarta: esa curva ya la ve el detector de frenadas.
/// </summary>
public sealed class DetectorLevantada
{
    private readonly double _largo;
    private readonly double _vMin, _acelOff, _freno, _tMin, _tMax, _maxHueco;

    private bool _activo, _tieneAnterior;
    private double _prevT, _prevD, _u, _u0, _d0, _v0, _t0, _vMinPos, _dMin;
    private int _vuelta;

    public DetectorLevantada(double largo, double vMin = 20.0, double acelOff = 0.15, double freno = 0.05,
        double tMin = 0.4, double tMax = 3.0, double maxHueco = 0.3)
    {
        _largo = largo; _vMin = vMin; _acelOff = acelOff; _freno = freno;
        _tMin = tMin; _tMax = tMax; _maxHueco = maxHueco;
    }

    public void Reiniciar() { _activo = false; _tieneAnterior = false; }

    public void Actualizar(double d, double v, double freno, double acelerador, double t, int vuelta, List<EventoFrenada> salida)
    {
        if (_tieneAnterior && (t - _prevT > _maxHueco || t <= _prevT)) Reiniciar();
        _u = !_tieneAnterior ? d : _u + Geometria.DifCirc(d, _prevD, _largo);
        _tieneAnterior = true;
        _prevT = t; _prevD = d;

        bool levantado = acelerador < _acelOff && freno < _freno && v >= _vMin;
        if (levantado)
        {
            if (!_activo)
            {
                _activo = true;
                _u0 = _u; _d0 = d; _v0 = v; _t0 = t; _vMinPos = v; _dMin = d; _vuelta = vuelta;
            }
            else if (v < _vMinPos) { _vMinPos = v; _dMin = d; }
            return;
        }
        if (!_activo) return;
        _activo = false;
        bool frenoAparecio = freno >= _freno;
        double dur = t - _t0;
        if (frenoAparecio || dur < _tMin || dur > _tMax) return;
        salida.Add(new EventoFrenada(_d0, _u - _u0, _dMin, _v0, _vMinPos, _vuelta,
            Tipos.Clasificar(_v0 - _vMinPos), 1));
    }
}
