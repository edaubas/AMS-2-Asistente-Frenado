using System.Runtime.InteropServices;

namespace AMS2Frenado.App.Interop;

/// <summary>
/// Llamadas Win32 del overlay: estilos extendidos (clics que atraviesan, sin foco, fuera de Alt-Tab), siempre arriba
/// y atajo global. Compila en Linux pero SIN EJECUTAR: solo se puede probar en Windows con AMS2.
/// </summary>
internal static class Win32
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TRANSPARENT = 0x20;
    public const long WS_EX_TOOLWINDOW = 0x80;
    public const long WS_EX_LAYERED = 0x80000;
    public const long WS_EX_NOACTIVATE = 0x08000000;

    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Bloqueada: LAYERED | TRANSPARENT | NOACTIVATE | TOOLWINDOW (los clics atraviesan hasta el juego).
    /// Desbloqueada: sin TRANSPARENT (se puede arrastrar); se mantiene NOACTIVATE para no sacarle el foco a AMS2.
    /// </summary>
    public static void AplicarEstiloOverlay(IntPtr hwnd, bool atravesable)
    {
        long ex = GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64();
        ex |= WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        if (atravesable) ex |= WS_EX_TRANSPARENT; else ex &= ~WS_EX_TRANSPARENT;
        SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>Vuelve a poner la ventana por encima de las demas sin activarla.</summary>
    public static void ReafirmarArriba(IntPtr hwnd)
        => SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
}
