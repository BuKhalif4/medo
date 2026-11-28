using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>Bell button with unread badge, the history panel, and the small toasts that appear next to the bell.</summary>
public static class NotifyUi
{
    public static readonly StackPanel ToastHost = new() { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 50, 24, 24), IsHitTestVisible = true };
    static Grid layer;
    static StackPanel list;
    static Border badge, panel;
    static TextBlock badgeText;
    static UIElement bellHost;
    static int ticketNo = 0;

    static string Color(string kind) => kind switch { "err" => "Bad", "warn" => "Warn", "ok" => "Good", _ => "Accent2" };
    static string Glyph(string kind) => kind switch { "err" => "", "warn" => "", "ok" => "", _ => "" };

    /// <summary>Keeps toasts below the update banner when it is showing.</summary>
    public static void FollowBanner(FrameworkElement banner)
    {
        double top = 50;
        void Adjust() => ToastHost.Margin = new Thickness(24, top + (banner.IsVisible ? banner.ActualHeight + 14 : 0), 24, 24);
        banner.SizeChanged += (_, _) => Adjust();
        banner.IsVisibleChanged += (_, _) => Adjust();
    }

    static bool Inside(DependencyObject d, DependencyObject root)
    {
        while (d != null) { if (ReferenceEquals(d, root)) return true; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
        return false;
    }

    static bool hooked;
    public static void Init()
    {
        if (!hooked && MainWindow.Instance != null)
        {
            hooked = true;
            // a click anywhere outside the box (or on another page/key), Esc, or switching windows closes it
            MainWindow.Instance.PreviewMouseDown += (_, e) =>
            {
                if (layer?.Visibility != Visibility.Visible) return;
                var src = e.OriginalSource as DependencyObject;
                if (!Inside(src, panel) && !Inside(src, bellHost)) Toggle(false);
            };
            MainWindow.Instance.Deactivated += (_, _) => Toggle(false);
        }
        Notify.Added += ShowToast;
        Notify.Changed += () => { RefreshBadge(); if (layer?.Visibility == Visibility.Visible) Rebuild(); };
    }

    // ---------------- bell ----------------
    public static UIElement BuildBell()
    {
        var b = new Button { Style = Ui.S("IconBtn"), Content = "", FontSize = 14, ToolTip = "الإشعارات" };
        b.Click += (_, _) => Toggle();
        badgeText = new TextBlock { Foreground = Brushes.White, FontSize = 9.5, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        badge = new Border
        {
            Background = Ui.B("Pink"), CornerRadius = Ui.Cr(8), MinWidth = 16, Height = 16, Padding = new Thickness(3, 0, 3, 0),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 6, 0),
            Child = badgeText, Visibility = Visibility.Collapsed, IsHitTestVisible = false,
        };
        var g = new Grid { FlowDirection = FlowDirection.LeftToRight };
        g.Children.Add(b); g.Children.Add(badge);
        bellHost = g;
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(g, true);
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(b, true);
        return g;
    }

    static void RefreshBadge()
    {
        if (badge == null) return;
        int n = Notify.Unread;
        badge.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        badgeText.Text = n > 9 ? "9+" : n.ToString();
    }

    // ---------------- history panel ----------------
    public static Grid BuildLayer()
    {
        layer = new Grid { Visibility = Visibility.Collapsed };
        var backdrop = new Border { Background = Brushes.Transparent };
        backdrop.MouseLeftButtonDown += (_, _) => Toggle(false);
        list = new StackPanel();
        var clear = Ui.Btn("مسح الكل", null, () => { Notify.Clear(); }, "BtnGhost");
        clear.Padding = new Thickness(10, 4, 10, 4); clear.FontSize = 11.5;
        var head = Ui.Cols(new[] { "*", "Auto" }, Ui.Txt("الإشعارات", 15, "Text", FontWeights.Bold), clear);
        head.Margin = new Thickness(16, 14, 16, 10);
        var sv = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 420, Padding = new Thickness(8, 0, 8, 10) };
        panel = new Border
        {
            Width = 380, Background = Ui.B("Surface2"), BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(1), CornerRadius = Ui.Cr(16),
            Child = new StackPanel { Children = { head, new Border { Height = 1, Background = Ui.B("BorderB") }, sv } },
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(24, 46, 24, 24),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.55 },
        };
        layer.Children.Add(backdrop); layer.Children.Add(panel);
        return layer;
    }

    public static void Toggle(bool? open = null)
    {
        if (layer == null) return;
        bool show = open ?? layer.Visibility != Visibility.Visible;
        if (show)
        {
            ToastHost.Children.Clear();
            Rebuild();
            layer.Visibility = Visibility.Visible;
            layer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            Notify.MarkAllRead();
        }
        else layer.Visibility = Visibility.Collapsed;
        RefreshBadge();
    }

    static void Rebuild()
    {
        list.Children.Clear();
        if (Notify.Items.Count == 0)
        {
            list.Children.Add(Ui.Txt("ما في إشعارات 🔕", 13, "TextMute").M(0, 28, 0, 28).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
            return;
        }
        foreach (var it in Notify.Items) list.Children.Add(Row(it));
    }

    static UIElement Row(NotifyItem it)
    {
        var c = Color(it.Kind);
        UIElement icon =
            new Border { Width = 30, Height = 30, CornerRadius = Ui.Cr(15), Background = Ui.Solid(ColorHex(c, 0x28)), Child = Ui.Icon(Glyph(it.Kind), 13, c), VerticalAlignment = VerticalAlignment.Top };
        var title = Ui.Txt(it.Title, 13, "Text", FontWeights.SemiBold);
        var text = Ui.Mixed(it.Text, 12, "TextDim"); text.Margin = new Thickness(0, 2, 0, 0);
        var time = Ui.Txt(it.Time.ToString("HH:mm"), 11, "TextMute").Ltr();
        var g = Ui.Cols(new[] { "40", "*", "Auto" }, icon, Ui.VStack(title, text), time);
        return new Border { Child = g, Padding = new Thickness(8, 10, 8, 10), CornerRadius = Ui.Cr(10), Margin = new Thickness(0, 1, 0, 1), Background = it.Read ? Brushes.Transparent : Ui.Solid("#14FFFFFF") };
    }

    static string ColorHex(string brushKey, byte alpha)
    {
        var b = (SolidColorBrush)Ui.B(brushKey);
        return $"#{alpha:X2}{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}";
    }

    // ---------------- toast ----------------
    static void ShowToast(NotifyItem it)
    {
        if (ToastHost == null) return;
        var c = Color(it.Kind);
        var bar = new Border { Width = 4, Background = Ui.B(c), CornerRadius = Ui.Cr(2), Margin = new Thickness(0, 0, 0, 0) };
        var icon = new Border { Width = 28, Height = 28, CornerRadius = Ui.Cr(14), Background = Ui.Solid(ColorHex(c, 0x28)), Child = Ui.Icon(Glyph(it.Kind), 12, c), VerticalAlignment = VerticalAlignment.Top };
        var msg = Ui.Mixed(it.Text, 13, "Text"); msg.Margin = new Thickness(0, 2, 0, 0);
        var body = Ui.VStack(Ui.Mixed(it.Title, 12.5, "TextDim", FontWeights.SemiBold), msg);
        var g = Ui.Cols(new[] { "Auto", "36", "*" }, bar, icon.M(10, 0, 0, 0), body.M(10, 0, 4, 0));
        var b = new Border
        {
            Child = g, Background = Ui.B("Surface2"), BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(1), CornerRadius = Ui.Cr(12),
            Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), MinWidth = 280, MaxWidth = 390, Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 0, Opacity = 0.5 },
        };
        ToastHost.Children.Add(b);
        while (ToastHost.Children.Count > 3) ToastHost.Children.RemoveAt(0);
        Ui.FadeIn(b, 0, -14, 260);

        void Dismiss()
        {
            var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
            a.Completed += (_, _) => ToastHost.Children.Remove(b);
            b.BeginAnimation(UIElement.OpacityProperty, a);
        }
        b.MouseLeftButtonUp += (_, _) => Dismiss();
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(it.Kind is "warn" or "err" ? 7 : 4.5) };
        t.Tick += (_, _) => { t.Stop(); Dismiss(); };
        t.Start();
    }

    static T Also<T>(this T x, Action<T> f) { f(x); return x; }
}
