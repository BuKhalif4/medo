using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>Small helpers to build the UI in code (keeps the project buildable anywhere and easy to read).</summary>
public static class Ui
{
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static void LoadTheme(Application app)
    {
        using var s = typeof(Ui).Assembly.GetManifestResourceStream("Theme.xaml");
        var dict = (ResourceDictionary)XamlReader.Load(s);
        app.Resources.MergedDictionaries.Add(dict);
    }

    public static Brush B(string key) => (Brush)Application.Current.Resources[key];
    public static Style S(string key) => (Style)Application.Current.Resources[key];
    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static SolidColorBrush Solid(string hex) { var b = new SolidColorBrush(C(hex)); b.Freeze(); return b; }

    public static BitmapImage Logo()
    {
        var bi = new BitmapImage();
        using var s = typeof(Ui).Assembly.GetManifestResourceStream("logo.png");
        bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = s; bi.EndInit(); bi.Freeze();
        return bi;
    }

    public static TextBlock Txt(string text, double size = 13, string brush = "Text", FontWeight? weight = null, TextWrapping wrap = TextWrapping.NoWrap)
        => new() { Text = text, FontSize = size, Foreground = B(brush), FontWeight = weight ?? FontWeights.Normal, TextWrapping = wrap, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Numbers/units must render left-to-right inside the Arabic UI ("88%" not "%88").</summary>
    public static T Ltr<T>(this T fe) where T : FrameworkElement { fe.FlowDirection = FlowDirection.LeftToRight; return fe; }

    public static TextBlock Title(string text, double size = 26) => new()
    {
        Text = text, FontSize = size, FontWeight = FontWeights.Bold, Foreground = B("Text"),
    };

    public static TextBlock GradText(string text, double size, FontWeight? weight = null) => new()
    {
        Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Bold, Foreground = B("AccentGradH"), VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Icon(string glyph, double size = 16, string brush = "Text") => new()
    {
        Text = glyph, FontFamily = IconFont, FontSize = size, Foreground = B(brush), VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    public static Border Card(UIElement child, double pad = 20, double radius = 16) => new()
    {
        Background = B("Surface"), BorderBrush = B("BorderB"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius), Padding = new Thickness(pad), Child = child,
    };

    public static Border Badge(string text, string fg, string bgHex)
        => new()
        {
            Background = Solid(bgHex), CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 3), Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = text, FontSize = 11, Foreground = B(fg), FontWeight = FontWeights.SemiBold },
        };

    public static Button Btn(string text, string glyph = null, Action onClick = null, string style = "Btn")
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph != null)
        {
            var ic = new TextBlock { Text = glyph, FontFamily = IconFont, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            sp.Children.Add(ic);
        }
        sp.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var b = new Button { Content = sp, Style = S(style) };
        if (onClick != null) b.Click += (_, _) => onClick();
        return b;
    }

    public static ToggleButton Switch(bool on, Action<bool> changed = null)
    {
        var t = new ToggleButton { Style = S("Switch"), IsChecked = on, VerticalAlignment = VerticalAlignment.Center };
        if (changed != null)
        {
            t.Checked += (_, _) => changed(true);
            t.Unchecked += (_, _) => changed(false);
        }
        return t;
    }

    public static CheckBox Check(object content, bool on, Action<bool> changed = null)
    {
        var c = new CheckBox { Style = S("Chk"), IsChecked = on, Content = content };
        if (changed != null)
        {
            c.Checked += (_, _) => changed(true);
            c.Unchecked += (_, _) => changed(false);
        }
        return c;
    }

