using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace RADYJKO;

public static class DwmHelper
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyWindowTheme(System.Windows.Window window, bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            int darkMode = isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref darkMode, 4);
            int backdropMica = 2;
            DwmSetWindowAttribute(hwnd, 38, ref backdropMica, 4);
        }
        catch { }
    }
}
