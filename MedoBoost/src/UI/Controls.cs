using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MedoBoost.UI;

/// <summary>Animated circular gauge (CPU / GPU / RAM ...).</summary>
public class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(RingGauge),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public string Label = "";
    public string ValueText = "0%";
    public string SubText = "";
    public double Thickness = 11;
    readonly Brush arcBrush;
    static readonly Pen BgPen;
    static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    static readonly Typeface Reg = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    static RingGauge()
    {
        BgPen = new Pen(new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x3A)), 11) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        BgPen.Freeze();
    }

    public RingGauge(string c1 = "#FFEC4899", string c2 = "#FF8B5CF6", string c3 = "#FF06B6D4")
    {
        FlowDirection = FlowDirection.LeftToRight;
        var g = new LinearGradientBrush { StartPoint = new Point(0, 1), EndPoint = new Point(1, 0) };
        g.GradientStops.Add(new GradientStop(Ui.C(c1), 0));
        g.GradientStops.Add(new GradientStop(Ui.C(c2), 0.5));
        g.GradientStops.Add(new GradientStop(Ui.C(c3), 1));
        g.Freeze();
        arcBrush = g;
        Width = 150; Height = 150;
    }

    public void Set(double v, string text, string sub = null)
    {
        v = double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 100);
        ValueText = text;
        if (sub != null) SubText = sub;
        BeginAnimation(ValueProperty, new DoubleAnimation(v, TimeSpan.FromMilliseconds(Ui.Prime ? 900 : 650)) { EasingFunction = Ui.Prime ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } : new CubicEase { EasingMode = EasingMode.EaseOut } });
        InvalidateVisual();
    }

    static Geometry Arc(Point c, double r, double startDeg, double sweepDeg)
    {
        if (sweepDeg <= 0.01) sweepDeg = 0.01;
        double a0 = startDeg * Math.PI / 180, a1 = (startDeg + sweepDeg) * Math.PI / 180;
        var p0 = new Point(c.X + r * Math.Cos(a0), c.Y + r * Math.Sin(a0));
        var p1 = new Point(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1));
        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(p0, false, false);
            ctx.ArcTo(p1, new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, true, false);
        }
        sg.Freeze();
        return sg;
    }

    static readonly Typeface DialNum = new(new FontFamily("Bahnschrift, Segoe UI"), FontStyles.Normal, FontWeights.Light, FontStretches.Normal);
    static readonly Typeface DialLbl = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Pen TickDim = Mk("#FF34343A", 1.2), TickBone = Mk("#FFECE8E1", 1.8), TickBoneMajor = Mk("#FFECE8E1", 2.4), TickHead = Mk("#FFFF5B2E", 3.2);
    static Pen Mk(string hex, double t) { var p = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), t) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }; p.Freeze(); return p; }

    /// <summary>Prime: an instrument dial. 54 ticks over 270°; ticks light up to the value and the leading tick is ember.</summary>
    void RenderDial(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, size = Math.Min(w, h);
        var c = new Point(w / 2, h / 2);
        double R = size / 2 - 6;
        const int n = 54;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)(n - 1);
            double ang = (135 + 270 * t) * Math.PI / 180;
            bool major = i % 9 == 0 || i == n - 1;
            double len = major ? 16 : 9;
            var dir = new Vector(Math.Cos(ang), Math.Sin(ang));
            bool lit = t * 100 <= Value + 0.01;
            bool head = lit && (i == n - 1 || (i + 1) / (double)(n - 1) * 100 > Value);
            if (head) len = 20;
            var pen = !lit ? TickDim : head ? TickHead : major ? TickBoneMajor : TickBone;
            dc.DrawLine(pen, c + dir * (R - len), c + dir * R);
        }
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var bone = new SolidColorBrush(Color.FromRgb(0xEC, 0xE8, 0xE1));
        var vt = new FormattedText(ValueText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, DialNum, size * 0.27, bone, dpi);
        dc.DrawText(vt, new Point(c.X - vt.Width / 2, c.Y - vt.Height / 2 - size * 0.03));
        var lt = new FormattedText(Label.ToUpperInvariant(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, DialLbl, size * 0.07, new SolidColorBrush(Color.FromRgb(0x8A, 0x86, 0x7E)), dpi);
        dc.DrawText(lt, new Point(c.X - lt.Width / 2, c.Y + vt.Height / 2 - size * 0.02));
        if (!string.IsNullOrEmpty(SubText))
        {
            var st = new FormattedText(SubText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, DialLbl, size * 0.075, new SolidColorBrush(Color.FromRgb(0xFF, 0x5B, 0x2E)), dpi);
            dc.DrawText(st, new Point(c.X - st.Width / 2, c.Y + R * 0.74));
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Ui.Prime) { RenderDial(dc); return; }
        double w = ActualWidth, h = ActualHeight;
        double size = Math.Min(w, h);
        var c = new Point(w / 2, h / 2);
        double r = size / 2 - Thickness - 4;
        dc.DrawGeometry(null, BgPen, Arc(c, r, 135, 270));
        double sweep = 270 * Value / 100;
        var glow = new Pen(arcBrush, Thickness + 8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.PushOpacity(0.18);
        dc.DrawGeometry(null, glow, Arc(c, r, 135, sweep));
        dc.Pop();
        var pen = new Pen(arcBrush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, pen, Arc(c, r, 135, sweep));

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var white = Brushes.White;
        var dim = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xB8));
        var vt = new FormattedText(ValueText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Bold, size * 0.2, white, dpi);
        dc.DrawText(vt, new Point(c.X - vt.Width / 2, c.Y - vt.Height / 2 - size * 0.04));
        var lt = new FormattedText(Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Reg, size * 0.085, dim, dpi);
        dc.DrawText(lt, new Point(c.X - lt.Width / 2, c.Y + vt.Height / 2 - size * 0.04));
        if (!string.IsNullOrEmpty(SubText))
        {
            var st = new FormattedText(SubText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Bold, size * 0.085, new SolidColorBrush(Color.FromRgb(0x22, 0xD3, 0xEE)), dpi);
            dc.DrawText(st, new Point(c.X - st.Width / 2, c.Y + r * 0.78));
        }
    }
}

