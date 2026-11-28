using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MedoBoost.Core;

/// <summary>Notification-area (tray) icon using plain Win32, so no WinForms dependency is needed.</summary>
public static class Tray
{
    const int WM_APP_TRAY = 0x8001;
    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_INFO = 0x10;
    const uint NIIF_INFO = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public int cbSize; public IntPtr hWnd; public uint uID, uFlags, uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA d);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadImage(IntPtr inst, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    static IntPtr hwnd, hicon;
    static uint taskbarCreated;
    public static bool Active { get; private set; }

    public static void Init(HwndSource src)
    {
        try
        {
            hwnd = src.Handle;
            taskbarCreated = RegisterWindowMessage("TaskbarCreated");
            var icoPath = System.IO.Path.Combine(Util.DataDir, "kha.ico");
            using (var s = typeof(Tray).Assembly.GetManifestResourceStream("logo.ico"))
            using (var f = File.Create(icoPath)) s.CopyTo(f);
            hicon = LoadImage(IntPtr.Zero, icoPath, 1, 16, 16, 0x10);
            src.AddHook(WndProc);
            Active = Add();
        }
        catch (Exception ex) { Log.Warn("Tray: " + ex.Message); }
    }

    static NOTIFYICONDATA Data() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = hwnd, uID = 1, hIcon = hicon,
        szTip = "KHA Boost", szInfo = "", szInfoTitle = "",
    };

    static bool Add()
    {
        var d = Data(); d.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP; d.uCallbackMessage = WM_APP_TRAY;
        return Shell_NotifyIcon(NIM_ADD, ref d);
    }

    public static void Remove()
    {
        if (!Active) return;
        var d = Data(); Shell_NotifyIcon(NIM_DELETE, ref d); Active = false;
    }

    public static void Balloon(string title, string text)
    {
        if (!Active) return;
        var d = Data(); d.uFlags = NIF_INFO; d.szInfoTitle = title.Length > 60 ? title[..60] : title;
        d.szInfo = text.Length > 250 ? text[..250] : text; d.dwInfoFlags = NIIF_INFO;
        Shell_NotifyIcon(NIM_MODIFY, ref d);
    }

    public static (double x, double y) CursorDip()
    {
        GetCursorPos(out var p);
        var w = UI.MainWindow.Instance;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(w);
        return (p.X / dpi.DpiScaleX, p.Y / dpi.DpiScaleY);
    }

    static IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_APP_TRAY)
        {
            int ev = (int)(lParam.ToInt64() & 0xFFFF);
            if (ev == 0x202 || ev == 0x203) { UI.MainWindow.Instance.Dispatcher.BeginInvoke(ShowMain); handled = true; }       // left click / double click
            else if (ev == 0x205) { UI.MainWindow.Instance.Dispatcher.BeginInvoke(UI.TrayMenu.ShowAtCursor); handled = true; }    // right click
        }
        else if (taskbarCreated != 0 && msg == taskbarCreated) { Add(); }                                                        // explorer restarted
        return IntPtr.Zero;
    }

    public static void ShowMain()
    {
        var w = UI.MainWindow.Instance;
        w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
        UI.UpdateUi.CheckIfStale();
    }

    public static void Exit()
    {
        UI.MainWindow.Quitting = true;
        Remove();
        Application.Current.Shutdown();
    }
}
