namespace AMS2Frenado.Core.Grabacion;

public enum ResultadoGrabacion
{
    Ok,
    /// <summary>mVersion distinta de 14.</summary>
    VersionDistinta,
    /// <summary>Largo de pista 0 o sin sentido, o nombre de pista vacio: se espera, no se graba basura.</summary>
    LecturaSinSentido,
    /// <summary>El juego no esta en modo jugando (menu, pausa, replay).</summary>
    NoJugando,
    /// <summary>Segundos que no avanzan (lectura repetida o reloj hacia atras).</summary>
    TiempoInvalido,
}

/// <summary>Cuanto lleva juntado una clase de una pista.</summary>
public sealed record EstadoClase(string Pista, string Variante, string Clase,
    int VueltasIa, int VueltasVos, int EventosIa, int EventosVos);

/// <summary>
/// Graba frenadas, toques y levantadas de todos los participantes, por pista + variante + clase.
/// El auto visto (mViewedParticipantIndex) se detecta con pedales y es "vos"; los demas se detectan
/// por la caida de velocidad y forman la "ia". Una vuelta cuenta solo si no es la primera que se ve,
/// no pasa por boxes (mPitModes != 0) y no esta invalidada (mLapsInvalidated / mLapInvalidated).
/// Solo lee datos: nunca toca el juego.
/// </summary>
public sealed class Grabador
{
    private const int MaxParticipantes = ConstantesShm.MaxParticipantes;
    private const double GraciaInvalidaS = 0.5;   // al cruzar la meta el flag tarda un instante en limpiarse

    private sealed class Fuente
    {
        public readonly Dictionary<ClaveVuelta, List<EventoFrenada>> Eventos = [];
        public readonly HashSet<ClaveVuelta> Vueltas = [];
    }

    private sealed class ClaseEstadoInterno(string clase)
    {
        public readonly string Clase = clase;
        public readonly Fuente Ia = new();
        public readonly Fuente Vos = new();
        public bool Sucia;
    }

    private sealed class PistaEstado(string pista, string variante, double largo)
    {
        public readonly string Pista = pista, Variante = variante;
        public readonly double Largo = largo;
        public readonly ConstructorMapa Mapa = new(largo);
        public readonly Dictionary<string, ClaseEstadoInterno> Clases = [];
        public bool MapaSucio;
    }

    private sealed class Participante
    {
        public PistaEstado Pista = null!;
        public ClaseEstadoInterno Clase = null!;
        public Fuente Fuente = null!;
        public bool EsVos;
        public DetectorFrenada Fuerte = null!, Suave = null!;
        public DetectorLevantada? Levanta;
        public ConstructorMapa.BufferVuelta Buffer = null!;
        public readonly List<EventoFrenada> Salida = [];
        public readonly List<EventoFrenada> Pendientes = [];

        public int Vuelta;
        public bool Sucia = true;          // la primera vuelta que se ve puede ser parcial
        public int UltimaBuena = -1;
        public double InicioVuelta;
        public bool TieneAnterior;
        public double PrevD, PrevT;
    }

    private readonly Almacen _almacen;
    private readonly double _intervaloGuardado;
    private readonly Dictionary<string, PistaEstado> _pistas = [];
    private readonly Participante?[] _part = new Participante?[MaxParticipantes];
    private double _ultimoGuardado = double.NaN;
    private double _tUltimo = double.NegativeInfinity;
    private bool _pendienteGuardar;

    public int Procesadas { get; private set; }
    public int Descartadas { get; private set; }
    public Exception? UltimoErrorGuardado { get; private set; }

    /// <param name="intervaloGuardado">Segundos minimos entre guardados automaticos (0 = guarda en cada vuelta valida).</param>
    public Grabador(Almacen almacen, double intervaloGuardado = 15)
    {
        _almacen = almacen;
        _intervaloGuardado = intervaloGuardado;
    }

    public ResultadoGrabacion Procesar(ResultadoLectura lectura, double segundos)
    {
        if (lectura.Estado != EstadoLectura.Ok) return Descartar(ResultadoGrabacion.LecturaSinSentido);
        var datos = lectura.Datos;
        return Procesar(in datos, segundos);
    }

