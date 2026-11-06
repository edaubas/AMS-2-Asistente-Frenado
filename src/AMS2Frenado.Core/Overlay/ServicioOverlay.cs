using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

/// <summary>Todo lo que la ventana dibuja en un tick (sin tipos de WPF).</summary>
public sealed record VistaOverlay
{
    public EstadoLectura Lectura { get; init; } = EstadoLectura.SinJuego;
    public VistaPanel Panel { get; init; } = VistaPanel.Nada;
    /// <summary>Estado crudo de la guia (null mientras no hay lectura valida).</summary>
    public PanelGuia? Guia { get; init; }
    /// <summary>"Pista · variante" para el encabezado.</summary>
    public string TituloPista { get; init; } = "";
    /// <summary>Chip con la clase ("GT3"); vacio si no se conoce.</summary>
    public string ChipClase { get; init; } = "";
    /// <summary>Verdadero en cualquiera de los dos modos de grabacion.</summary>
    public bool Grabando { get; init; }
    /// <summary>Modo "Grabar solo vos": la IA no se graba (chip "GRABANDO · SOLO VOS").</summary>
    public bool SoloVos { get; init; }
    /// <summary>Pedal de freno del auto visto, 0..1 (0 sin lectura valida).</summary>
    public double Freno { get; init; }
    /// <summary>Verdadero solo en el tick en que hay que pitar.</summary>
    public bool Pitar { get; init; }
    /// <summary>Aviso fuera de banda (ej. no se pudo guardar). Null si no hay.</summary>
    public string? Aviso { get; init; }
}

/// <summary>
/// Integracion de la fase 5: en cada tick recibe una lectura y devuelve lo que hay que mostrar.
/// Modo Grabar (todo): alimenta al <see cref="Grabador"/> (auto visto = vos, el resto = IA por clase) y ademas muestra la
/// guia con lo ya guardado. Modo Grabar solo vos: igual, pero el grabador procesa unicamente al auto visto y no toca
/// lo de la IA ya guardado. Modo Mostrar: carga clase.json y mapa.json de pista + variante + clase y alimenta al
/// <see cref="MotorGuia"/>. Sin datos, sin juego o con mVersion distinta de 14, avisa en el panel.
/// Logica pura: no usa ventanas ni relojes (el tiempo llega por parametro). Sin probar en AMS2 en vivo.
/// </summary>
public sealed class ServicioOverlay
{
    /// <summary>Cada cuanto, en modo Grabar, se mira si el grabador escribio archivos nuevos.</summary>
    public const double PeriodoRecargaS = 10;

    private readonly Almacen _almacen;
    private readonly MotorGuia _motor;
    private readonly DecisorAviso _decisor = new();
    private Grabador? _grabador;
    private ModoApp _modo = ModoApp.Mostrar;

    private ContextoJuego? _ctx;
    private DatosCargados _datos = new(null, null, 0);
    private readonly AvisoFrenada _aviso = new();
    private bool _pitidoActivo;
    private DecisionAviso _decision = DecisionAviso.Nada;
    private double _freno;
    private double _tRecarga = double.NegativeInfinity;
    private uint? _ultimaSecuencia;
    private bool _huboSesion;
    private VistaOverlay _ultima = new() { Panel = ConstructorVista.EsperandoAms2() };
    private string? _avisoGrabacion;

    public ServicioOverlay(Almacen almacen, OpcionesGuia? opciones = null)
    {
        _almacen = almacen;
        _motor = new MotorGuia(opciones);
    }

    public ModoApp Modo
    {
        get => _modo;
        set
        {
            if (value == _modo) return;
            if (EsGrabacion(_modo)) GuardarGrabacion();
            _modo = value;
            _tRecarga = double.NegativeInfinity;      // al volver a mostrar, recarga lo recien grabado
            _ultimaSecuencia = null;
        }
    }

    private static bool EsGrabacion(ModoApp m) => m is ModoApp.Grabar or ModoApp.GrabarSoloVos;

