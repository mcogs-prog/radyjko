using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace RADYJKO;

public class GlobalHotkeys : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd;
    private HwndSource? _source;
    public Action? OnPlayPause;
    public Action? OnNextStation;
    public Action? OnPrevStation;

    public void Register(System.Windows.Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(HwndHook);
        RegisterHotKey(_hwnd, 9001, 3, 0x20);
        RegisterHotKey(_hwnd, 9002, 3, 0x27);
        RegisterHotKey(_hwnd, 9003, 3, 0x25);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312)
        {
            int id = wParam.ToInt32();
            if (id == 9001) { OnPlayPause?.Invoke(); handled = true; }
            else if (id == 9002) { OnNextStation?.Invoke(); handled = true; }
            else if (id == 9003) { OnPrevStation?.Invoke(); handled = true; }
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero) { UnregisterHotKey(_hwnd, 9001); UnregisterHotKey(_hwnd, 9002); UnregisterHotKey(_hwnd, 9003); }
        _source?.RemoveHook(HwndHook);
    }
}
