namespace AMS2Frenado.Core.Guia;

/// <summary>
/// Constantes configurables de la guia. Los valores por defecto son los del handover y estan
/// pendientes de calibrar en pista (sin probar en AMS2).
/// </summary>
public sealed record OpcionesGuia
{
    /// <summary>Segundos de aviso a la velocidad actual (handover: 5 s).</summary>
    public double SegundosAviso { get; init; } = 5;
    /// <summary>Piso de la ventana por tiempo, en metros.</summary>
    public double VentanaMinM { get; init; } = 120;
    /// <summary>Tope de la ventana por tiempo, en metros.</summary>
    public double VentanaMaxM { get; init; } = 400;
    /// <summary>
    /// Limite Lejos/Aproximacion del diseno aprobado (300 m a 216 km/h). Con la ventana por tiempo
    /// activa el limite real es la ventana (a 216 km/h coincide con 300); este valor se usa solo si
    /// <see cref="UsarVentanaPorTiempo"/> es falso o la velocidad no es valida.
    /// </summary>
    public double UmbralLejosM { get; init; } = 300;
    /// <summary>Limite Aproximacion/Inminente: menos de 100 m hasta el punto de frenado.</summary>
    public double UmbralInminenteM { get; init; } = 100;
    public bool UsarVentanaPorTiempo { get; init; } = true;

    /// <summary>Metros que se restan a la distancia frenar-vertice antes de buscar el cartel (se calibra con una captura del juego).</summary>
    public double DesfaseCartelM { get; init; } = 0;
    /// <summary>Carteles disponibles si la pista no define los suyos en mapa.json.</summary>
    public IReadOnlyList<int> CartelesPorDefecto { get; init; } = Carteles.PorDefecto;

    /// <summary>Margen en metros que hay que superar para retroceder de estado (evita el ruido de +-metros).</summary>
    public double HisteresisM { get; init; } = 20;
    /// <summary>Tiempo que debe sostenerse la evidencia de retroceso antes de bajar de estado.</summary>
    public double TiempoMinRetrocesoS { get; init; } = 1.0;
    /// <summary>Margen alrededor del vertice para no cambiar de curva por ruido en la posicion.</summary>
    public double MargenVerticeM { get; init; } = 5;
}