    /// <summary>Referencia que manda en la guia y, sola, en el pitido y el simbolo.</summary>
    public ReferenciaGuia ReferenciaPrincipal { get; set; } = ReferenciaGuia.Ia;
    /// <summary>Aviso de frenada (simbolo en ventana aparte): activado por defecto.</summary>
    public bool AvisoActivo { get => _aviso.Habilitado; set => _aviso.Habilitado = value; }
    public double AvisoDuracionS
    {
        get => _aviso.DuracionS;
        set { _aviso.DuracionS = value; _decisor.DuracionS = value; }
    }
    /// <summary>Segundos antes del punto de frenado en que el simbolo empieza a aparecer (1 a 3).</summary>
    public double AvisoRampaS { get => _decisor.RampaS; set => _decisor.RampaS = value; }
    /// <summary>
    /// Segundos ANTES del punto de frenado en que suena el pitido y el simbolo llega al 100 % (0 a 2,0; 0 = en el punto).
    /// Es el instante de aviso T_aviso unico de los dos. La UI de configuracion solo tiene que enlazar esta propiedad.
    /// </summary>
    public double AvisoAnticipacionS { get => _decisor.AnticipacionS; set => _decisor.AnticipacionS = value; }
    /// <summary>Compensacion de latencia fija de ventana y sonido (0 a 0,25 s, sin calibrar en pista: 0). No se guarda en config.json.</summary>
    public double CompensacionLatenciaS { get => _decisor.CompensacionLatenciaS; set => _decisor.CompensacionLatenciaS = value; }
    /// <summary>
    /// "Probar aviso": pasada sintetica con la rampa completa (<see cref="AvisoRampaS"/>) que termina en T_aviso, donde pita si el
    /// pitido esta activado; el simbolo sigue pleno hasta <see cref="AvisoDuracionS"/> despues del punto (T_aviso + anticipacion).
    /// Ignora si el aviso esta activado.
    /// </summary>
    public void ProbarAviso(double segundos) => _aviso.Probar(segundos, AvisoRampaS, AvisoAnticipacionS, pitar: _pitidoActivo);
    /// <summary>"Probar pitido": la misma pasada, pero pita siempre (aunque el pitido este apagado) y el simbolo solo si el aviso esta activado.</summary>
    public void ProbarPitido(double segundos) => _aviso.Probar(segundos, AvisoRampaS, AvisoAnticipacionS, pitar: true, simbolo: AvisoActivo);

    /// <summary>
    /// Cuadro del aviso de frenada en este instante. Se llama en cada tick de la ventana (~60 Hz), con o sin muestra
    /// nueva, para que el fundido avance con el reloj y no con la telemetria (~27 Hz). El cuadro trae <see cref="CuadroAviso.Pitar"/>
    /// solo en las pruebas. La decision (rampa, pleno,
    /// "ya frenando") se toma una vez por muestra en <see cref="Paso"/>.
    /// </summary>
    public CuadroAviso ActualizarAviso(double segundos) => _aviso.Actualizar(_decision, segundos);

    /// <summary>Fuerza releer los archivos de datos (por ejemplo, despues de importar un respaldo).</summary>
    public void ForzarRecarga() { _tRecarga = double.NegativeInfinity; _ultimaSecuencia = null; }
    public bool Pitido { get => _pitidoActivo; set => _pitidoActivo = value; }
    public Almacen Almacen => _almacen;
    public VistaOverlay Ultima => _ultima;
    /// <summary>Ultimo error al grabar o guardar (para un aviso en la bandeja); null si no hubo.</summary>
    public string? UltimoError => _avisoGrabacion;

    /// <summary>Escribe a disco lo grabado hasta ahora. Se llama al salir y al dejar el modo Grabar. No lanza.</summary>
    public void GuardarGrabacion()
    {
        try { _grabador?.Guardar(); _avisoGrabacion = null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _avisoGrabacion = "No se pudo guardar: " + e.Message; }
    }

