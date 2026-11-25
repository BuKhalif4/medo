using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using MedoBoost.Core;
using MedoBoost.Pages;

namespace MedoBoost.UI;

public abstract class PageBase : Grid
{
    public virtual void OnShow() { }
    public virtual void OnHide() { }
}

public partial class MainWindow : Window
{
    readonly Grid contentHost = new();
    readonly Dictionary<string, PageBase> pages = new();
    readonly Dictionary<string, Func<PageBase>> factories = new();
    readonly Dictionary<string, RadioButton> navButtons = new();
    PageBase current;
    public static MainWindow Instance { get; private set; }
    public static bool SkipUpdateCheck;
    /// <summary>True when the app is really exiting (tray "خروج", update install, logoff); otherwise ✕ only hides to the tray.</summary>
    public static bool Quitting;
    public OverlayWindow Overlay { get; private set; }
    TextBlock miniCpu, miniRam;
    ProgressBar miniCpuBar, miniRamBar;

    public MainWindow()
    {
        Instance = this;
        Title = "KHA Boost";
        Width = 1280; Height = 800; MinWidth = Ui.Prime ? 1120 : 1020; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FlowDirection = L.Flow;
        FontFamily = new FontFamily(Ui.Prime ? "Bahnschrift, Segoe UI" : "Segoe UI");
        Background = Ui.B("Bg");
        Foreground = Ui.B("Text");
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        try { Icon = Ui.Logo(); } catch { }
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = Ui.Prime ? 52 : 44, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0),
            CornerRadius = Ui.Cr(0), UseAeroCaptionButtons = false,
        });

        factories["home"] = () => new DashboardPage();
        factories["game"] = () => new GameReadyPage();
        factories["fresh"] = () => new FreshPage();
        factories["clean"] = () => new CleanerPage();
        factories["apps"] = () => new ProcessesPage();
        factories["tweaks"] = () => new TweaksPage();
        factories["security"] = () => new SecurityPage();
        factories["uninstall"] = () => new UninstallPage();
        factories["startup"] = () => new StartupPage();
        factories["fps"] = () => new FpsPage();
        factories["tools"] = () => new ToolsPage();
        factories["log"] = () => new LogPage();

        Content = Build();
        Loaded += (_, _) =>
        {
            Navigate("home");
            if (!Settings.Current.WelcomeDone && !SkipUpdateCheck) ShowWelcome();
            // FPS page listens for the benchmark hotkey, so create it up-front
            try { pages["fps"] = factories["fps"](); } catch (Exception ex) { Log.Error("FPS page: " + ex.Message); }
            if (Settings.Current.AutoUpdateCheck && !SkipUpdateCheck)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                t.Tick += async (_, _) => { t.Stop(); await UpdateUi.CheckAndPrompt(true); };
                t.Start();
            }
            if (!SkipUpdateCheck)
            {
                var dt = new DispatcherTimer { Interval = TimeSpan.FromSeconds(50) };
                dt.Tick += async (_, _) =>
                {
                    dt.Stop();
                    var disks = await Task.Run(DiskHealth.Read);
                    var bad = disks.Where(d => d.Bad || d.Warn).ToList();
                    if (bad.Count > 0)
                        Notify.Push(bad.Any(d => d.Bad) ? "err" : "warn", "صحة القرص", string.Join(" | ", bad.Select(d => $"{d.Name}: {(d.Bad ? "حالته سيئة" : "يحتاج انتباه")}")) + "\nافتح «أدوات الإصلاح» ← صحة الأقراص، وخذ نسخة احتياطية من ملفاتك.", balloon: true);
                };
                dt.Start();
            }
        };
        SourceInitialized += OnSourceInitialized;
        StateChanged += (_, _) => { if (Content is FrameworkElement fe) fe.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0); };
        Application.Current.SessionEnding += (_, _) => Quitting = true;
        PreviewKeyDown += OnFKey;
        if (L.En)
        {
            var sweep = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            sweep.Tick += (_, _) => L.Sweep(this);
            sweep.Start();
            ContentRendered += (_, _) => L.Sweep(this);
        }
        Closing += (_, e) =>
        {
            if (Quitting || !Settings.Current.MinimizeToTray || !Tray.Active) return;
            e.Cancel = true;
            Hide();
            if (!Settings.Current.TrayHintShown)
            {
                Settings.Current.TrayHintShown = true; Settings.Save();
                Tray.Balloon("KHA Boost شغّال بالخلفية", "اضغط على الأيقونة بجانب الساعة لفتحه. للخروج: كليك يمين ← خروج.");
            }
        };
        Closed += (_, _) => { try { Overlay?.Close(); } catch { } Tray.Remove(); Application.Current.Shutdown(); };

        HardwareMonitor.Instance.Updated += s => Dispatcher.BeginInvoke(() =>
        {
            if (miniCpu == null) return;   // the mini readout only exists in the classic sidebar
            miniCpu.Text = $"{s.CpuLoad:0}%"; miniCpuBar.Value = s.CpuLoad;
            miniRam.Text = $"{s.RamLoad:0}%"; miniRamBar.Value = s.RamLoad;
        });
    }

    UIElement Build()
    {
        if (Ui.Prime) return BuildPrime();
        var root = new Grid();
        root.Children.Add(BackgroundFx());

        var main = new Grid();
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(main);

        // ---------- Sidebar ----------
        var side = new Grid { Background = Ui.Solid("#CC0D0D18") };
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var sideBorder = new Border { BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(0, 0, 1, 0), Child = side };
        main.Children.Add(sideBorder);

        var logo = new Image { Source = Ui.Logo(), Width = 42, Height = 42 };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        var brand = Ui.VStack(Ui.GradText("KHA Boost", 21), Ui.Txt("PC Optimizer", 11, "TextMute"));
        brand.Margin = new Thickness(12, 0, 0, 0);
        var head = Ui.HStack(0, logo, brand);
        head.Margin = new Thickness(20, 22, 16, 26);
        WindowChrome.SetIsHitTestVisibleInChrome(head, false);
        side.Children.Add(head);

        var nav = new StackPanel();
        Grid.SetRow(nav, 1);
        side.Children.Add(nav);
        void Nav(string id, string glyph, string text)
        {
            var rb = new RadioButton { Style = Ui.S("NavBtn"), Tag = glyph, Content = text, GroupName = "nav" };
            rb.Checked += (_, _) => Navigate(id);
            navButtons[id] = rb;
            nav.Children.Add(rb);
        }
        Nav("home", "", "الرئيسية");
        Nav("game", "\uE768", "جهّز للعب");
        Nav("fresh", "\uE895", "تنظيف من الصفر");
        Nav("clean", "", "التنظيف العميق");
        Nav("apps", "", "البرامج والرامات");
        Nav("tweaks", "", "تسريع وتويكات");
        Nav("startup", "", "بدء التشغيل");
        Nav("fps", "", "FPS والأوفرلاي");
        Nav("security", "", "الحماية والتحديثات");
        Nav("uninstall", "", "إلغاء التثبيت");
        Nav("tools", "", "أدوات الإصلاح");
        Nav("log", "", "السجل وحول");

        // mini monitor at the bottom of the sidebar
        miniCpu = Ui.Txt("0%", 12, "Text", FontWeights.SemiBold).Ltr(); miniRam = Ui.Txt("0%", 12, "Text", FontWeights.SemiBold).Ltr();
        miniCpuBar = Ui.Progress(); miniRamBar = Ui.Progress();
        miniCpuBar.Height = 5; miniRamBar.Height = 5;
        var mini = Ui.VStack(
            Ui.Cols(new[] { "*", "Auto" }, Ui.Txt("المعالج", 12, "TextDim"), miniCpu), miniCpuBar.M(0, 5, 0, 10),
            Ui.Cols(new[] { "*", "Auto" }, Ui.Txt("الرام", 12, "TextDim"), miniRam), miniRamBar.M(0, 5, 0, 0));
        var miniCard = Ui.Card(mini, 14, 12);
        miniCard.Margin = new Thickness(14, 0, 14, 10);
        var ver = Ui.Txt($"{Updater.CurrentText}  •  صنع خصيصاً لـ MEDO", 10.5, "TextMute");
        ver.HorizontalAlignment = HorizontalAlignment.Center; ver.Margin = new Thickness(0, 0, 0, 14);
        var bottom = Ui.VStack(miniCard, ver);
        Grid.SetRow(bottom, 2);
        side.Children.Add(bottom);

        // ---------- Content ----------
        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(right, 1);
        main.Children.Add(right);

        // caption buttons (always in the top-left in RTL = top-right physical? keep them LTR at physical right)
        var caption = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, FlowDirection = FlowDirection.LeftToRight };
        var minB = new Button { Style = Ui.S("IconBtn"), Content = "" };
        var maxB = new Button { Style = Ui.S("IconBtn"), Content = "" };
        var closeB = new Button { Style = Ui.S("CloseBtn"), Content = "" };
        minB.Click += (_, _) => WindowState = WindowState.Minimized;
        maxB.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        closeB.Click += (_, _) => Close();
        foreach (var b in new[] { closeB, maxB, minB }) { WindowChrome.SetIsHitTestVisibleInChrome(b, true); caption.Children.Add(b); }
        NotifyUi.Init();
        caption.Children.Add(NotifyUi.BuildBell());
        var capHost = new Grid { FlowDirection = FlowDirection.LeftToRight };
        capHost.Children.Add(caption);
        right.Children.Add(capHost);

        Grid.SetRow(UpdateUi.Banner, 1);
        right.Children.Add(UpdateUi.Banner);

        contentHost.Margin = new Thickness(34, 0, 34, 26);
        Grid.SetRow(contentHost, 2);
        right.Children.Add(contentHost);

        // modal + toasts
        var modal = new Grid { Visibility = Visibility.Collapsed };
        Dialogs.Host = modal;
        var toasts = NotifyUi.ToastHost;
        NotifyUi.FollowBanner(UpdateUi.Banner);
        var notifLayer = NotifyUi.BuildLayer();
        Grid.SetColumnSpan(modal, 2); Grid.SetColumnSpan(toasts, 2); Grid.SetColumnSpan(notifLayer, 2);
        main.Children.Add(notifLayer);
        main.Children.Add(toasts);
        root.Children.Add(modal);
        return root;
    }

    /// <summary>Slowly moving glowing blobs behind everything.</summary>
    static UIElement BackgroundFx()
    {
        var canvas = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        void Blob(string color, double size, double x, double y, double dx, double dy, double secs)
        {
            var c = Ui.C(color);
            var e = new System.Windows.Shapes.Ellipse
            {
                Width = size, Height = size,
                Fill = new RadialGradientBrush(Color.FromArgb(0x38, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B)),
            };
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y);
            var tt = new TranslateTransform();
            e.RenderTransform = tt;
            canvas.Children.Add(e);
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, dx, TimeSpan.FromSeconds(secs)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dy, TimeSpan.FromSeconds(secs * 1.3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });
        }
        Blob("#FF8B5CF6", 700, 500, -250, -160, 120, 14);
        Blob("#FF06B6D4", 620, -150, 350, 180, -100, 17);
        Blob("#FFEC4899", 480, 900, 450, -120, -140, 20);
        return canvas;
    }

    public void Navigate(string id)
    {
        if (!factories.ContainsKey(id)) return;
        if (navButtons.TryGetValue(id, out var rb) && rb.IsChecked != true) { rb.IsChecked = true; return; }
        if (!pages.TryGetValue(id, out var page))
        {
            try { page = factories[id](); }
            catch (Exception ex) { Log.Error($"Page {id}: {ex}"); Dialogs.Toast("خطأ بفتح الصفحة: " + ex.Message, "err"); return; }
            pages[id] = page;
        }
        if (current == page) return;
        current?.OnHide();
        contentHost.Children.Clear();
        contentHost.Children.Add(page);
        current = page;
        if (Ui.Prime) Ui.ScanReveal(page, contentHost); else Ui.FadeIn(page, 0, 18, 420);
        try { page.OnShow(); } catch (Exception ex) { Log.Error($"OnShow {id}: {ex.Message}"); }
    }

    public IEnumerable<string> PageIds => factories.Keys;

    /// <summary>The boot-up intro (first run, or "replay" from Settings).</summary>
    public void ShowWelcome()
    {
        if (Content is Grid g && !g.Children.OfType<WelcomeOverlay>().Any())
        {
            var o = new WelcomeOverlay();
            Grid.SetRowSpan(o, Math.Max(1, g.RowDefinitions.Count));
            Grid.SetColumnSpan(o, Math.Max(1, g.ColumnDefinitions.Count));
            g.Children.Add(o);
        }
    }

    // ---------- Overlay + global hotkeys ----------
    public void SetOverlay(bool on)
    {
        Settings.Current.OverlayEnabled = on;
        Settings.Save();
        if (on)
        {
            if (Overlay == null) { Overlay = new OverlayWindow(); Overlay.Closed += (_, _) => Overlay = null; }
            Overlay.ApplySettings();
            Overlay.Show();
        }
        else Overlay?.Hide();
        OverlayChanged?.Invoke(on);
    }
    public event Action<bool> OverlayChanged;
    public event Action BenchmarkHotkey;

    const int HK_OVERLAY = 1, HK_BENCH = 2, HK_BOOST = 3;

    void OnSourceInitialized(object sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        try
        {
            int v = 1; Native.DwmSetWindowAttribute(hwnd, 20, ref v, 4);    // dark mode
            int r = 2; Native.DwmSetWindowAttribute(hwnd, 33, ref r, 4);    // rounded corners (Win11)
        }
        catch { }
        const uint MOD_CTRL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;
        Native.RegisterHotKey(hwnd, HK_OVERLAY, MOD_CTRL | MOD_SHIFT | MOD_NOREPEAT, 0x46);  // Ctrl+Shift+F
        Native.RegisterHotKey(hwnd, HK_BENCH, MOD_CTRL | MOD_SHIFT | MOD_NOREPEAT, 0x42);    // Ctrl+Shift+B
        Native.RegisterHotKey(hwnd, HK_BOOST, MOD_CTRL | MOD_SHIFT | MOD_NOREPEAT, 0x47);    // Ctrl+Shift+G
        var hsrc = HwndSource.FromHwnd(hwnd);
        hsrc?.AddHook(WndProc);
        if (!SkipUpdateCheck) Tray.Init(hsrc);
        if (Settings.Current.OverlayEnabled) Dispatcher.BeginInvoke(() => SetOverlay(true), DispatcherPriority.Background);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312) // WM_HOTKEY
        {
            int id = wParam.ToInt32();
            if (id == HK_OVERLAY) SetOverlay(!(Overlay?.IsVisible ?? false));
            else if (id == HK_BENCH) BenchmarkHotkey?.Invoke();
            else if (id == HK_BOOST) _ = GameMode.ToggleAsync();
            handled = true;
        }
        return IntPtr.Zero;
    }
}