    public static StackPanel HStack(double gap = 0, params UIElement[] items)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var i in items)
        {
            if (i is FrameworkElement fe && gap > 0 && sp.Children.Count > 0) fe.Margin = new Thickness(gap, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
            sp.Children.Add(i);
        }
        return sp;
    }

    public static StackPanel VStack(params UIElement[] items)
    {
        var sp = new StackPanel();
        foreach (var i in items) sp.Children.Add(i);
        return sp;
    }

    /// <summary>Grid with star columns, e.g. Cols("*","Auto") and children placed in order.</summary>
    public static Grid Cols(string[] widths, params UIElement[] items)
    {
        var g = new Grid();
        foreach (var w in widths) g.ColumnDefinitions.Add(new ColumnDefinition { Width = ParseLen(w) });
        for (int i = 0; i < items.Length; i++) { if (items[i] == null) continue; Grid.SetColumn(items[i], i); g.Children.Add(items[i]); }
        return g;
    }

    public static GridLength ParseLen(string w)
    {
        if (w == "Auto") return GridLength.Auto;
        if (w.EndsWith("*")) return new GridLength(w.Length == 1 ? 1 : double.Parse(w[..^1]), GridUnitType.Star);
        return new GridLength(double.Parse(w));
    }

    public static T M<T>(this T fe, double l, double t, double r, double b) where T : FrameworkElement { fe.Margin = new Thickness(l, t, r, b); return fe; }
    public static T M<T>(this T fe, double all) where T : FrameworkElement { fe.Margin = new Thickness(all); return fe; }
    public static T Tip<T>(this T fe, string tip) where T : FrameworkElement { fe.ToolTip = tip; return fe; }

    public static Border Sep() => new() { Height = 1, Background = B("BorderB"), Margin = new Thickness(0, 12, 0, 12) };

    public static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = new Thickness(0, 0, 6, 0),
    };

    public static ProgressBar Progress(double value = 0) => new() { Style = S("Prog"), Minimum = 0, Maximum = 100, Value = value };

    // ---------------- Animations ----------------
    static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    public static void FadeIn(UIElement e, int delayMs = 0, double fromY = 14, int durMs = 380)
    {
        e.Opacity = 0;
        var tt = new TranslateTransform(0, fromY);
        e.RenderTransform = tt;
        var begin = TimeSpan.FromMilliseconds(delayMs);
        var d = new Duration(TimeSpan.FromMilliseconds(durMs));
        e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, d) { BeginTime = begin, EasingFunction = Ease });
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 0, d) { BeginTime = begin, EasingFunction = Ease });
    }

    public static void Stagger(Panel p, int stepMs = 45, int startMs = 0)
    {
        int i = 0;
        foreach (UIElement c in p.Children) FadeIn(c, startMs + (i++) * stepMs);
    }

    public static void Pop(UIElement e)
    {
        var st = new ScaleTransform(0.85, 0.85);
        e.RenderTransformOrigin = new Point(0.5, 0.5);
        e.RenderTransform = st;
        var a = new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
    }

    public static void Spin(UIElement e, bool on)
    {
        var rt = e.RenderTransform as RotateTransform;
        if (rt == null) { rt = new RotateTransform(); e.RenderTransform = rt; e.RenderTransformOrigin = new Point(0.5, 0.5); }
        if (on) rt.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });
        else rt.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    /// <summary>Animates a number inside a TextBlock (count-up effect).</summary>
    public static void CountTo(TextBlock tb, double from, double to, Func<double, string> fmt, int ms = 900)
    {
        var start = DateTime.Now;
        EventHandler h = null;
        h = (_, _) =>
        {
            double t = Math.Min(1, (DateTime.Now - start).TotalMilliseconds / ms);
            double e = 1 - Math.Pow(1 - t, 3);
            tb.Text = fmt(from + (to - from) * e);
            if (t >= 1) CompositionTarget.Rendering -= h;
        };
        CompositionTarget.Rendering += h;
    }

    public static Color Heat(double v, double warn = 70, double bad = 85)
        => v >= bad ? C("#FFEF4444") : v >= warn ? C("#FFF59E0B") : C("#FF22C55E");

    public static string TempText(double t) => double.IsNaN(t) ? "—" : $"{t:0}°C";
}
