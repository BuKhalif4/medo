using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>
/// Small always-on-top, click-through panel in a screen corner showing FPS / CPU / GPU / temps.
/// Works over games in Borderless / Windowed mode (exclusive fullscreen games draw over every window).
/// </summary>
public class OverlayWindow : Window
{
    readonly Border panel;
    readonly WrapPanel items = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly Dictionary<string, TextBlock> values = new();
    readonly Dictionary<string, FrameworkElement> cells = new();
    string fgName = ""; int fgPid;

    public OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        FlowDirection = FlowDirection.LeftToRight;
        FontFamily = new FontFamily("Segoe UI");
        Title = "KHA Overlay";

        panel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x0A, 0x0A, 0x14)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x8B, 0x5C, 0xF6)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6),
            Child = items, Margin = new Thickness(8),
        };
        Content = panel;

        Add("fps", "FPS", "#FF22D3EE", true);
        Add("ft", "ms", "#FF9A9AB8");
        Add("cpu", "CPU", "#FFA78BFA");
        Add("ctemp", "CPU°", "#FFA78BFA");
        Add("gpu", "GPU", "#FF4ADE80");
        Add("gtemp", "GPU°", "#FF4ADE80");
        Add("vram", "VRAM", "#FF4ADE80");
        Add("ram", "RAM", "#FFF472B6");
        Add("clock", "", "#FF9A9AB8");

        timer.Tick += (_, _) => Tick();
        IsVisibleChanged += (_, _) => { if (IsVisible) timer.Start(); else timer.Stop(); };
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
        };
        SizeChanged += (_, _) => Place();
    }

    void Add(string key, string label, string color, bool big = false)
    {
        var v = new TextBlock { Text = "—", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = big ? 17 : 14, VerticalAlignment = VerticalAlignment.Center };
        var l = new TextBlock { Text = label, Foreground = new SolidColorBrush(Ui.C(color)), FontWeight = FontWeights.SemiBold, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 2, 0, 0) };
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 1, 8, 1) };
        if (key == "ft" || key == "clock") sp.Children.Add(v); else { sp.Children.Add(l); l.Margin = new Thickness(0, 2, 5, 0); sp.Children.Add(v); }
        if (key == "ft") sp.Children.Add(l);
        values[key] = v; cells[key] = sp;
        items.Children.Add(sp);
    }

    public void ApplySettings()
    {
        var s = Settings.Current;
        cells["fps"].Visibility = s.ShowFps ? Visibility.Visible : Visibility.Collapsed;
        cells["ft"].Visibility = s.ShowFrameTime ? Visibility.Visible : Visibility.Collapsed;
        cells["cpu"].Visibility = s.ShowCpu ? Visibility.Visible : Visibility.Collapsed;
        cells["ctemp"].Visibility = s.ShowCpuTemp ? Visibility.Visible : Visibility.Collapsed;
        cells["gpu"].Visibility = s.ShowGpu ? Visibility.Visible : Visibility.Collapsed;
        cells["gtemp"].Visibility = s.ShowGpuTemp ? Visibility.Visible : Visibility.Collapsed;
        cells["vram"].Visibility = s.ShowVram ? Visibility.Visible : Visibility.Collapsed;
        cells["ram"].Visibility = s.ShowRam ? Visibility.Visible : Visibility.Collapsed;
        cells["clock"].Visibility = s.ShowClock ? Visibility.Visible : Visibility.Collapsed;
        items.Orientation = s.OverlayHorizontal ? Orientation.Horizontal : Orientation.Vertical;
        items.MaxWidth = s.OverlayHorizontal ? 900 : 200;
        panel.Opacity = s.OverlayOpacity;
        panel.LayoutTransform = new ScaleTransform(s.OverlayScale, s.OverlayScale);
        Place();
    }

    void Place()
    {
        var wa = SystemParameters.WorkArea;
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0) return;
        // use full screen (not work area) on the top so it sits above games
        double sw = SystemParameters.PrimaryScreenWidth, sh = SystemParameters.PrimaryScreenHeight;
        switch (Settings.Current.OverlayCorner)
        {
            case 0: Left = 0; Top = 0; break;
            case 1: Left = sw - w; Top = 0; break;
            case 2: Left = 0; Top = wa.Bottom - h; break;
            default: Left = sw - w; Top = wa.Bottom - h; break;
        }
    }

    static Brush Col(double v, double warn, double bad) => new SolidColorBrush(Ui.Heat(v, warn, bad));

    void Tick()
    {
        var hw = HardwareMonitor.Instance.Last;
        var (pid, fps, ft) = FpsMonitor.Instance.Foreground();
        if (pid != fgPid) { fgPid = pid; try { fgName = Process.GetProcessById(pid).ProcessName; } catch { fgName = ""; } }
        bool self = pid == Environment.ProcessId;
        values["fps"].Text = fps > 0 && !self ? $"{fps:0}" : "—";
        values["fps"].Foreground = fps > 0 ? (fps < 30 ? Brushes.OrangeRed : fps < 60 ? Brushes.Gold : Brushes.White) : Brushes.White;
        values["ft"].Text = fps > 0 && ft > 0 && !self ? $"{ft:0.0}" : "—";
        values["cpu"].Text = $"{hw.CpuLoad:0}%";
        values["ctemp"].Text = Ui.TempText(hw.CpuTemp);
        if (!double.IsNaN(hw.CpuTemp)) values["ctemp"].Foreground = Col(hw.CpuTemp, 80, 90);
        values["gpu"].Text = double.IsNaN(hw.GpuLoad) ? "—" : $"{hw.GpuLoad:0}%";
        values["gtemp"].Text = Ui.TempText(hw.GpuTemp);
        if (!double.IsNaN(hw.GpuTemp)) values["gtemp"].Foreground = Col(hw.GpuTemp, 78, 86);
        values["vram"].Text = double.IsNaN(hw.VramUsedMb) ? "—" : $"{hw.VramUsedMb / 1024:0.0}G";
        values["ram"].Text = $"{(hw.RamTotal - hw.RamAvail) / 1073741824.0:0.0}G";
        values["clock"].Text = DateTime.Now.ToString("HH:mm");
        // keep above other topmost windows (games in borderless mode)
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }
}
