using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>
/// KHA Prime shell: a top instrument bar with live readouts, and the pages on a row of physical function keys (F1–F12).
/// No sidebar, no cards-in-a-sidebar layout.
/// </summary>
public partial class MainWindow
{
    static readonly (string id, string label)[] Keys =
    {
        ("home", "الرئيسية"), ("game", "جهّز"), ("fresh", "من الصفر"), ("clean", "تنظيف"), ("apps", "البرامج"), ("tweaks", "تسريع"),
        ("startup", "بدء"), ("fps", "FPS"), ("security", "الحماية"), ("uninstall", "إلغاء تثبيت"), ("tools", "أدوات"), ("log", "حول"),
    };

    readonly Dictionary<string, TextBlock> ticker = new();
    Border gameLamp;
    TextBlock gameText;

    void OnFKey(object sender, KeyEventArgs e)
    {
        var k = e.Key == Key.System ? e.SystemKey : e.Key;
        if (k >= Key.F1 && k <= Key.F12 && Ui.Prime)
        {
            int i = k - Key.F1;
            if (i < Keys.Length) { Navigate(Keys[i].id); e.Handled = true; }
        }
        else if (k == Key.Escape) NotifyUi.Toggle(false);
    }

    UIElement BuildPrime()
    {
        var root = new Grid { Background = Ui.B("Bg") };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ---------------- top bar ----------------
        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var logo = new Image { Source = Ui.Logo(), Width = 24, Height = 24, Margin = new Thickness(18, 0, 12, 0) };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        var word = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, FlowDirection = FlowDirection.LeftToRight };
        word.Children.Add(new TextBlock { Text = "KHA", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Ui.B("Text") });
        word.Children.Add(new Border { Width = 1, Height = 16, Background = Ui.B("TextMute"), Margin = new Thickness(9, 0, 9, 0) });
        word.Children.Add(new TextBlock { Text = "BOOST", FontSize = 17, FontWeight = FontWeights.Light, Foreground = Ui.B("TextDim") });
        word.Children.Add(new TextBlock { Text = Updater.CurrentText, FontSize = 9.5, FontFamily = new FontFamily("Cascadia Mono, Consolas"), Foreground = Ui.B("TextMute"), Margin = new Thickness(10, 7, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Children = { logo, word } };
        WindowChrome.SetIsHitTestVisibleInChrome(brand, false);
        Grid.SetColumn(brand, 0);
        bar.Children.Add(brand);

        // live readouts
        var strip = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FlowDirection = FlowDirection.LeftToRight };
        foreach (var (k, cap) in new[] { ("cpu", "CPU"), ("gpu", "GPU"), ("ram", "RAM"), ("ct", "CPU°"), ("gt", "GPU°") })
        {
            var v = new TextBlock { Text = "—", FontSize = 13, FontFamily = new FontFamily("Cascadia Mono, Consolas"), Foreground = Ui.B("Text"), MinWidth = 38, VerticalAlignment = VerticalAlignment.Center };
            ticker[k] = v;
            strip.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 14, 0),
                Children = { new TextBlock { Text = cap, FontSize = 9.5, FontFamily = new FontFamily("Cascadia Mono, Consolas"), Foreground = Ui.B("TextMute"), Margin = new Thickness(0, 2, 7, 0), VerticalAlignment = VerticalAlignment.Center }, v },
            });
            if (k != "gt") strip.Children.Add(new Border { Width = 1, Height = 14, Background = Ui.B("BorderB") });
        }
        Grid.SetColumn(strip, 1);
        bar.Children.Add(strip);

        // right: game lamp, signals, window buttons
        gameLamp = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Ui.B("TextMute"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        gameText = new TextBlock { Text = "GAME", FontSize = 10.5, FontFamily = new FontFamily("Cascadia Mono, Consolas"), Foreground = Ui.B("TextDim"), VerticalAlignment = VerticalAlignment.Center };
        var gameBtn = new Border
        {
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { gameLamp, gameText }, FlowDirection = FlowDirection.LeftToRight },
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 4, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent,
            BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), ToolTip = "جهّز للعب (Ctrl+Shift+G)",
        };
        gameBtn.MouseEnter += (_, _) => gameBtn.BorderBrush = Ui.B("TextDim");
        gameBtn.MouseLeave += (_, _) => gameBtn.BorderBrush = Ui.B("BorderB");
        gameBtn.MouseLeftButtonUp += async (_, _) => await GameMode.ToggleAsync();
        WindowChrome.SetIsHitTestVisibleInChrome(gameBtn, true);
        void Lamp() { gameLamp.Background = GameMode.Active ? Ui.B("Accent") : Ui.B("TextMute"); gameText.Foreground = GameMode.Active ? Ui.B("Accent") : Ui.B("TextDim"); }
        GameMode.Changed += Lamp; Lamp();

        var minB = new Button { Style = Ui.S("IconBtn"), Content = "" };
        var maxB = new Button { Style = Ui.S("IconBtn"), Content = "" };
        var closeB = new Button { Style = Ui.S("CloseBtn"), Content = "" };
        minB.Click += (_, _) => WindowState = WindowState.Minimized;
        maxB.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        closeB.Click += (_, _) => Close();
        var ctrl = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center };
        ctrl.Children.Add(gameBtn);
        NotifyUi.Init();
        ctrl.Children.Add(NotifyUi.BuildBell());
        foreach (var b in new[] { minB, maxB, closeB }) { WindowChrome.SetIsHitTestVisibleInChrome(b, true); ctrl.Children.Add(b); }
        Grid.SetColumn(ctrl, 2);
        bar.Children.Add(ctrl);

        Grid.SetRow(bar, 0);
        root.Children.Add(bar);
        var rule = new Border { Height = 1, Background = Ui.B("BorderB"), VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        Grid.SetRow(rule, 0);
        root.Children.Add(rule);

        // ---------------- page area ----------------
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        UpdateUi.Banner.Margin = new Thickness(40, 16, 40, 0);
        Grid.SetRow(UpdateUi.Banner, 0);
        body.Children.Add(UpdateUi.Banner);
        contentHost.Margin = new Thickness(40, 16, 40, 10);
        Grid.SetRow(contentHost, 1);
        body.Children.Add(contentHost);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        // ---------------- function keys ----------------
        var keys = new UniformGrid { Columns = Keys.Length, Margin = new Thickness(14, 12, 14, 12) };
        for (int i = 0; i < Keys.Length; i++)
        {
            var (id, label) = Keys[i];
            var rb = new RadioButton { Style = Ui.S("FKey"), Tag = $"F{i + 1}", Content = label, GroupName = "nav" };
            rb.Checked += (_, _) => Navigate(id);
            navButtons[id] = rb;
            keys.Children.Add(rb);
        }
        var keyStrip = new Border { Child = keys, Background = Ui.B("Surface"), BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(0, 1, 0, 0) };
        Grid.SetRow(keyStrip, 2);
        root.Children.Add(keyStrip);

        HardwareMonitor.Instance.Updated += s => Dispatcher.BeginInvoke(() =>
        {
            ticker["cpu"].Text = $"{s.CpuLoad:0}%";
            ticker["gpu"].Text = double.IsNaN(s.GpuLoad) ? "—" : $"{s.GpuLoad:0}%";
            ticker["ram"].Text = $"{s.RamLoad:0}%";
            ticker["ct"].Text = double.IsNaN(s.CpuTemp) ? "—" : $"{s.CpuTemp:0}°";
            ticker["gt"].Text = double.IsNaN(s.GpuTemp) ? "—" : $"{s.GpuTemp:0}°";
            ticker["ct"].Foreground = double.IsNaN(s.CpuTemp) ? Ui.B("Text") : new SolidColorBrush(Ui.Heat(s.CpuTemp, 75, 88));
            ticker["gt"].Foreground = double.IsNaN(s.GpuTemp) ? Ui.B("Text") : new SolidColorBrush(Ui.Heat(s.GpuTemp, 75, 85));
        });

        // overlays: signal roll, tickets, dialogs
        var modal = new Grid { Visibility = Visibility.Collapsed };
        Dialogs.Host = modal;
        var tickets = NotifyUi.ToastHost;
        NotifyUi.FollowBanner(UpdateUi.Banner);
        var layer = NotifyUi.BuildLayer();
        Grid.SetRowSpan(layer, 3); Grid.SetRowSpan(tickets, 3); Grid.SetRowSpan(modal, 3);
        root.Children.Add(layer);
        root.Children.Add(tickets);
        root.Children.Add(modal);
        return root;
    }
}
