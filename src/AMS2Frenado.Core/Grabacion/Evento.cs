namespace AMS2Frenado.Core.Grabacion;

/// <summary>Tipo de curva segun la caida de velocidad (handover, "Curvas rapidas").</summary>
public enum TipoCurva
{
    /// <summary>La velocidad cae 30 km/h o mas.</summary>
    Frenada,
    /// <summary>Cae entre 10 y 30 km/h.</summary>
    Toque,
    /// <summary>Cae menos de 10 km/h, o se suelta el acelerador sin frenar.</summary>
    Levanta,
}

public static class Tipos
{
    public const double UmbralFrenadaKmh = 30;
    public const double UmbralToqueKmh = 10;

    public static TipoCurva Clasificar(double caidaMs)
    {
        double kmh = caidaMs * 3.6;
        return kmh >= UmbralFrenadaKmh ? TipoCurva.Frenada
             : kmh >= UmbralToqueKmh ? TipoCurva.Toque
             : TipoCurva.Levanta;
    }

    public static string ATexto(TipoCurva t) => t switch
    {
        TipoCurva.Frenada => "frenada",
        TipoCurva.Toque => "toque",
        _ => "levanta",
    };

    public static TipoCurva DeTexto(string? s) => s switch
    {
        "frenada" => TipoCurva.Frenada,
        "toque" => TipoCurva.Toque,
        _ => TipoCurva.Levanta,
    };
}

/// <summary>
/// Una frenada, toque o levantada detectada. Distancias en metros de distancia de vuelta,
/// velocidades en m/s. <see cref="Prioridad"/>: 3 detector fuerte, 2 suave, 1 levantada por acelerador.
/// </summary>
public readonly record struct EventoFrenada(
    double FrenarEn, double LargoFrenada, double Apice, double V0, double VMin,
    int Vuelta, TipoCurva Tipo, int Prioridad)
{
    public double Caida => V0 - VMin;
}

public static class Geometria
{
    /// <summary>a - b en una pista circular, en [-largo/2, largo/2).</summary>
    public static double DifCirc(double a, double b, double largo)
    {
        double d = Mod(a - b, largo);
        return d >= largo / 2 ? d - largo : d;
    }

    /// <summary>Modulo siempre positivo (como % de Python).</summary>
    public static double Mod(double a, double m)
    {
        double r = a % m;
        return r < 0 ? r + m : r;
    }
}
