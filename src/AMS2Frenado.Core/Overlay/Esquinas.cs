namespace AMS2Frenado.Core.Overlay;

public enum Esquina { ArribaDerecha, ArribaIzquierda, AbajoDerecha, AbajoIzquierda }

/// <summary>Area de trabajo de un monitor, en DIP.</summary>
public readonly record struct AreaPantalla(double X, double Y, double Ancho, double Alto)
{
    public double Derecha => X + Ancho;
    public double Abajo => Y + Alto;
}

public static class Esquinas
{
    /// <summary>Esquina del area con el margen dado (por defecto 24 / 24; arriba a la derecha).</summary>
    public static (double X, double Y) Posicionar(Esquina e, AreaPantalla a, double ancho, double alto, double margen = MedidasUi.Margen)
    {
        double x = e is Esquina.ArribaDerecha or Esquina.AbajoDerecha ? a.Derecha - margen - ancho : a.X + margen;
        double y = e is Esquina.AbajoDerecha or Esquina.AbajoIzquierda ? a.Abajo - margen - alto : a.Y + margen;
        return (x, y);
    }

    /// <summary>Mantiene la ventana dentro del area (si es mas grande que el area, queda en la esquina superior izquierda).</summary>
    public static (double X, double Y) AjustarDentro((double X, double Y) p, AreaPantalla a, double ancho, double alto)
    {
        double x = Math.Max(a.X, Math.Min(p.X, a.Derecha - ancho));
        double y = Math.Max(a.Y, Math.Min(p.Y, a.Abajo - alto));
        return (x, y);
    }

    /// <summary>Verdadero si al menos una parte de la ventana, de al menos 40 px, cae dentro del area.</summary>
    public static bool Visible((double X, double Y) p, AreaPantalla a, double ancho, double alto)
    {
        double w = Math.Min(p.X + ancho, a.Derecha) - Math.Max(p.X, a.X);
        double h = Math.Min(p.Y + alto, a.Abajo) - Math.Max(p.Y, a.Y);
        return w >= 40 && h >= 40;
    }

    public static string Nombre(Esquina e) => e switch
    {
        Esquina.ArribaDerecha => "Arriba a la derecha",
        Esquina.ArribaIzquierda => "Arriba a la izquierda",
        Esquina.AbajoDerecha => "Abajo a la derecha",
        _ => "Abajo a la izquierda",
    };
}
