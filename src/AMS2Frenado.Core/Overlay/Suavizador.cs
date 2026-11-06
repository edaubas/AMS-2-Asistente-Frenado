using AMS2Frenado.Core.Guia;

namespace AMS2Frenado.Core.Overlay;

/// <summary>Lo que la ventana pinta en este instante.</summary>
public sealed record CuadroUi(VistaPanel Panel, double OpacidadTarjeta, double OpacidadFranja);

/// <summary>
/// Reglas anti-parpadeo de la interfaz (UI-SPEC 3.5). No decide estados (eso es el MotorGuia): solo suaviza el dibujo.
///  - Tarjeta de guia: fundido de entrada de 120 ms y de salida de 200 ms.
///  - Curva siguiente ya activa (chicane): el contenido cambia al instante, sin fundido de salida.
///  - Franja (FRENA, TOQUE, LEVANTA): fundido de 80 ms al aparecer; sin pulso ni destello.
///  - Cifras (cuenta regresiva y regla): como maximo 10 veces por segundo. Los cambios de estado, curva o cartel pasan al instante.
/// Las demas tarjetas (Lejos, SIN DATOS, grabando) se muestran sin fundido.
/// </summary>
public sealed class Suavizador
{
    private double? _t;
    private VistaPanel _mostrado = VistaPanel.Nada;
    private double _opTarjeta = 1;
    private double _opFranja = 1;
    private bool _habiaFranja;
    private VistaPanel? _cifras;
    private double _tCifras = double.NegativeInfinity;
    private string _claveCifras = "";

    public CuadroUi Actualizar(VistaPanel v, double segundos)
    {
        double dt = _t is double t0 ? Math.Max(0, segundos - t0) : 0;
        _t = segundos;
        bool esGuia = v.Tipo == TipoTarjeta.Guia;
        bool mostradoEsGuia = _mostrado.Tipo == TipoTarjeta.Guia;

        if (esGuia)
        {
            if (!mostradoEsGuia) { _opTarjeta = 0; _opFranja = 1; _habiaFranja = false; }   // entra: fundido de 120 ms
            _mostrado = RetenerCifras(v, segundos);
            _opTarjeta = Math.Min(1, _opTarjeta + dt / TiemposUi.FundidoEntradaGuiaS);
        }
        else if (mostradoEsGuia && _opTarjeta > 0)
        {
            // sale de la guia: se sigue viendo el ultimo contenido mientras se apaga (200 ms)
            _opTarjeta = Math.Max(0, _opTarjeta - dt / TiemposUi.FundidoSalidaGuiaS);
            if (_opTarjeta <= 0) { _mostrado = v; _opTarjeta = 1; _cifras = null; }
        }
        else
        {
            _mostrado = v; _opTarjeta = 1; _cifras = null;
        }

        // franja: 80 ms al aparecer; cambiar de franja a franja (FRENA -> TOQUE) no se anima
        bool hayFranja = _mostrado.TextoFranja is not null;
        if (hayFranja && !_habiaFranja) _opFranja = 0;
        _habiaFranja = hayFranja;
        _opFranja = hayFranja ? Math.Min(1, _opFranja + dt / TiemposUi.FundidoFranjaS) : 1;

        return new CuadroUi(_mostrado, _opTarjeta, _opFranja);
    }

    private VistaPanel RetenerCifras(VistaPanel v, double t)
    {
        string clave = string.Join("|", v.Estado, v.IndiceCurva, v.CurvaTxt, v.TextoFranja, v.NumeroPrincipal, v.NumeroSecundario,
            v.ColorNumero, v.MostrarRegla, string.Join(",", v.EtiquetasPrincipal.Select(e => e.Texto)), v.EtiquetaSecundaria?.Texto);
        if (_cifras is null || clave != _claveCifras || t - _tCifras >= TiemposUi.PeriodoCifrasS)
        {
            _cifras = v; _claveCifras = clave; _tCifras = t;
            return v;
        }
        // misma tarjeta, menos de 100 ms desde la ultima cifra: se conservan los numeros que cambian todo el tiempo
        return v with { Metros = _cifras.Metros, Marcadores = _cifras.Marcadores, EscalaReglaM = _cifras.EscalaReglaM };
    }
}

/// <summary>Pitido opcional: una sola vez al entrar en FRENA en cada curva. Lo reproduce la App.</summary>
public sealed class GestorPitido
{
    private EstadoPanel _prev = EstadoPanel.SinDatos;
    private int _curvaPitada = -1;

    public bool Habilitado { get; set; }

    /// <summary>Verdadero cuando hay que pitar con este panel.</summary>
    public bool Debe(PanelGuia p)
    {
        bool entra = p.Estado == EstadoPanel.Frena && _prev != EstadoPanel.Frena && p.IndiceCurva != _curvaPitada;
        if (p.Estado == EstadoPanel.Frena) _curvaPitada = p.IndiceCurva;
        else if (p.Estado is EstadoPanel.Lejos or EstadoPanel.SinDatos) _curvaPitada = -1;
        _prev = p.Estado;
        return Habilitado && entra;
    }
}