    public VistaOverlay Paso(ResultadoLectura r, double segundos)
    {
        switch (r.Estado)
        {
            case EstadoLectura.SinJuego:
                _ctx = null; _ultimaSecuencia = null; _huboSesion = false; _freno = 0;
                SinAviso();
                return _ultima = new VistaOverlay { Lectura = r.Estado, Panel = ConstructorVista.EsperandoAms2() };
            case EstadoLectura.VersionDistinta:
                _ctx = null; _ultimaSecuencia = null; _huboSesion = false; _freno = 0;
                SinAviso();
                return _ultima = new VistaOverlay { Lectura = r.Estado, Panel = ConstructorVista.VersionNoCompatible(r.Version) };
            case EstadoLectura.Ok:
                return Procesar(r.Datos, segundos);
            default:
                return _ultima with { Pitar = false };       // inestable o fin de captura: se mantiene lo ultimo
        }
    }

    private VistaOverlay Procesar(in SharedMemory sm, double t)
    {
        if (sm.mVersion != ConstantesShm.Version)
        {
            SinAviso();
            return _ultima = new VistaOverlay { Lectura = EstadoLectura.VersionDistinta, Panel = ConstructorVista.VersionNoCompatible(sm.mVersion) };
        }

        // La telemetria efectiva llega a ~27-39 Hz y se lee a ~60 Hz: una muestra repetida no se procesa dos veces.
        if (_ultimaSecuencia == sm.mSequenceNumber && _ultima.Lectura == EstadoLectura.Ok) return _ultima with { Pitar = false };
        _ultimaSecuencia = sm.mSequenceNumber;

        var ctx = ContextoJuego.De(sm);
        float largo = sm.mTrackLength;
        if (sm.mGameState != 2 || ctx is null || !float.IsFinite(largo) || largo < 100)
        {
            // menu, pausa o replay: se conserva lo ultimo; si nunca hubo sesion, se pide entrar a pista
            if (_huboSesion) return _ultima with { Pitar = false };
            SinAviso();
            return _ultima = new VistaOverlay { Lectura = EstadoLectura.Ok, Panel = ConstructorVista.EntrarAPista() };
        }
        _huboSesion = true;

        if (!ctx.Equals(_ctx)) { _ctx = ctx; Recargar(t); }
        else if (double.IsNegativeInfinity(_tRecarga)) Recargar(t);      // cambio de modo: se relee lo guardado

        bool grabando = EsGrabacion(_modo), soloVos = _modo == ModoApp.GrabarSoloVos;
        if (grabando)
        {
            try
            {
                if (_grabador is null || _grabador.SoloVos != soloVos)
                {
                    GuardarGrabacion();            // al pasar de "todo" a "solo vos" (o al reves) se guarda lo juntado y se empieza limpio
                    _grabador = new Grabador(_almacen, soloVos: soloVos);
                }
                _grabador.Procesar(in sm, t);
                _avisoGrabacion = _grabador.UltimoErrorGuardado is { } e ? "No se pudo guardar: " + e.Message : null;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _avisoGrabacion = "Error al grabar: " + e.Message;
            }
            if (t - _tRecarga >= PeriodoRecargaS) Recargar(t, soloSiCambio: true);
        }

        _motor.ReferenciaPrincipal = ReferenciaPrincipal;
        _motor.Grabando = grabando;
        int visto = Math.Clamp(sm.mViewedParticipantIndex, 0, ConstantesShm.MaxParticipantes - 1);
        double d = sm.mParticipantInfo[visto].mCurrentLapDistance;
        double kmh = sm.mSpeed * 3.6;
        bool valida = sm.mPitModes[visto] == 0 && double.IsFinite(d) && double.IsFinite(kmh);
        var pg = _motor.Actualizar(new EntradaGuia(t, d, kmh, largo, valida));

        var vista = ConstructorVista.Construir(pg, new ContextoVista(ctx.Clase, ctx.Pista, ctx.Variante, largo, _datos.MotivoClase));
        if (grabando && vista.Tipo is TipoTarjeta.SinDatos or TipoTarjeta.Lejos)
            vista = Contadores(sm, ctx, visto, soloVos);

        // mBrake: pedal del auto visto (0..1). Si el valor no sirve se toma como "no frena".
        _freno = float.IsFinite(sm.mBrake) ? Math.Clamp(sm.mBrake, 0f, 1f) : 0;

        // una sola decision para el simbolo y el pitido, con la referencia principal elegida
        double vms = double.IsFinite(sm.mSpeed) ? sm.mSpeed : 0;
        _decision = _decisor.Evaluar(pg, ReferenciaPrincipal, vms, _freno, t);

        return _ultima = new VistaOverlay
        {
            Lectura = EstadoLectura.Ok, Panel = vista, Guia = pg,
            TituloPista = ctx.Variante.Length == 0 ? ctx.Pista : $"{ctx.Pista} · {ctx.Variante}",
            ChipClase = ctx.Clase, Grabando = grabando, SoloVos = soloVos,
            Freno = _freno, Pitar = _pitidoActivo && _decision.Pitar, Aviso = _avisoGrabacion,
        };
    }

