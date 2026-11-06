namespace AMS2Frenado.Core.Overlay;

/// <summary>
/// Arrastre manual de una ventana por su cabecera (ventana de ajustes). Logica pura, en DIP de pantalla: quien llama pasa la
/// posicion del cursor (GetCursorPos, no la relativa a la ventana: asi mover la ventana no realimenta el calculo) y aplica el
/// resultado con SetWindowPos. La ventana sale siempre dentro del area de trabajo del monitor indicado.
/// </summary>
public sealed class ArrastreVentana
{
    private double _dx, _dy;
    private (double X, double Y) _inicio;

    public bool Activo { get; private set; }
    /// <summary>Ultima posicion calculada (la que se aplico a la ventana).</summary>
    public (double X, double Y) Posicion { get; private set; }

    /// <summary>Empieza el arrastre: guarda el desfase entre el cursor y la esquina de la ventana.</summary>
    public void Iniciar(double cursorX, double cursorY, double ventanaX, double ventanaY)
    {
        _dx = ventanaX - cursorX; _dy = ventanaY - cursorY;
        Posicion = _inicio = (ventanaX, ventanaY);
        Activo = true;
    }

    /// <summary>
    /// Nueva posicion para el cursor dado, limitada al <paramref name="area"/> (el monitor bajo el cursor) para que la
    /// ventana no salga de la pantalla. Sin arrastre activo devuelve la ultima posicion.
    /// </summary>
    public (double X, double Y) Mover(double cursorX, double cursorY, AreaPantalla area, double ancho, double alto)
    {
        if (!Activo || !double.IsFinite(cursorX) || !double.IsFinite(cursorY)) return Posicion;
        return Posicion = Esquinas.AjustarDentro((cursorX + _dx, cursorY + _dy), area, ancho, alto);
    }

    /// <summary>Verdadero si la ventana se movio de verdad desde <see cref="Iniciar"/> (un simple clic en la cabecera no cuenta).</summary>
    public bool Desplazado => Math.Abs(Posicion.X - _inicio.X) >= 1 || Math.Abs(Posicion.Y - _inicio.Y) >= 1;

    public void Terminar() => Activo = false;
}

/// <summary>
/// Posicion de la ventana de ajustes: la elegida por el usuario (arrastrandola) manda sobre la automatica (junto a la barra de
/// botones) y se recuerda durante la sesion y, si <see cref="ConfigApp.AjustesPosicion"/> la guarda, entre sesiones. Mientras haya
/// una elegida, el recalculo automatico no la pisa. Logica pura.
/// </summary>
public sealed class PosicionAjustes
{
    public bool Elegida { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>Recuerda la posicion donde el usuario soltó la ventana.</summary>
    public void Elegir(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        Elegida = true; X = x; Y = y;
    }

    public void Olvidar() => Elegida = false;

    /// <summary>
    /// Donde poner la ventana al abrirla (o al corregirla con su alto real): la elegida, llevada dentro del monitor donde cae
    /// (<paramref name="areaEn"/> recibe el centro de la ventana), o, si no hay, la automatica junto a la barra.
    /// </summary>
    public (double X, double Y) Resolver(AreaPantalla barra, AreaPantalla areaBarra, Func<double, double, AreaPantalla> areaEn, double ancho, double alto)
    {
        if (!Elegida) return Esquinas.PosicionMenu(barra, areaBarra, ancho, alto);
        var area = areaEn(X + ancho / 2, Y + alto / 2);
        return Esquinas.AjustarDentro((X, Y), area, ancho, alto);
    }
}
