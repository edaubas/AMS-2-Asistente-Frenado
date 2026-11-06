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
    public bool Grabando { get; init; }
    /// <summary>Pedal de freno del auto visto, 0..1 (0 sin lectura valida).</summary>
    public double Freno { get; init; }
    /// <summary>Verdadero solo en el tick en que hay que pitar.</summary>
    public bool Pitar { get; init; }
    /// <summary>Aviso fuera de banda (ej. no se pudo guardar). Null si no hay.</summary>
    public string? Aviso { get; init; }
}

/// <summary>
/// Integracion de la fase 5: en cada tick recibe una lectura y devuelve lo que hay que mostrar.
/// Modo Grabar: alimenta al <see cref="Grabador"/> (auto visto = vos, el resto = IA por clase) y ademas muestra la
/// guia con lo ya guardado. Modo Mostrar: carga clase.json y mapa.json de pista + variante + clase y alimenta al
/// <see cref="MotorGuia"/>. Sin datos, sin juego o con mVersion distinta de 14, avisa en el panel.
/// Logica pura: no usa ventanas ni relojes (el tiempo llega por parametro). Sin probar en AMS2 en vivo.
/// </summary>
public sealed class ServicioOverlay
{
    /// <summary>Cada cuanto, en modo Grabar, se mira si el grabador escribio archivos nuevos.</summary>
    public const double PeriodoRecargaS = 10;

    private readonly Almacen _almacen;
    private readonly MotorGuia _motor;
    private readonly GestorPitido _pitido = new();
    private Grabador? _grabador;
    private ModoApp _modo = ModoApp.Mostrar;

    private ContextoJuego? _ctx;
    private DatosCargados _datos = new(null, null, 0);
    private readonly AvisoFrenada _aviso = new();
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
            if (_modo == ModoApp.Grabar) GuardarGrabacion();
            _modo = value;
            _tRecarga = double.NegativeInfinity;      // al volver a mostrar, recarga lo recien grabado
            _ultimaSecuencia = null;
        }
    }

    public ReferenciaGuia ReferenciaPrincipal { get; set; } = ReferenciaGuia.Ia;
    /// <summary>Aviso de frenada (simbolo en ventana aparte): activado por defecto.</summary>
    public bool AvisoActivo { get => _aviso.Habilitado; set => _aviso.Habilitado = value; }
    public double AvisoDuracionS { get => _aviso.DuracionS; set => _aviso.DuracionS = value; }
    /// <summary>"Probar aviso": lo muestra durante la duracion configurada sin estar en pista.</summary>
    public void ProbarAviso(double segundos) => _aviso.Probar(segundos);

    /// <summary>
    /// Opacidad del aviso de frenada en este instante. Se llama en cada tick de la ventana (~60 Hz), con o sin muestra
    /// nueva, para que el fundido de 80 ms avance con el reloj y no con la telemetria (~27 Hz).
    /// </summary>
    public CuadroAviso ActualizarAviso(double segundos)
        => _aviso.Actualizar(_ultima.Lectura == EstadoLectura.Ok ? _ultima.Guia : null, _freno, segundos);

    /// <summary>Fuerza releer los archivos de datos (por ejemplo, despues de importar un respaldo).</summary>
    public void ForzarRecarga() { _tRecarga = double.NegativeInfinity; _ultimaSecuencia = null; }
    public bool Pitido { get => _pitido.Habilitado; set => _pitido.Habilitado = value; }
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
                return _ultima = new VistaOverlay { Lectura = r.Estado, Panel = ConstructorVista.EsperandoAms2() };
            case EstadoLectura.VersionDistinta:
                _ctx = null; _ultimaSecuencia = null; _huboSesion = false; _freno = 0;
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
            return _ultima = new VistaOverlay { Lectura = EstadoLectura.VersionDistinta, Panel = ConstructorVista.VersionNoCompatible(sm.mVersion) };

        // La telemetria efectiva llega a ~27-39 Hz y se lee a ~60 Hz: una muestra repetida no se procesa dos veces.
        if (_ultimaSecuencia == sm.mSequenceNumber && _ultima.Lectura == EstadoLectura.Ok) return _ultima with { Pitar = false };
        _ultimaSecuencia = sm.mSequenceNumber;

        var ctx = ContextoJuego.De(sm);
        float largo = sm.mTrackLength;
        if (sm.mGameState != 2 || ctx is null || !float.IsFinite(largo) || largo < 100)
        {
            // menu, pausa o replay: se conserva lo ultimo; si nunca hubo sesion, se pide entrar a pista
            if (_huboSesion) return _ultima with { Pitar = false };
            return _ultima = new VistaOverlay { Lectura = EstadoLectura.Ok, Panel = ConstructorVista.EntrarAPista() };
        }
        _huboSesion = true;

        if (!ctx.Equals(_ctx)) { _ctx = ctx; Recargar(t); }
        else if (double.IsNegativeInfinity(_tRecarga)) Recargar(t);      // cambio de modo: se relee lo guardado

        if (_modo == ModoApp.Grabar)
        {
            try
            {
                _grabador ??= new Grabador(_almacen);
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
        _motor.Grabando = _modo == ModoApp.Grabar;
        int visto = Math.Clamp(sm.mViewedParticipantIndex, 0, ConstantesShm.MaxParticipantes - 1);
        double d = sm.mParticipantInfo[visto].mCurrentLapDistance;
        double kmh = sm.mSpeed * 3.6;
        bool valida = sm.mPitModes[visto] == 0 && double.IsFinite(d) && double.IsFinite(kmh);
        var pg = _motor.Actualizar(new EntradaGuia(t, d, kmh, largo, valida));

        var vista = ConstructorVista.Construir(pg, new ContextoVista(ctx.Clase, ctx.Pista, ctx.Variante, largo));
        if (_modo == ModoApp.Grabar && vista.Tipo is TipoTarjeta.SinDatos or TipoTarjeta.Lejos)
            vista = Contadores(sm, ctx, visto);

        // mBrake: pedal del auto visto (0..1). Si el valor no sirve se toma como "no frena".
        _freno = float.IsFinite(sm.mBrake) ? Math.Clamp(sm.mBrake, 0f, 1f) : 0;

        return _ultima = new VistaOverlay
        {
            Lectura = EstadoLectura.Ok, Panel = vista, Guia = pg,
            TituloPista = ctx.Variante.Length == 0 ? ctx.Pista : $"{ctx.Pista} · {ctx.Variante}",
            ChipClase = ctx.Clase, Grabando = _modo == ModoApp.Grabar,
            Freno = _freno, Pitar = _pitido.Debe(pg), Aviso = _avisoGrabacion,
        };
    }

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

    private VistaPanel Contadores(in SharedMemory sm, ContextoJuego ctx, int visto)
    {
        var estados = _grabador?.Estados() ?? [];
        var enPista = estados.Where(e => e.Pista == ctx.Pista && e.Variante == ctx.Variante).ToList();
        var mia = enPista.FirstOrDefault(e => e.Clase == ctx.Clase);
        int autos = 0;
        int n = Math.Clamp(sm.mNumParticipants, 0, ConstantesShm.MaxParticipantes);
        for (int i = 0; i < n; i++)
        {
            if (i == visto || sm.mParticipantInfo[i].mIsActive == 0) continue;
            var arr = sm.mCarClassNames[i];
            if (Texto.Leer(arr) == ctx.Clase) autos++;
        }
        var otras = enPista.Where(e => e.Clase != ctx.Clase).Select(e => e.Clase).ToList();
        string pie = "Parar de grabar: Bandeja › Modo › Mostrar.";
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