    /// <summary>Sin lectura valida: se apaga el aviso y se olvida la pasada en curso.</summary>
    private void SinAviso() { _decisor.Reiniciar(); _decision = DecisionAviso.Nada; }

    private void Recargar(double t, bool soloSiCambio = false)
    {
        _tRecarga = t;
        if (_ctx is null) return;
        if (soloSiCambio)
        {
            if (SelectorDatos.Sello(_almacen, _ctx) == _datos.Sello) return;
            // no se cambia el recorrido de la guia en medio de una frenada
            if (_ultima.Guia is { Estado: not (EstadoPanel.Lejos or EstadoPanel.SinDatos) }) { _tRecarga = t - PeriodoRecargaS + 1; return; }
        }
        _datos = SelectorDatos.Cargar(_almacen, _ctx);
        _motor.CargarDatos(_datos.Clase, _datos.Mapa);
    }

    private VistaPanel Contadores(in SharedMemory sm, ContextoJuego ctx, int visto, bool soloVos)
    {
        var estados = _grabador?.Estados() ?? [];
        var enPista = estados.Where(e => e.Pista == ctx.Pista && e.Variante == ctx.Variante).ToList();
        var mia = enPista.FirstOrDefault(e => e.Clase == ctx.Clase);
        if (soloVos)
        {
            // la IA no se graba: solo cuenta lo tuyo, y la IA ya guardada no se toca
            return new VistaPanel
            {
                Tipo = TipoTarjeta.Grabando, Estado = EstadoPanel.SinDatos,
                Contadores = new ContadoresGrabacion(
                    Encabezado: ctx.Clase,
                    Ia: "", IaDetalle: "",
                    Vos: (mia?.VueltasVos ?? 0).ToString(), VosDetalle: "vueltas válidas",
                    Pie: "Solo se graba tu auto: la IA ya guardada no se toca. Parar de grabar: ⇄ › Mostrar.",
                    SoloVos: true),
            };
        }
        int autos = 0;
        int n = Math.Clamp(sm.mNumParticipants, 0, ConstantesShm.MaxParticipantes);
        for (int i = 0; i < n; i++)
        {
            if (i == visto || sm.mParticipantInfo[i].mIsActive == 0) continue;
            var arr = sm.mCarClassNames[i];
            if (Texto.Leer(arr) == ctx.Clase) autos++;
        }
        var otras = enPista.Where(e => e.Clase != ctx.Clase).Select(e => e.Clase).ToList();
        string pie = "Parar de grabar: ⇄ › Mostrar.";
        if (otras.Count > 0) pie = $"Otras clases en pista ({string.Join(", ", otras)}) también se graban. " + pie;
        return new VistaPanel
        {
            Tipo = TipoTarjeta.Grabando, Estado = EstadoPanel.SinDatos,
            Contadores = new ContadoresGrabacion(
                Encabezado: ctx.Clase,
                Ia: (mia?.VueltasIa ?? 0).ToString(), IaDetalle: $"vueltas · {autos} autos",
                Vos: (mia?.VueltasVos ?? 0).ToString(), VosDetalle: "vueltas válidas",
                Pie: pie),
        };
    }
}
