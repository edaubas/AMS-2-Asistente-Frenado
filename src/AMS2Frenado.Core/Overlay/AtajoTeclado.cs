namespace AMS2Frenado.Core.Overlay;

/// <summary>Mismos valores que MOD_* de RegisterHotKey (Win32).</summary>
[Flags]
public enum Modificadores { Ninguno = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }

/// <summary>Atajo global, por ejemplo "Ctrl+Alt+L". Exige al menos un modificador para no robar teclas sueltas al juego.</summary>
public sealed record AtajoTeclado(Modificadores Modificadores, string Tecla)
{
    public static readonly AtajoTeclado PorDefecto = new(Modificadores.Control | Modificadores.Alt, "L");

    public string Texto
    {
        get
        {
            var partes = new List<string>();
            if (Modificadores.HasFlag(Modificadores.Control)) partes.Add("Ctrl");
            if (Modificadores.HasFlag(Modificadores.Alt)) partes.Add("Alt");
            if (Modificadores.HasFlag(Modificadores.Shift)) partes.Add("Shift");
            if (Modificadores.HasFlag(Modificadores.Win)) partes.Add("Win");
            partes.Add(Tecla);
            return string.Join("+", partes);
        }
    }

    /// <summary>Codigo de tecla virtual de Windows (A-Z, 0-9, F1-F12).</summary>
    public int CodigoVirtual => CodigoDe(Tecla);

    public static int CodigoDe(string tecla)
    {
        if (tecla.Length == 1)
        {
            char c = char.ToUpperInvariant(tecla[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') return c;
        }
        if (tecla.Length is 2 or 3 && (tecla[0] is 'F' or 'f') && int.TryParse(tecla.AsSpan(1), out int f) && f is >= 1 and <= 12)
            return 0x70 + f - 1;
        return 0;
    }

    /// <summary>Null si el texto no es un atajo valido (sin modificador, tecla desconocida o modificador repetido).</summary>
    public static AtajoTeclado? Parsear(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var mod = Modificadores.Ninguno;
        string? tecla = null;
        foreach (var raw in texto.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Modificadores m = raw.ToLowerInvariant() switch
            {
                "ctrl" or "control" => Modificadores.Control,
                "alt" => Modificadores.Alt,
                "shift" or "mayus" => Modificadores.Shift,
                "win" or "windows" => Modificadores.Win,
                _ => Modificadores.Ninguno,
            };
            if (m != Modificadores.Ninguno)
            {
                if (mod.HasFlag(m)) return null;
                mod |= m;
            }
            else
            {
                if (tecla is not null) return null;
                tecla = raw.ToUpperInvariant();
            }
        }
        if (tecla is null || mod == Modificadores.Ninguno || CodigoDe(tecla) == 0) return null;
        return new AtajoTeclado(mod, tecla);
    }
}
