using System.Globalization;
using AMS2Frenado.Core.Grabacion;

namespace AMS2Frenado.Core.Guia;

/// <summary>Una muestra para la guia. Segundos crece; DistanciaVuelta y LargoPista en metros; velocidad en km/h.</summary>
public readonly record struct EntradaGuia(
    double Segundos, double DistanciaVuelta, double VelocidadKmh, double LargoPista = 0, bool Valida = true);

/// <summary>
/// Calcula el estado del panel a partir de las curvas guardadas y la posicion actual. Lógica pura, sin UI.
///
/// Sigue una curva a la vez (la proxima por delante, por vertice). Para esa curva el nivel solo avanza
/// (Lejos -> Aproximacion -> Inminente -> Frena) apenas lo dice la posicion, y solo retrocede si la
/// evidencia supera un margen en metros durante un tiempo minimo: no parpadea por ruido de +-metros ni
/// por los huecos de datos (~27 Hz, hasta ~0,45 s). Pasado el vertice cambia a la curva siguiente.
/// Sin probar en AMS2 en vivo.
/// </summary>
public sealed class MotorGuia
{
    private readonly OpcionesGuia _op;
    private DatosClase? _datos;
    private MapaPista? _mapa;
    private ReferenciaGuia _referencia = ReferenciaGuia.Ia;
    private bool _grabando;

    private double _largo;
    private List<Plan>? _plan;

    // seguimiento
    private int _seg = -1;
    private int _pasada = -1;
    private double? _prevSd;
    private int _nivel;
    private double _ventanaFija;
    private double? _desdeRetroceso;
    private double _metrosMin = double.PositiveInfinity;
    private double? _prevT;

    private PanelGuia _ultimo;

    private sealed record Plan(int Indice, CurvaGuardada Curva, TipoCurva Tipo, double Vertice, double FrenarEn, double Span,
        ReferenciaGuia RefP, BloqueCurva BloqueP, ReferenciaGuia? RefS, BloqueCurva? BloqueS, int? CartelIa, int? CartelVos);

    public MotorGuia(OpcionesGuia? opciones = null)
    {
        _op = opciones ?? new OpcionesGuia();
        _ultimo = new PanelGuia { Estado = EstadoPanel.SinDatos, Visible = true, TextoEstado = "SIN DATOS" };
    }

    public PanelGuia Ultimo => _ultimo;

    /// <summary>Referencia principal (la que manda el numero grande). Si la curva no la tiene se usa la otra.</summary>
    public ReferenciaGuia ReferenciaPrincipal
    {
        get => _referencia;
        set { if (value != _referencia) { _referencia = value; Invalidar(); } }
    }

    /// <summary>Lo fija el modo (fase 5). Solo se refleja en <see cref="PanelGuia.Grabando"/>.</summary>
    public bool Grabando
    {
        get => _grabando;
        set { _grabando = value; _ultimo = _ultimo with { Grabando = value }; }
    }

    /// <summary>Carga las curvas de la clase y el mapa (para su lista opcional de carteles). Null: sin datos.</summary>
    public void CargarDatos(DatosClase? datos, MapaPista? mapa = null)
    {
        _datos = datos; _mapa = mapa;
        Invalidar();
    }

    private void Invalidar()
    {
        _plan = null; _largo = 0;
        ReiniciarSeguimiento();
    }

    private void ReiniciarSeguimiento()
    {
        _seg = -1; _pasada = -1; _prevSd = null; _prevT = null;
        ReiniciarNivel();
    }

    private void ReiniciarNivel()
    {
        _nivel = 0; _ventanaFija = 0; _desdeRetroceso = null; _metrosMin = double.PositiveInfinity;
    }

    /// <summary>Ventana por tiempo: velocidad * segundos de aviso, entre el piso y el tope.</summary>
    public static double Ventana(double velocidadKmh, OpcionesGuia op)
    {
        if (!op.UsarVentanaPorTiempo || double.IsNaN(velocidadKmh) || velocidadKmh < 0) return op.UmbralLejosM;
        return Math.Clamp(velocidadKmh / 3.6 * op.SegundosAviso, op.VentanaMinM, op.VentanaMaxM);
    }

    // ------------------------------------------------------------ plan

