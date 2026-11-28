using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>
/// First-run intro: the logotype is struck letter by letter, a hairline draws across, the panel "boots" by reading your real
/// hardware line by line, then you pick the language and enter. Can be replayed from Settings.
/// </summary>
public class WelcomeOverlay : Grid
{
    readonly FontFamily mono = new("Cascadia Mono, Consolas");
    readonly StackPanel logLines = new() { Margin = new Thickness(0, 26, 0, 0) };
    readonly StackPanel ticks = new() { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
    readonly StackPanel final = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 28, 0, 0) };
    readonly Button skip;
    string pendingLang;
    bool skipped, done;
    CheckBox restore;

    public WelcomeOverlay()
    {
        Background = Ui.B("Bg");
        Panel.SetZIndex(this, 1000);
        pendingLang = Settings.Current.Language;

        var stage = new StackPanel { Width = 560, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FlowDirection = FlowDirection.LeftToRight };
        Children.Add(stage);

        // logotype, struck letter by letter
        var letters = new StackPanel { Orientation = Orientation.Horizontal };
        string[] ls = { "K", "H", "A" };
        for (int i = 0; i < ls.Length; i++)
        {
            var t = new TextBlock { Text = ls[i], FontSize = 96, FontWeight = FontWeights.Light, Foreground = Ui.B("Text"), Margin = new Thickness(0, 0, 14, 0), Opacity = 0, RenderTransform = new TranslateTransform(0, 30) };
            letters.Children.Add(t);
            var d = TimeSpan.FromMilliseconds(250 + i * 170);
            t.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)) { BeginTime = d });
            ((TranslateTransform)t.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(520)) { BeginTime = d, EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 } });
        }
        var boost = new TextBlock { Text = "B O O S T", FontSize = 26, FontWeight = FontWeights.Light, Foreground = Ui.B("TextDim"), Margin = new Thickness(18, 52, 0, 0), Opacity = 0 };
        boost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(900) });
        letters.Children.Add(boost);
        stage.Children.Add(letters);

        // hairline draws across
        var rule = new Border { Height = 2, Width = 0, Background = Ui.B("Accent"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 0) };
        rule.BeginAnimation(WidthProperty, new DoubleAnimation(0, 560, TimeSpan.FromMilliseconds(900)) { BeginTime = TimeSpan.FromMilliseconds(700), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        stage.Children.Add(rule);
        var sub = new TextBlock { Text = "INSTRUMENT PANEL  ·  INITIALIZING", FontFamily = mono, FontSize = 11, Foreground = Ui.B("TextMute"), Margin = new Thickness(0, 12, 0, 0), Opacity = 0 };
        sub.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(1500) });
        stage.Children.Add(sub);

        stage.Children.Add(logLines);
        for (int i = 0; i < 40; i++) ticks.Children.Add(new Border { Width = 9, Height = 14, Margin = new Thickness(0, 0, 4, 0), Background = Ui.B("BorderB") });
        stage.Children.Add(ticks);

        BuildFinal();
        stage.Children.Add(final);

        skip = Ui.Btn("تخطي", null, () => { skipped = true; ShowFinal(); }, "BtnGhost");
        skip.Padding = new Thickness(14, 5, 14, 5); skip.FontSize = 11.5;
        skip.HorizontalAlignment = HorizontalAlignment.Right; skip.VerticalAlignment = VerticalAlignment.Top; skip.Margin = new Thickness(24);
        Children.Add(skip);

        Loaded += async (_, _) => await Run();
    }

    void BuildFinal()
    {
        var title = Ui.Txt(L.T("أهلاً بك"), 22, "Text", FontWeights.Light);
        var lang = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0), FlowDirection = FlowDirection.LeftToRight };
        foreach (var (code, name) in new[] { ("ar", "العربية"), ("en", "English") })
        {
            var rb = new RadioButton { Style = Ui.S("Pill"), Content = new TextBlock { Text = name, FontSize = 14 }, GroupName = "lang", IsChecked = pendingLang == code, MinWidth = 120 };
            string c = code;
            rb.Checked += (_, _) => pendingLang = c;
            lang.Children.Add(rb);
        }
        restore = Ui.Check(Ui.Txt("إنشاء نقطة استعادة للنظام الآن (منصوح)", 12.5, "TextDim"), true);
        restore.Margin = new Thickness(0, 14, 0, 0);
        var enter = Ui.Btn("دخول", "", () => Finish());
        enter.Padding = new Thickness(34, 12, 34, 12); enter.FontSize = 14; enter.HorizontalAlignment = HorizontalAlignment.Left; enter.Margin = new Thickness(0, 22, 0, 0);
        final.Children.Add(title);
        final.Children.Add(Ui.Txt("اللغة / Language", 11.5, "TextMute").M(0, 14, 0, 0));
        final.Children.Add(lang);
        final.Children.Add(restore);
        final.Children.Add(enter);
    }

    async Task Delay(int ms) { if (!skipped) await Task.Delay(ms); }

    async Task Type(string text, bool ok)
    {
        var tb = new TextBlock { FontFamily = mono, FontSize = 12.5, Foreground = Ui.B("TextDim"), Margin = new Thickness(0, 3, 0, 3) };
        logLines.Children.Add(tb);
        foreach (var ch in text) { if (skipped) { tb.Text = text; break; } tb.Text += ch; await Task.Delay(9); }
        if (ok)
        {
            var run = new System.Windows.Documents.Run("   [ OK ]") { Foreground = Ui.B("Accent") };
            tb.Inlines.Add(run);
        }
    }

    async Task Run()
    {
        await Delay(1700);
        SystemSpecs s = null;
        var read = Task.Run(() => { try { return SystemInfo.Read(); } catch { return new SystemSpecs(); } });
        var hw = HardwareMonitor.Instance.Last;
        s = await read;
        string P(string k, string v) => (k + "                ")[..10] + v;
        var lines = new List<string>
        {
            P("PROCESSOR", L.TEn(s.Cpu + "  " + s.CpuSub)), P("MEMORY", L.TEn($"{s.Ram}  {s.RamSub}")), P("GRAPHICS", s.Gpu),
            P("STORAGE", s.Storage), P("SENSORS", double.IsNaN(hw.GpuTemp) && double.IsNaN(hw.CpuTemp) ? "standby" : $"online  {(double.IsNaN(hw.CpuTemp) ? "—" : hw.CpuTemp.ToString("0") + "°")} / {(double.IsNaN(hw.GpuTemp) ? "—" : hw.GpuTemp.ToString("0") + "°")}"),
            P("SYSTEM", s.Os),
        };
        int lit = 0;
        for (int i = 0; i < lines.Count && !skipped; i++)
        {
            await Type(lines[i], true);
            int target = (i + 1) * ticks.Children.Count / lines.Count;
            while (lit < target) { ((Border)ticks.Children[lit]).Background = Ui.B("Text"); lit++; await Delay(18); }
            await Delay(120);
        }
        ShowFinal();
    }

    void ShowFinal()
    {
        if (done || final.Visibility == Visibility.Visible) return;
        for (int i = 0; i < ticks.Children.Count; i++) ((Border)ticks.Children[i]).Background = Ui.B(i == ticks.Children.Count - 1 ? "Accent" : "Text");
        skip.Visibility = Visibility.Collapsed;
        final.Visibility = Visibility.Visible;
        Ui.FadeIn(final, 0, 18, 500);
    }

    void Finish()
    {
        if (done) return;
        done = true;
        var st = Settings.Current;
        st.WelcomeDone = true;
        bool langChanged = pendingLang != st.Language;
        st.Language = pendingLang;
        Settings.Save();
        if (restore.IsChecked == true && !langChanged) _ = Task.Run(() => { var (ok, msg) = Tweaks.CreateRestorePoint(); Notify.Push(ok ? "ok" : "warn", ok ? "نقطة الاستعادة جاهزة" : "نقطة الاستعادة فشلت", ok ? "" : msg); });
        if (langChanged) { Program.Restart(); return; }
        var move = new TranslateTransform();
        RenderTransform = move;
        var a = new DoubleAnimation(0, -Math.Max(ActualHeight, 700), TimeSpan.FromMilliseconds(650)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        a.Completed += (_, _) => (Parent as Panel)?.Children.Remove(this);
        move.BeginAnimation(TranslateTransform.YProperty, a);
    }
}