    public ResultadoGrabacion Procesar(in SharedMemory sm, double t)
    {
        if (sm.mVersion != ConstantesShm.Version) return Descartar(ResultadoGrabacion.VersionDistinta);
        float largoF = sm.mTrackLength;
        string pista = Texto.Leer(sm.mTrackLocation);
        if (!float.IsFinite(largoF) || largoF < 100 || pista.Length == 0)
            return Descartar(ResultadoGrabacion.LecturaSinSentido);
        if (sm.mGameState != 2) return Descartar(ResultadoGrabacion.NoJugando);
        if (!double.IsFinite(t) || t <= _tUltimo) return Descartar(ResultadoGrabacion.TiempoInvalido);
        _tUltimo = t;
        Procesadas++;

        double largo = largoF;
        string variante = Texto.Leer(sm.mTrackVariation);
        string clavePista = pista + "\u0001" + variante;
        if (!_pistas.TryGetValue(clavePista, out var ps) || Math.Abs(ps.Largo - largo) > 5)
            _pistas[clavePista] = ps = new PistaEstado(pista, variante, largo);

        int n = Math.Clamp(sm.mNumParticipants, 0, MaxParticipantes);
        int visto = sm.mViewedParticipantIndex;
        for (int i = 0; i < n; i++)
        {
            if (sm.mParticipantInfo[i].mIsActive == 0) { _part[i] = null; continue; }
            bool esVos = i == visto;
            string clase = esVos ? Texto.Leer(sm.mCarClassName) : "";
            if (clase.Length == 0) { var arr = sm.mCarClassNames[i]; clase = Texto.Leer(arr); }
            if (clase.Length == 0) { _part[i] = null; continue; }

            var p = _part[i];
            if (p is null || p.Pista != ps || p.Clase.Clase != clase || p.EsVos != esVos)
                p = _part[i] = NuevoParticipante(ps, clase, esVos, largo);
            ProcesarParticipante(p, i, in sm, t, largo);
        }

        if (double.IsNaN(_ultimoGuardado)) _ultimoGuardado = t;
        if (_pendienteGuardar && t - _ultimoGuardado >= _intervaloGuardado)
        {
            _ultimoGuardado = t;
            try { Guardar(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { UltimoErrorGuardado = e; }
        }
        return ResultadoGrabacion.Ok;
    }

    private ResultadoGrabacion Descartar(ResultadoGrabacion r) { Descartadas++; return r; }

    private static Participante NuevoParticipante(PistaEstado ps, string clase, bool esVos, double largo)
    {
        if (!ps.Clases.TryGetValue(clase, out var ce)) ps.Clases[clase] = ce = new ClaseEstadoInterno(clase);
        var modo = esVos ? ModoDetector.Pedal : ModoDetector.Velocidad;
        return new Participante
        {
            Pista = ps, Clase = ce, EsVos = esVos, Fuente = esVos ? ce.Vos : ce.Ia,
            Fuerte = new DetectorFrenada(largo, modo, ParametrosDetector.Fuerte),
            Suave = new DetectorFrenada(largo, modo, ParametrosDetector.Suave),
            Levanta = esVos ? new DetectorLevantada(largo) : null,
            Buffer = ps.Mapa.NuevoBuffer(),
        };
    }

    private void ProcesarParticipante(Participante p, int i, in SharedMemory sm, double t, double largo)
    {
        double d = sm.mParticipantInfo[i].mCurrentLapDistance;
        double v = p.EsVos ? sm.mSpeed : sm.mSpeeds[i];
        if (!double.IsFinite(d) || !double.IsFinite(v) || d < 0 || d > largo * 1.01) return;

        bool enPits = sm.mPitModes[i] != 0;
        bool invalida = sm.mLapsInvalidated[i] != 0 || (p.EsVos && sm.mLapInvalidated != 0);

        if (p.TieneAnterior)
        {
            double dt = t - p.PrevT;
            double salto = Geometria.DifCirc(d, p.PrevD, largo);
            bool cruzoMeta = p.PrevD - d > largo / 2;
            bool saltoRaro = !cruzoMeta && Math.Abs(salto) > Math.Max(30, v * dt * 2 + 20);
            if (cruzoMeta) FinDeVuelta(p, i, t);
            else if (saltoRaro || dt > 2.0) p.Sucia = true;       // reinicio a boxes, teletransporte o datos perdidos
        }
        p.TieneAnterior = true;
        p.PrevD = d; p.PrevT = t;

        if (enPits) p.Sucia = true;
        if (invalida && t - p.InicioVuelta >= GraciaInvalidaS) p.Sucia = true;

        if (enPits)
        {
            p.Fuerte.Reiniciar(); p.Suave.Reiniciar(); p.Levanta?.Reiniciar();
            return;
        }

        int vuelta = p.Vuelta;
        p.Salida.Clear();
        if (p.EsVos)
        {
            double freno = sm.mBrake, acel = sm.mThrottle;
            p.Fuerte.Actualizar(d, v, freno, t, vuelta, p.Salida);
            p.Suave.Actualizar(d, v, freno, t, vuelta, p.Salida);
            p.Levanta!.Actualizar(d, v, freno, acel, t, vuelta, p.Salida);
        }
        else
        {
            p.Fuerte.Actualizar(d, v, null, t, vuelta, p.Salida);
            p.Suave.Actualizar(d, v, null, t, vuelta, p.Salida);
        }
        foreach (var e in p.Salida) Recibir(p, i, e);

        if (v > 3) p.Pista.Mapa.Agregar(p.Buffer, d, sm.mParticipantInfo[i].mWorldPosition[0], sm.mParticipantInfo[i].mWorldPosition[2]);
    }

    private static void Recibir(Participante p, int i, EventoFrenada e)
    {
        if (e.Vuelta == p.Vuelta) p.Pendientes.Add(e);
        else if (e.Vuelta == p.UltimaBuena)        // evento que cerro despues de cruzar la meta
        {
            var clave = new ClaveVuelta(i, e.Vuelta);
            if (!p.Fuente.Eventos.TryGetValue(clave, out var l)) p.Fuente.Eventos[clave] = l = [];
            l.Add(e);
            p.Clase.Sucia = true;
        }
    }

    private void FinDeVuelta(Participante p, int i, double t)
    {
        if (!p.Sucia)
        {
            var clave = new ClaveVuelta(i, p.Vuelta);
            p.Fuente.Vueltas.Add(clave);
            p.Fuente.Eventos[clave] = [.. p.Pendientes];
            p.UltimaBuena = p.Vuelta;
            p.Clase.Sucia = true;
            _pendienteGuardar = true;
            if (p.Pista.Mapa.Confirmar(p.Buffer)) p.Pista.MapaSucio = true;
        }
        else p.UltimaBuena = -1;
        p.Vuelta++;
        p.Sucia = false;
        p.InicioVuelta = t;
        p.Pendientes.Clear();
        p.Buffer.Limpiar();
    }

    // ------------------------------------------------------------ resultados

    public IReadOnlyList<EstadoClase> Estados()
        => _pistas.Values.SelectMany(ps => ps.Clases.Values.Select(c =>
            new EstadoClase(ps.Pista, ps.Variante, c.Clase, c.Ia.Vueltas.Count, c.Vos.Vueltas.Count,
                c.Ia.Eventos.Values.Sum(l => l.Count), c.Vos.Eventos.Values.Sum(l => l.Count)))).ToList();

    /// <summary>Curvas de una clase tal como se guardarian ahora (fusionadas con lo ya guardado). Null si no hay nada.</summary>
    public DatosClase? ConstruirClase(string pista, string variante, string clase)
    {
        if (!_pistas.TryGetValue(pista + "\u0001" + variante, out var ps) || !ps.Clases.TryGetValue(clase, out var c)) return null;
        var ia = Agrupador.Agrupar(c.Ia.Eventos.Where(kv => c.Ia.Vueltas.Contains(kv.Key)), c.Ia.Vueltas.Count, ps.Largo);
        var vos = Agrupador.Agrupar(c.Vos.Eventos.Where(kv => c.Vos.Vueltas.Contains(kv.Key)), c.Vos.Vueltas.Count, ps.Largo);
        var nuevas = Agrupador.Emparejar(ia, vos, ps.Largo);
        if (nuevas.Count == 0) return null;       // no pisamos un archivo existente con uno vacio
        var previo = _almacen.LeerClase(pista, variante, clase);
        return new DatosClase
        {
            Pista = pista, Variante = variante, Clase = clase, Largo = Math.Round(ps.Largo, 1),
            Curvas = Fusion.Fusionar(nuevas, previo?.Curvas ?? [], ps.Largo),
            Extra = previo?.Extra,
        };
    }

    public MapaPista? ConstruirMapa(string pista, string variante)
        => _pistas.TryGetValue(pista + "\u0001" + variante, out var ps) ? ps.Mapa.Construir(pista, variante) : null;

    /// <summary>Escribe a disco lo que haya cambiado. Puede lanzar IOException.</summary>
    public void Guardar()
    {
        foreach (var ps in _pistas.Values)
        {
            if (ps.MapaSucio)
            {
                var mapa = ps.Mapa.Construir(ps.Pista, ps.Variante);
                var previo = _almacen.LeerMapa(ps.Pista, ps.Variante);
                if (mapa is not null && (previo is null || previo.Vueltas <= mapa.Vueltas))
                {
                    mapa.Carteles = previo?.Carteles;      // lo editado a mano no se pisa
                    mapa.Extra = previo?.Extra;
                    _almacen.GuardarMapa(mapa);
                }
                ps.MapaSucio = false;
            }
            foreach (var c in ps.Clases.Values.Where(c => c.Sucia))
            {
                var datos = ConstruirClase(ps.Pista, ps.Variante, c.Clase);
                if (datos is not null) _almacen.GuardarClase(datos);
                c.Sucia = false;
            }
        }
        _pendienteGuardar = false;
    }
}
