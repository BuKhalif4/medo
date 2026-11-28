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
    readonly Dictionary<string, TextBlock> labels = new();
    readonly Dictionary<string, string> baseColors = new();
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

        string A = Ui.Prime ? "#FFFF5B2E" : "#FF22D3EE", Bn = Ui.Prime ? "#FFECE8E1" : "#FFA78BFA", Gn = Ui.Prime ? "#FF9DB596" : "#FF4ADE80", Rm = Ui.Prime ? "#FFD9D3C5" : "#FFF472B6";
        Add("fps", "FPS", A, true);
        Add("ft", "ms", "#FF9A9AB8");
        Add("cpu", "CPU", Bn);
        Add("ctemp", "CPU°", Bn);
        Add("gpu", "GPU", Gn);
        Add("gtemp", "GPU°", Gn);
        Add("vram", "VRAM", Gn);
        Add("ram", "RAM", Rm);
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
        values[key] = v; cells[key] = sp; labels[key] = l; baseColors[key] = color;
        items.Children.Add(sp);
    }

    public void ApplySettings()
    {
        var s = Settings.Current;
        string style = s.OverlayStyle ?? "full";
        // which cells show: minimal / pro are fixed sets, the others follow the check boxes
        var shown = style switch
        {
            "minimal" => new HashSet<string> { "fps" },
            "pro" => new HashSet<string> { "fps", "ft", "cpu", "gpu", "ctemp", "gtemp" },
            _ => new HashSet<string>(new[]
            {
                s.ShowFps ? "fps" : null, s.ShowFrameTime ? "ft" : null, s.ShowCpu ? "cpu" : null, s.ShowCpuTemp ? "ctemp" : null,
                s.ShowGpu ? "gpu" : null, s.ShowGpuTemp ? "gtemp" : null, s.ShowVram ? "vram" : null, s.ShowRam ? "ram" : null, s.ShowClock ? "clock" : null,
            }.Where(x => x != null)),
        };
        foreach (var kv in cells) kv.Value.Visibility = shown.Contains(kv.Key) ? Visibility.Visible : Visibility.Collapsed;

        // accent colour: theme colour unless the user picked one (custom / neon)
        Color? accent = null;
        if ((style is "custom" or "neon") && !string.IsNullOrEmpty(s.OverlayAccent))
        {
            try { accent = Ui.C(s.OverlayAccent); } catch (FormatException) { accent = null; }
        }
        if (style == "neon" && accent == null) accent = Ui.C("#FF22D3EE");
        foreach (var kv in labels)
            kv.Value.Foreground = new SolidColorBrush(accent != null && kv.Key is "fps" or "cpu" or "gpu" ? accent.Value : Ui.C(baseColors[kv.Key]));

        bool bg = style != "minimal" && (style != "custom" || s.OverlayBackground);
        panel.Background = bg ? new SolidColorBrush(Color.FromArgb(style == "neon" ? (byte)0xB0 : (byte)0xCC, 0x0A, 0x0A, 0x14)) : Brushes.Transparent;
        panel.BorderThickness = new Thickness(bg ? 1 : 0);
        panel.BorderBrush = new SolidColorBrush(accent != null ? Color.FromArgb(style == "neon" ? (byte)0xFF : (byte)0x99, accent.Value.R, accent.Value.G, accent.Value.B) : Color.FromArgb(0x66, 0x8B, 0x5C, 0xF6));
        panel.CornerRadius = new CornerRadius(style == "pro" ? 4 : 10);
        panel.Padding = style == "minimal" ? new Thickness(4, 2, 4, 2) : style == "pro" ? new Thickness(8, 3, 8, 3) : new Thickness(10, 6, 10, 6);
        // glow for neon, a dark outline-shadow when there is no background so the text stays readable over the game
        panel.Effect = style == "neon" && accent != null
            ? new System.Windows.Media.Effects.DropShadowEffect { Color = accent.Value, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.85 }
            : !bg ? new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = 1 }
            : null;
        values["fps"].FontSize = style == "minimal" ? 26 : 17;
        values["fps"].FontFamily = style == "pro" ? new FontFamily("Consolas") : new FontFamily("Segoe UI");

        items.Orientation = s.OverlayHorizontal && style != "minimal" ? Orientation.Horizontal : Orientation.Vertical;
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
