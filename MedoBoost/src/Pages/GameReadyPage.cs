using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

/// <summary>"جهّز للعب": one switch that prepares the PC for a game session and puts everything back afterwards.</summary>
public class GameReadyPage : PageBase
{
    readonly TextBlock statusTitle = Ui.Txt("", 20, "Text", FontWeights.Bold);
    readonly TextBlock statusSub = Ui.Txt("", 12.5, "TextDim", null, TextWrapping.Wrap);
    readonly TextBlock timerText = Ui.GradText("00:00", 34).Ltr();
    readonly Border bigBtn;
    readonly TextBlock bigGlyph = Ui.Icon("", 40);
    readonly TextBlock bigLabel = Ui.Txt("", 13, "Text", FontWeights.SemiBold);
    readonly System.Windows.Shapes.Ellipse glow;
    readonly WrapPanel chosen = new(), running = new();
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    bool loadingApps;

    public GameReadyPage()
    {
        var root = new StackPanel();
        root.Children.Add(Ui.Title("جهّز للعب"));
        root.Children.Add(Ui.Txt("ضغطة وحدة قبل اللعب: يسكّر البرامج اللي اخترتها، يحرر الرام، يشغّل طاقة قصوى، ويوقف الإزعاجات. وأول ما تخلص بترجع كل الإعدادات زي ما كانت.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 18));

        // ---------- big status card ----------
        glow = new System.Windows.Shapes.Ellipse { Width = 150, Height = 150, Fill = Ui.B("AccentGrad"), Opacity = 0, Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 30 } };
        bigBtn = new Border
        {
            Width = 124, Height = 124, CornerRadius = new CornerRadius(62), Background = Ui.B("AccentGrad"), Cursor = System.Windows.Input.Cursors.Hand,
            Child = Ui.VStack(bigGlyph.Also(g => g.HorizontalAlignment = HorizontalAlignment.Center).M(0, 28, 0, 4), bigLabel.Also(l => l.HorizontalAlignment = HorizontalAlignment.Center)),
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1),
        };
        bigBtn.MouseEnter += (_, _) => Scale(1.06);
        bigBtn.MouseLeave += (_, _) => Scale(1);
        bigBtn.MouseLeftButtonUp += async (_, _) => { bigBtn.IsEnabled = false; await GameMode.ToggleAsync(); bigBtn.IsEnabled = true; Update(); };
        var bigHost = new Grid { Width = 150, Height = 150 };
        bigHost.Children.Add(glow); bigHost.Children.Add(bigBtn);
        var pulse = new DoubleAnimation(0.25, 0.7, TimeSpan.FromSeconds(1.4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        glow.Tag = pulse;

        var statusCard = Ui.Card(Ui.Cols(new[] { "Auto", "*", "Auto" },
            bigHost,
            Ui.VStack(statusTitle, statusSub.M(0, 6, 0, 0)).Also(s => s.VerticalAlignment = VerticalAlignment.Center).M(20, 0, 20, 0),
            Ui.VStack(Ui.Txt("مدة وضع اللعب", 11.5, "TextMute"), timerText).Also(s => s.VerticalAlignment = VerticalAlignment.Center)), 22);
        root.Children.Add(statusCard);

        // ---------- steps ----------
        var s = Settings.Current;
        var steps = new StackPanel();
        void Step(string glyph, string title, string desc, bool on, Action<bool> set)
        {
            var sw = Ui.Switch(on, v => { set(v); Settings.Save(); });
            var g = Ui.Cols(new[] { "36", "*", "Auto" },
                Ui.Icon(glyph, 17, "Accent2"),
                Ui.VStack(Ui.Txt(title, 13.5, "Text", FontWeights.SemiBold), Ui.Txt(desc, 12, "TextDim", null, TextWrapping.Wrap).M(0, 2, 0, 0)).M(0, 0, 16, 0), sw);
            g.Margin = new Thickness(0, 8, 0, 8);
            steps.Children.Add(g);
        }
        Step("", "إغلاق البرامج المختارة", "يسكّر البرامج اللي تحددها تحت (ديسكورد، متصفح، OneDrive...).", s.GameReadyClose, v => s.GameReadyClose = v);
        Step("", "تحرير الرام", "يفرّغ الذاكرة غير المستخدمة قبل ما تبدأ.", s.GameReadyRam, v => s.GameReadyRam = v);
        Step("", "طاقة قصوى", "يشغّل خطة الأداء العالي مؤقتاً ويرجع خطتك بعدين.", s.GameReadyPower, v => s.GameReadyPower = v);
        Step("", "إيقاف خدمات الخلفية مؤقتاً", "SysMain والفهرسة والتتبع تتوقف أثناء اللعب وترجع تشتغل بعدها.", s.GameReadyServices, v => s.GameReadyServices = v);
        Step("", "تنظيف كاش DNS", "يمسح كاش الشبكة قبل اللعب الأونلاين.", s.GameReadyDns, v => s.GameReadyDns = v);
        Step("", "إيقاف تحديثات Windows يوم", "ما ينزل تحديث أو يعيد التشغيل وأنت تلعب. ينرجع بعد اللعب.", s.GameReadyPauseUpdates, v => s.GameReadyPauseUpdates = v);
        Step("", "كتم إشعارات Windows", "ما يطلع لك إشعارات أثناء اللعب.", s.GameReadyMute, v => s.GameReadyMute = v);
        root.Children.Add(Ui.Card(Ui.VStack(Ui.Txt("شو يسوي «جهّز للعب»؟", 15, "Text", FontWeights.Bold), Ui.Sep(), steps), 20).M(0, 16, 0, 0));

        // ---------- apps to close ----------
        var refresh = Ui.Btn("تحديث", "", () => _ = LoadRunning(), "BtnGhost");
        refresh.Padding = new Thickness(10, 4, 10, 4);
        var appsCard = Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "*", "Auto" }, Ui.Txt("البرامج اللي تنسكر", 15, "Text", FontWeights.Bold), refresh),
            Ui.Txt("اضغط على برنامج شغّال لإضافته. الضغط على ✕ يشيله من القائمة.", 12, "TextDim").M(0, 4, 0, 10),
            chosen,
            Ui.Txt("البرامج الشغّالة الحين", 12, "TextMute", FontWeights.SemiBold).M(0, 12, 0, 8),
            running), 20);
        root.Children.Add(appsCard.M(0, 16, 0, 0));

        // ---------- auto ----------
        var auto = Ui.Switch(s.GameReadyAuto, v => { s.GameReadyAuto = v; Settings.Save(); });
        root.Children.Add(Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "*", "Auto" },
                Ui.VStack(Ui.Txt("تلقائي: يشتغل لما تفتح لعبة", 15, "Text", FontWeights.Bold),
                          Ui.Txt("أول ما يكتشف لعبة بتعرض فريمات (FPS) تفعّل وضع اللعب لحاله، ولما تسكّرها يرجع كل شي. (المتصفحات ومشغّلات الفيديو ما تُحسب لعبة).", 12, "TextDim", null, TextWrapping.Wrap).M(0, 4, 16, 0)),
                auto),
            Ui.Sep(),
            Ui.Txt("اختصار من لوحة المفاتيح: Ctrl + Shift + G  (شغّال حتى وأنت داخل اللعبة)", 12, "TextMute")), 20).M(0, 16, 0, 10));

        Children.Add(Ui.Scroll(root));
        Ui.Stagger(root, 60);

        tick.Tick += (_, _) => UpdateTimer();
        GameMode.Changed += () => { if (IsVisible) Update(); };
        BuildChosen();
    }

    void Scale(double to)
    {
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(140));
        var st = (ScaleTransform)bigBtn.RenderTransform;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a); st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    public override void OnShow() { Update(); tick.Start(); _ = LoadRunning(); }
    public override void OnHide() => tick.Stop();

    void Update()
    {
        bool on = GameMode.Active;
        var st = GameMode.State;
        statusTitle.Text = on ? "وضع اللعب شغّال 🚀" : "جاهز تبدأ؟";
        statusSub.Text = on
            ? GameMode.Summary(st) + (st.GamePid != 0 ? $"  •  اللعبة: {st.GameName}" : "")
            : "اضغط على الدائرة وبنجهّز الجهاز للعب.";
        bigGlyph.Text = on ? "" : "";
        bigLabel.Text = on ? "إنهاء" : "جهّز";
        bigBtn.Background = on ? Ui.B("Good") : Ui.B("AccentGrad");
        glow.Fill = on ? Ui.B("Good") : Ui.B("AccentGrad");
        if (on) glow.BeginAnimation(UIElement.OpacityProperty, (DoubleAnimation)glow.Tag);
        else { glow.BeginAnimation(UIElement.OpacityProperty, null); glow.Opacity = 0; }
        UpdateTimer();
    }

    void UpdateTimer()
    {
        if (!GameMode.Active) { timerText.Text = "00:00"; return; }
        var t = DateTime.Now - GameMode.State.Started;
        timerText.Text = t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    Border Chip(string text, bool removable, Action click, string glyph = null)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(Ui.Txt(text, 12.5, removable ? "Text" : "TextDim"));
        if (removable) sp.Children.Add(Ui.Txt("  ✕", 12, "Bad"));
        var b = new Border
        {
            Child = sp, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(16), Cursor = System.Windows.Input.Cursors.Hand,
            Background = removable ? Ui.Solid("#33EF4444") : Ui.B("Surface3"), BorderBrush = removable ? Ui.Solid("#66EF4444") : Ui.B("BorderB"), BorderThickness = new Thickness(1),
        };
        b.MouseEnter += (_, _) => b.BorderBrush = Ui.B("Accent");
        b.MouseLeave += (_, _) => b.BorderBrush = removable ? Ui.Solid("#66EF4444") : Ui.B("BorderB");
        b.MouseLeftButtonUp += (_, _) => click();
        return b;
    }

    void BuildChosen()
    {
        chosen.Children.Clear();
        var list = Settings.Current.BoostKillList;
        if (list.Count == 0) chosen.Children.Add(Ui.Txt("ما اخترت أي برنامج بعد.", 12.5, "TextMute"));
        foreach (var name in list.ToList())
            chosen.Children.Add(Chip(name, true, () => { Settings.Current.BoostKillList.Remove(name); Settings.Save(); BuildChosen(); _ = LoadRunning(); }));
    }

    async Task LoadRunning()
    {
        if (loadingApps) return;
        loadingApps = true;
        try
        {
            var groups = await Task.Run(() => ProcessManager.Instance.Snapshot(false));
            var have = new HashSet<string>(Settings.Current.BoostKillList, StringComparer.OrdinalIgnoreCase);
            running.Children.Clear();
            foreach (var g in groups.Where(g => !have.Contains(g.Name) && !g.Name.Contains("KHABoost", StringComparison.OrdinalIgnoreCase) && !g.Name.Contains("MedoBoost", StringComparison.OrdinalIgnoreCase))
                                    .OrderByDescending(g => g.Ram).Take(16))
            {
                var grp = g;
                running.Children.Add(Chip($"{grp.Title}  {Util.Size(grp.Ram)}", false, () =>
                {
                    Settings.Current.BoostKillList.Add(grp.Name); Settings.Save(); BuildChosen(); _ = LoadRunning();
                }));
            }
        }
        catch (Exception ex) { Log.Warn("GameReady apps: " + ex.Message); }
        finally { loadingApps = false; }
    }
}

internal static class Ext
{
    public static T Also<T>(this T x, Action<T> f) { f(x); return x; }
}