    private void ConstruirPlan(double largoEntrada)
    {
        _plan = [];
        _largo = _datos is null ? 0 : (_datos.Largo > 0 ? _datos.Largo : _mapa?.Largo > 0 ? _mapa.Largo : largoEntrada);
        if (_datos is null || _largo <= 0) return;

        var carteles = Carteles.Normalizar(_mapa?.Carteles, _op.CartelesPorDefecto);
        for (int i = 0; i < _datos.Curvas.Count; i++)
        {
            var c = _datos.Curvas[i];
            var tipo = Tipos.DeTexto(c.Tipo);
            BloqueCurva? ia = c.Ia, vos = c.Vos;
            var (rp, bp, rs, bs) = _referencia == ReferenciaGuia.Ia
                ? (ReferenciaGuia.Ia, ia, ReferenciaGuia.Vos, vos)
                : (ReferenciaGuia.Vos, vos, ReferenciaGuia.Ia, ia);
            if (bp is null) { (rp, bp, rs, bs) = (rs, bs, rp, null); }
            if (bp is null) continue;

            double vertice = bp.Vertice > 0 ? bp.Vertice : c.Vertice;
            double span = Geometria.Mod(vertice - bp.FrenarEn, _largo);
            if (span <= 0 || span > _largo / 2) continue;     // dato sin sentido
            _plan.Add(new Plan(i, c, tipo, Geometria.Mod(vertice, _largo), Geometria.Mod(bp.FrenarEn, _largo), span,
                rp, bp, bs is null ? null : rs, bs,
                tipo == TipoCurva.Frenada && ia is not null ? Carteles.Calcular(ia, _largo, _op.DesfaseCartelM, carteles) : null,
                tipo == TipoCurva.Frenada && vos is not null ? Carteles.Calcular(vos, _largo, _op.DesfaseCartelM, carteles) : null));
        }
    }

    // ------------------------------------------------------------ actualizacion

    public PanelGuia Actualizar(in EntradaGuia e)
    {
        if (!e.Valida || double.IsNaN(e.DistanciaVuelta) || double.IsInfinity(e.DistanciaVuelta)
            || double.IsNaN(e.Segundos) || e.DistanciaVuelta < 0)
            return _ultimo;                      // lectura sin sentido: se mantiene lo ultimo

        if (_plan is null) ConstruirPlan(e.LargoPista);
        if (_plan!.Count == 0 || _largo <= 0
            || (e.LargoPista > 0 && Math.Abs(e.LargoPista - _largo) > 0.02 * _largo))
        {
            ReiniciarSeguimiento();
            return _ultimo = new PanelGuia { Estado = EstadoPanel.SinDatos, Visible = true, Grabando = _grabando, TextoEstado = "SIN DATOS" };
        }

        double L = _largo;
        double d = Geometria.Mod(e.DistanciaVuelta, L);
        if (_prevT is double pt && e.Segundos < pt) ReiniciarSeguimiento();     // el tiempo volvio atras: sesion nueva
        _prevT = e.Segundos;

        var ms = new double[_plan.Count];
        for (int i = 0; i < ms.Length; i++) ms[i] = Geometria.Mod(_plan[i].Vertice - d, L);

        // ---- que curva seguimos
        double margen = _op.MargenVerticeM;
        int idx = _seg;
        if (idx >= _plan.Count) { idx = -1; _prevSd = null; }
        if (idx < 0)
        {
            idx = Cercana(ms, -1, -1);
            ReiniciarNivel();
        }
        else
        {
            double mt = ms[idx];
            double sd = Geometria.DifCirc(_plan[idx].Vertice, d, L);          // >0: el vertice esta por delante
            // el vertice quedo atras: pasa de "por delante o en el margen" a "mas de 'margen' atras" avanzando
            bool cruzo = _prevSd is double ps && ps > -margen && sd <= -margen && ps - sd < L / 4;
            if (cruzo)
            {
                _pasada = idx;
                int otra = Cercana(ms, idx, -1);
                if (otra >= 0) idx = otra;
                ReiniciarNivel();
            }
            else if (L - mt >= margen)           // si no, esta en el margen de ruido justo despues del vertice: se mantiene
            {
                int n = Cercana(ms, -1, idx);
                if (n != idx) { idx = n; ReiniciarNivel(); }
            }
        }
        _seg = idx;
        _prevSd = Geometria.DifCirc(_plan[idx].Vertice, d, L);
        if (_pasada == idx && ms[idx] >= L / 4) _pasada = -1;    // ya se alejo: la proxima vez es una vuelta nueva

        // ---- distancias
        var p = _plan[idx];
        double m = ms[idx];
        double adelante = L - m < margen ? 0 : m;           // dentro del margen despues del vertice: en el vertice
        double aVertice = adelante;
        double aFrenar = adelante > p.Span ? adelante - p.Span : 0;
        double ventana = Ventana(e.VelocidadKmh, _op);

        // ---- nivel con histeresis
        // recien pasado el vertice y con ruido de vuelta a "por delante": no se vuelve a avisar esta curva
        bool bloqueada = idx == _pasada && m < L / 4;
        int bruto = bloqueada ? 0 : Nivel(p.Tipo, aFrenar, ventana);
        if (bruto > _nivel)
        {
            _nivel = bruto;
            _ventanaFija = Math.Max(_ventanaFija, ventana);
            _desdeRetroceso = null;
        }
        else
        {
            int ret = Nivel(p.Tipo, aFrenar - _op.HisteresisM, Math.Max(_ventanaFija, ventana));
            if (ret < _nivel)
            {
                _desdeRetroceso ??= e.Segundos;
                if (e.Segundos - _desdeRetroceso >= _op.TiempoMinRetrocesoS)
                {
                    _nivel = ret;
                    _desdeRetroceso = null;
                    _metrosMin = double.PositiveInfinity;
                }
            }
            else _desdeRetroceso = null;
        }
        // el numero de metros no sube por ruido mientras se sigue la misma curva
        _metrosMin = Math.Min(_metrosMin, aFrenar);
        double metros = _metrosMin;

        return _ultimo = Armar(p, idx, _nivel, metros, aVertice, ventana);
    }