/// <summary>Live line chart for the last N seconds.</summary>
public class Sparkline : FrameworkElement
{
    readonly List<double>[] series;
    readonly Color[] colors;
    public int Capacity = 60;
    public double FixedMax = 100;   // <=0 → auto scale
    public bool Fill = !Ui.Prime;

    public Sparkline(params string[] hexColors)
    {
        FlowDirection = FlowDirection.LeftToRight;
        colors = hexColors.Select((h, i) => Ui.C(Ui.Prime ? Ui.Series(i) : h)).ToArray();
        series = colors.Select(_ => new List<double>()).ToArray();
        Height = 120;
        ClipToBounds = true;
    }

    public void Push(params double[] values)
    {
        for (int i = 0; i < series.Length && i < values.Length; i++)
        {
            var v = double.IsNaN(values[i]) ? 0 : values[i];
            series[i].Add(v);
            if (series[i].Count > Capacity) series[i].RemoveAt(0);
        }
        InvalidateVisual();
    }

    public double Max => FixedMax > 0 ? FixedMax : Math.Max(1, series.SelectMany(s => s).DefaultIfEmpty(1).Max() * 1.15);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)), 1);
        for (int i = 1; i < 4; i++) dc.DrawLine(gridPen, new Point(0, h * i / 4), new Point(w, h * i / 4));
        double max = Max;
        for (int si = series.Length - 1; si >= 0; si--)
        {
            var s = series[si];
            if (s.Count < 2) continue;
            double step = w / (Capacity - 1);
            double x0 = w - (s.Count - 1) * step;
            var pts = s.Select((v, i) => new Point(x0 + i * step, h - Math.Min(1, v / max) * (h - 4) - 2)).ToList();
            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                ctx.BeginFigure(pts[0], false, false);
                ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
            }
            line.Freeze();
            if (Fill)
            {
                var area = new StreamGeometry();
                using (var ctx = area.Open())
                {
                    ctx.BeginFigure(new Point(pts[0].X, h), true, true);
                    ctx.PolyLineTo(pts.Append(new Point(pts[^1].X, h)).ToList(), false, true);
                }
                area.Freeze();
                var fill = new LinearGradientBrush(Color.FromArgb(0x55, colors[si].R, colors[si].G, colors[si].B), Color.FromArgb(0x00, colors[si].R, colors[si].G, colors[si].B), 90);
                dc.DrawGeometry(fill, null, area);
            }
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(colors[si]), Ui.Prime ? 1.6 : 2) { LineJoin = PenLineJoin.Round }, line);
            if (Ui.Prime) dc.DrawEllipse(new SolidColorBrush(colors[si]), null, pts[^1], 3, 3);
        }
    }
}

/// <summary>Modal dialogs and toast notifications drawn inside the main window.</summary>
public static class Dialogs
{
    public static Grid Host;      // modal layer

    public static Task<bool> Confirm(string title, string message, string ok = "تأكيد", bool danger = false, string cancel = "إلغاء")
    {
        var tcs = new TaskCompletionSource<bool>();
        if (Host == null) { tcs.SetResult(true); return tcs.Task; }
        var dim = new Border { Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x05, 0x05, 0x0C)) };
        var okBtn = Ui.Btn(ok, null, null, danger ? "BtnDanger" : "Btn");
        var noBtn = Ui.Btn(cancel, null, null, "BtnGhost");
        var buttons = Ui.HStack(10, okBtn, noBtn);
        buttons.HorizontalAlignment = HorizontalAlignment.Left;
        var content = Ui.VStack(
            Ui.HStack(10, Ui.Icon(danger ? "" : "", 20, danger ? "Warn" : "Accent2"), Ui.Txt(title, 18, "Text", FontWeights.Bold)),
            Ui.Txt(message, 13.5, "TextDim", null, TextWrapping.Wrap).M(0, 14, 0, 20),
            buttons);
        var card = Ui.Card(content, 26, 18);
        card.MaxWidth = 520; card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Center;
        card.Background = Ui.B("Surface2");
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 40, ShadowDepth = 0, Opacity = Ui.Prime ? 0.8 : 0.6, Color = Ui.Prime ? Colors.Black : ((SolidColorBrush)Ui.B("Accent")).Color };
        var layer = new Grid();
        layer.Children.Add(dim); layer.Children.Add(card);
        Host.Children.Add(layer);
        Host.Visibility = Visibility.Visible;
        layer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        Ui.Pop(card);
        void Close(bool r)
        {
            Host.Children.Remove(layer);
            if (Host.Children.Count == 0) Host.Visibility = Visibility.Collapsed;
            tcs.TrySetResult(r);
        }
        okBtn.Click += (_, _) => Close(true);
        noBtn.Click += (_, _) => Close(false);
        dim.MouseLeftButtonDown += (_, _) => Close(false);
        return tcs.Task;
    }

    /// <summary>Kept for the many existing call sites: now goes through the notification center (toast + bell history).</summary>
    public static void Toast(string text, string kind = "ok")
    {
        string title = kind switch { "err" => "خطأ", "warn" => "تنبيه", "info" => "معلومة", _ => "تم" };
        bool record = !(kind == "info" && text.StartsWith("جاري"));
        Core.Notify.Push(kind, title, text, record);
    }
}
