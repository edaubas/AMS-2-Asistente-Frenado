using System.Globalization;
using System.Windows.Media;

namespace AMS2Frenado.App;

/// <summary>Pinceles congelados a partir de los "#RRGGBB" del Core (con cache).</summary>
internal static class Pincel
{
    private static readonly Dictionary<(string, double), SolidColorBrush> Cache = [];

    public static SolidColorBrush De(string hex, double alfa = 1)
    {
        if (Cache.TryGetValue((hex, alfa), out var b)) return b;
        var c = (Color)ColorConverter.ConvertFromString(hex);
        c.A = (byte)Math.Round(Math.Clamp(alfa, 0, 1) * 255, MidpointRounding.AwayFromZero);
        b = new SolidColorBrush(c);
        b.Freeze();
        Cache[(hex, alfa)] = b;
        return b;
    }

    public static Color Color(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static string Invariante(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
}
