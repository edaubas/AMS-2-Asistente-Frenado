using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Overlay;
using Forms = System.Windows.Forms;

namespace AMS2Frenado.App;

/// <summary>
/// Acceso unico al registro de diagnostico (%APPDATA%\AMS2AsistenteFrenado\registro.log). APAGADO por defecto: se enciende con el
/// argumento <c>--diagnostico</c> o desde el menu de ajustes / la bandeja ("Guardar registro de diagnostico"). Ver docs/ANALISIS-OVERLAY-DESAPARECE.md.
/// Todo lo que toca ventanas va con try/catch: el registro nunca debe cambiar el comportamiento de la app. Compila, SIN EJECUTAR.
/// </summary>
internal static class Diag
{
    public const string Argumento = "--diagnostico";

    public static readonly RegistroDiagnostico Registro = new(Path.Combine(Almacen.RutaPorDefecto(), "registro.log"));

    /// <summary>Una linea con el estado de una ventana: visibilidad, siempre arriba, opacidad, posicion, tamano, monitor y HWND.</summary>
    public static string Describir(Window? w)
    {
        if (w is null) return "(sin ventana)";
        try
        {
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            string monitor = "?";
            if (hwnd != IntPtr.Zero) monitor = Forms.Screen.FromHandle(hwnd).DeviceName.TrimStart('\\', '.');
            return $"Visibility={w.Visibility} IsVisible={w.IsVisible} Topmost={w.Topmost} Opacity={w.Opacity:0.##} " +
                   $"pos=({w.Left:0},{w.Top:0}) tam={w.ActualWidth:0}x{w.ActualHeight:0} monitor={monitor} hwnd=0x{hwnd.ToInt64():X}";
        }
        catch (Exception e) { return "(no se pudo leer: " + e.GetType().Name + ")"; }
    }

    /// <summary>Anota el estado de una ventana solo si cambio desde la ultima vez.</summary>
    public static void Ventana(string nombre, Window? w)
    {
        if (!Registro.Activo) return;
        Registro.Cambio("ventana", nombre, Describir(w));
    }

    /// <summary>Anota un evento de ventana (mostrada, oculta, etc.) con su estado.</summary>
    public static void Evento(string nombre, string que, Window? w)
    {
        if (!Registro.Activo) return;
        Registro.Escribir("ventana", $"{nombre} {que}: {Describir(w)}");
    }

    /// <summary>Marca de arranque: version, SO, argumentos y cuanto paso desde que Windows creo el proceso (detecta demoras previas a la app, ej. antivirus o extraccion).</summary>
    public static void Arranque(string[] args, bool primeraInstancia)
    {
        if (!Registro.Activo) return;
        string desdeProceso = "?";
        try { desdeProceso = $"{(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds:0.0} s"; } catch { }
        string version = typeof(Diag).Assembly.GetName().Version?.ToString() ?? "?";
        Registro.Escribir("arranque", $"AMS2Frenado {version} | {Environment.OSVersion} | {(Environment.Is64BitProcess ? "x64" : "x86")} | " +
            $"args=[{string.Join(' ', args)}] | primeraInstancia={primeraInstancia} | desde la creacion del proceso hasta OnStartup: {desdeProceso}");
        try
        {
            var p = Forms.Screen.AllScreens;
            Registro.Escribir("arranque", "monitores: " + string.Join("; ", p.Select(s =>
                $"{s.DeviceName.TrimStart('\\', '.')}{(s.Primary ? "*" : "")} {s.Bounds.Width}x{s.Bounds.Height} @({s.Bounds.X},{s.Bounds.Y})")));
        }
        catch (Exception e) { Registro.Error("monitores", e); }
    }
}
