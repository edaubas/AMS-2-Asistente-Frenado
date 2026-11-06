using System.Text;

namespace AMS2Frenado.Core;

public static class Texto
{
    /// <summary>bytes de un char[] de C -> string (corta en el primer NUL).</summary>
    public static string Leer(ReadOnlySpan<byte> bytes)
    {
        int n = bytes.IndexOf((byte)0);
        if (n < 0) n = bytes.Length;
        return Encoding.UTF8.GetString(bytes[..n]).Trim();
    }

    public static string Leer(in Arr64<byte> arr) => Leer((ReadOnlySpan<byte>)arr);
}