    private int Nivel(TipoCurva tipo, double aFrenar, double ventana)
    {
        if (tipo == TipoCurva.Frenada)
        {
            if (aFrenar <= 0) return 3;
            if (aFrenar < _op.UmbralInminenteM) return 2;
        }
        return aFrenar <= ventana ? 1 : 0;
    }

    /// <summary>Curva con menor distancia por delante, sin la excluida y sin la recien pasada si cae dentro del margen (ruido).</summary>
    private int Cercana(double[] ms, int excluir, int actual)
    {
        int mejor = -1;
        for (int i = 0; i < ms.Length; i++)
        {
            if (i == excluir) continue;
            if (i == _pasada && i != actual && ms[i] < _op.MargenVerticeM) continue;
            if (mejor < 0 || ms[i] < ms[mejor]) mejor = i;
        }
        return mejor;
    }

    // ------------------------------------------------------------ salida

    private PanelGuia Armar(Plan p, int idx, int nivel, double aFrenar, double aVertice, double ventana)
    {
        var estado = p.Tipo switch
        {
            TipoCurva.Toque => nivel >= 1 ? EstadoPanel.Toque : EstadoPanel.Lejos,
            TipoCurva.Levanta => nivel >= 1 ? EstadoPanel.Levanta : EstadoPanel.Lejos,
            _ => nivel switch { 0 => EstadoPanel.Lejos, 1 => EstadoPanel.Aproximacion, 2 => EstadoPanel.Inminente, _ => EstadoPanel.Frena },
        };

        int? cartelP = p.RefP == ReferenciaGuia.Ia ? p.CartelIa : p.CartelVos;
        int? cartelS = p.RefS is null ? null : (p.RefS == ReferenciaGuia.Ia ? p.CartelIa : p.CartelVos);
        if (cartelS == cartelP) cartelS = null;            // IA y vos en el mismo cartel: uno solo
        int velP = p.BloqueP.VVerticeKmh;
        int? velS = p.BloqueS?.VVerticeKmh;

        string nombre = p.Curva.Nombre;
        var ci = CultureInfo.InvariantCulture;
        string estadoTxt = estado switch
        {
            EstadoPanel.Frena => "FRENÁ",
            EstadoPanel.Toque => "TOQUE",
            EstadoPanel.Levanta => "LEVANTÁ",
            _ => "",
        };

        string? principal = null, secundario = null, metros = null;
        if (estado == EstadoPanel.Lejos)
            metros = string.Create(ci, $"Próxima {nombre.ToUpperInvariant()} · {Math.Round(aFrenar / 10) * 10:0} m");
        else if (p.Tipo == TipoCurva.Frenada)
        {
            principal = cartelP is null ? null : string.Create(ci, $"{cartelP} m");
            secundario = cartelS is null ? null : string.Create(ci, $"{cartelS} m");
            metros = string.Create(ci, $"{Math.Round(aFrenar):0} m");
        }
        else
        {
            principal = string.Create(ci, $"{velP} km/h");
            secundario = velS is null || velS == velP ? null : string.Create(ci, $"{velS} km/h");
            if (p.Tipo == TipoCurva.Toque) metros = string.Create(ci, $"{Math.Round(aFrenar):0} m");   // Levanta: sin regla de metros
        }

        return new PanelGuia
        {
            Estado = estado,
            Visible = estado != EstadoPanel.Lejos,
            Grabando = _grabando,
            IndiceCurva = p.Indice,
            Curva = p.Curva,
            NombreCurva = nombre,
            Tipo = p.Tipo,
            Principal = p.RefP,
            Secundaria = p.RefS,
            MetrosHastaFrenar = aFrenar,
            MetrosHastaVertice = aVertice,
            VentanaM = ventana,
            CartelIa = p.CartelIa,
            CartelVos = p.CartelVos,
            CartelPrincipal = cartelP,
            CartelSecundario = cartelS,
            VelMinPrincipalKmh = velP,
            VelMinSecundariaKmh = velS,
            TextoEstado = estadoTxt,
            TextoPrincipal = principal,
            TextoSecundario = secundario,
            TextoMetros = metros,
        };
    }
}
