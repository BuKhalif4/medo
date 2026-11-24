using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class FpsPage : PageBase
{
    readonly TextBlock fpsBig = Ui.GradText("—", 64);
    readonly TextBlock appName = Ui.Txt("افتح لعبة لعرض الـ FPS", 14, "TextDim");
    readonly TextBlock frameTime = Ui.Txt("", 13, "TextDim");
    readonly Sparkline fpsChart = new("#FF22D3EE") { FixedMax = 0, Height = 140 };
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly ToggleButton overlaySwitch;
    readonly Button benchBtn;
    readonly TextBlock benchStatus = Ui.Txt("", 13, "Accent2", FontWeights.SemiBold);
    readonly StackPanel results = new();
    readonly Border compare;
    List<BenchResult> history = new();
    static string HistFile => Path.Combine(Util.DataDir, "benchmarks.json");
    DispatcherTimer benchTimer;
    DateTime benchEnd;
    string benchApp;

    public FpsPage()
    {
        var root = new StackPanel();
        root.Children.Add(Ui.Title("FPS والأوفرلاي"));
        root.Children.Add(Ui.Txt("عداد FPS بدون حقن داخل اللعبة (آمن مع الأنتي تشيت) – يستخدم أحداث Windows نفسها اللي يستخدمها PresentMon. قِس الأداء قبل وبعد التنظيف وشوف الفرق بالأرقام.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 18));

        // ---- live
        var live = Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "Auto", "*" },
                Ui.VStack(Ui.Txt("الـ FPS الحالي", 13, "TextDim"), fpsBig, frameTime),
                Ui.VStack(appName.M(0, 0, 0, 8), fpsChart).M(24, 0, 0, 0))));
        if (!string.IsNullOrEmpty(FpsMonitor.Instance.Error)) appName.Text = "تعذر تشغيل عداد FPS: " + FpsMonitor.Instance.Error;

        // ---- overlay settings
        var s = Settings.Current;
        overlaySwitch = Ui.Switch(s.OverlayEnabled, v => { if ((MainWindow.Instance.Overlay?.IsVisible ?? false) != v) MainWindow.Instance.SetOverlay(v); });
        MainWindow.Instance.OverlayChanged += v => { if (overlaySwitch.IsChecked != v) overlaySwitch.IsChecked = v; };

        UIElement Pills(string[] labels, int selected, Action<int> on)
        {
            var wp = new WrapPanel();
            string group = Guid.NewGuid().ToString();
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var rb = new RadioButton { Style = Ui.S("Pill"), Content = labels[i].EndsWith("%") ? new TextBlock { Text = labels[i], FlowDirection = FlowDirection.LeftToRight } : labels[i], GroupName = group, IsChecked = i == selected };
                rb.Checked += (_, _) => on(idx);
                wp.Children.Add(rb);
            }
            return wp;
        }
        void Changed() { Settings.Save(); MainWindow.Instance.Overlay?.ApplySettings(); }

        var metrics = new WrapPanel();
        void Metric(string label, bool v, Action<bool> set)
            => metrics.Children.Add(Ui.Check(Ui.Txt(label, 12.5), v, x => { set(x); Changed(); }).M(0, 0, 18, 10));
        Metric("FPS", s.ShowFps, v => s.ShowFps = v);
        Metric("Frame time", s.ShowFrameTime, v => s.ShowFrameTime = v);
        Metric("CPU %", s.ShowCpu, v => s.ShowCpu = v);
        Metric("حرارة CPU", s.ShowCpuTemp, v => s.ShowCpuTemp = v);
        Metric("GPU %", s.ShowGpu, v => s.ShowGpu = v);
        Metric("حرارة GPU", s.ShowGpuTemp, v => s.ShowGpuTemp = v);
        Metric("VRAM", s.ShowVram, v => s.ShowVram = v);
        Metric("RAM", s.ShowRam, v => s.ShowRam = v);
        Metric("الساعة", s.ShowClock, v => s.ShowClock = v);

        var overlayCard = Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "*", "Auto" }, Ui.VStack(Ui.Txt("أوفرلاي فوق الشاشة", 15, "Text", FontWeights.Bold), Ui.Txt("اختصار Ctrl+Shift+F – يظهر فوق الألعاب بوضع Borderless أو Windowed", 12, "TextDim", null, TextWrapping.Wrap).M(0, 2, 12, 0)), overlaySwitch),
            Ui.Sep(),
            Ui.Txt("المكان", 12.5, "TextDim").M(0, 0, 0, 6),
            Pills(new[] { "فوق يسار", "فوق يمين", "تحت يسار", "تحت يمين" }, s.OverlayCorner, i => { s.OverlayCorner = i; Changed(); }),
            Ui.Txt("الشكل", 12.5, "TextDim").M(0, 8, 0, 6),
            Pills(new[] { "أفقي", "عمودي" }, s.OverlayHorizontal ? 0 : 1, i => { s.OverlayHorizontal = i == 0; Changed(); }),
            Ui.Txt("الحجم", 12.5, "TextDim").M(0, 8, 0, 6),
            Pills(new[] { "صغير", "عادي", "كبير" }, s.OverlayScale < 0.95 ? 0 : s.OverlayScale > 1.1 ? 2 : 1, i => { s.OverlayScale = i == 0 ? 0.85 : i == 2 ? 1.25 : 1.0; Changed(); }),
            Ui.Txt("الشفافية", 12.5, "TextDim").M(0, 8, 0, 6),
            Pills(new[] { "60%", "75%", "90%", "100%" }, s.OverlayOpacity < 0.7 ? 0 : s.OverlayOpacity < 0.85 ? 1 : s.OverlayOpacity < 0.95 ? 2 : 3,
                i => { s.OverlayOpacity = new[] { 0.6, 0.75, 0.9, 1.0 }[i]; Changed(); }),
            Ui.Txt("المعلومات المعروضة", 12.5, "TextDim").M(0, 10, 0, 8),
            metrics));

        // ---- benchmark
        benchBtn = Ui.Btn("ابدأ القياس", "", async () => await StartBenchCountdown());
        var prio = Ui.Check(Ui.Txt("رفع أولوية اللعبة تلقائياً (High) عند اللعب", 12.5, "TextDim"), s.AutoGamePriority, v => { s.AutoGamePriority = v; Settings.Save(); });
        compare = Ui.Card(new StackPanel(), 16, 12);
        compare.Background = Ui.B("Surface2");
        var benchCard = Ui.Card(Ui.VStack(
            Ui.Txt("قياس الأداء (Benchmark)", 15, "Text", FontWeights.Bold),
            Ui.Txt("• اختر «قبل» واقِس وانت تلعب\n• نظّف وطبّق التويكات\n• اختر «بعد» واقِس بنفس المكان باللعبة\n• داخل اللعبة: Ctrl+Shift+B للبدء والإيقاف", 12, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 12),
            Ui.Txt("اسم القياس", 12.5, "TextDim").M(0, 0, 0, 6),
            Pills(new[] { "قبل", "بعد" }, s.BenchLabel == "بعد" ? 1 : 0, i => { s.BenchLabel = i == 0 ? "قبل" : "بعد"; Settings.Save(); }),
            Ui.Txt("المدة", 12.5, "TextDim").M(0, 8, 0, 6),
            Pills(new[] { "30 ثانية", "60 ثانية", "120 ثانية" }, s.BenchmarkSeconds <= 30 ? 0 : s.BenchmarkSeconds <= 60 ? 1 : 2, i => { s.BenchmarkSeconds = new[] { 30, 60, 120 }[i]; Settings.Save(); }),
            Ui.HStack(14, benchBtn, benchStatus).M(0, 12, 0, 12),
            prio,
            compare.M(0, 14, 0, 10),
            results));

        root.Children.Add(live);
        root.Children.Add(Ui.Cols(new[] { "*", "16", "*" }, overlayCard, null, benchCard).M(0, 16, 0, 0));
        Children.Add(Ui.Scroll(root));
        Ui.Stagger(root, 70);

        timer.Tick += (_, _) => Tick();
        MainWindow.Instance.BenchmarkHotkey += () =>
        {
            if (FpsMonitor.Instance.Benchmarking) StopBench();
            else StartBench(Native.ForegroundPid());
        };
        LoadHistory();
    }

    public override void OnShow() => timer.Start();
    public override void OnHide() => timer.Stop();

    void Tick()
    {
        var (pid, fps, ft) = FpsMonitor.Instance.Foreground();
        if (pid == Environment.ProcessId)
        {
            // when MEDO Boost is in front, show the app that rendered most recently
            fpsBig.Text = "—";
            if (!FpsMonitor.Instance.Running) return;
            appName.Text = "بدّل للعبة – الـ FPS يظهر هنا وفي الأوفرلاي";
            return;
        }
        fpsBig.Text = fps > 0 ? $"{fps:0}" : "—";
        frameTime.Text = fps > 0 ? $"{ft:0.0} ms لكل فريم" : "";
        try { appName.Text = fps > 0 ? Process.GetProcessById(pid).ProcessName + ".exe" : "التطبيق الحالي لا يرسم فريمات"; } catch { }
        fpsChart.Push(fps);
    }

    async Task StartBenchCountdown()
    {
        if (FpsMonitor.Instance.Benchmarking) { StopBench(); return; }
        benchBtn.IsEnabled = false;
        for (int i = 5; i > 0; i--) { benchStatus.Text = $"بدّل للعبة… يبدأ خلال {i}"; await Task.Delay(1000); }
        benchBtn.IsEnabled = true;
        StartBench(Native.ForegroundPid());
    }

    void StartBench(int pid)
    {
        if (pid == Environment.ProcessId || pid <= 4) { benchStatus.Text = "لازم تكون اللعبة في المقدمة"; return; }
        try { benchApp = Process.GetProcessById(pid).ProcessName; } catch { benchApp = "?"; }
        FpsMonitor.Instance.BeginBenchmark(pid);
        benchEnd = DateTime.Now.AddSeconds(Settings.Current.BenchmarkSeconds);
        SetBenchButton(true);
        Dialogs.Toast($"بدأ القياس على {benchApp} لمدة {Settings.Current.BenchmarkSeconds} ثانية", "info");
        benchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        benchTimer.Tick += (_, _) =>
        {
            var left = benchEnd - DateTime.Now;
            benchStatus.Text = $"جاري القياس: {benchApp} – باقي {Math.Max(0, left.TotalSeconds):0} ث";
            if (left <= TimeSpan.Zero) StopBench();
        };
        benchTimer.Start();
    }

    void SetBenchButton(bool running)
    {
        var sp = (StackPanel)benchBtn.Content;
        ((TextBlock)sp.Children[0]).Text = running ? "" : "";
        ((TextBlock)sp.Children[1]).Text = running ? "إيقاف القياس" : "ابدأ القياس";
    }

    void StopBench()
    {
        benchTimer?.Stop();
        SetBenchButton(false);
        var r = FpsMonitor.Instance.EndBenchmark($"{Settings.Current.BenchLabel} – {benchApp}");
        if (r == null) { benchStatus.Text = "ما انسجلت فريمات كافية (تأكد اللعبة شغالة)"; return; }
        history.Add(r);
        SaveHistory();
        benchStatus.Text = $"✔ متوسط {r.AvgFps:0} FPS";
        Dialogs.Toast($"نتيجة القياس: {r.AvgFps:0} FPS متوسط – 1% Low: {r.Low1:0}");
        RenderResults();
    }

    void LoadHistory()
    {
        try { if (File.Exists(HistFile)) history = JsonSerializer.Deserialize<List<BenchResult>>(File.ReadAllText(HistFile)) ?? new(); } catch { }
        RenderResults();
    }

    void SaveHistory() { try { File.WriteAllText(HistFile, JsonSerializer.Serialize(history)); } catch { } }

    void RenderResults()
    {
        results.Children.Clear();
        var cs = (StackPanel)compare.Child;
        cs.Children.Clear();
        var before = history.LastOrDefault(h => h.Name.StartsWith("قبل"));
        var after = history.LastOrDefault(h => h.Name.StartsWith("بعد"));
        if (before != null && after != null)
        {
            UIElement Diff(string label, double a, double b)
            {
                double pct = a > 0 ? (b - a) / a * 100 : 0;
                var t = Ui.Txt($"{(pct >= 0 ? "+" : "")}{pct:0.0}%", 18, pct >= 0 ? "Good" : "Bad", FontWeights.Bold);
                return Ui.VStack(Ui.Txt(label, 12, "TextDim"), Ui.Txt($"{a:0} ← {b:0}", 13), t);
            }
            cs.Children.Add(Ui.Txt("المقارنة: قبل ← بعد", 13.5, "Text", FontWeights.SemiBold).M(0, 0, 0, 10));
            cs.Children.Add(Ui.Cols(new[] { "*", "*", "*" }, Diff("متوسط FPS", before.AvgFps, after.AvgFps), Diff("1% Low", before.Low1, after.Low1), Diff("0.1% Low", before.Low01, after.Low01)));
        }
        else cs.Children.Add(Ui.Txt("سوِّ قياس «قبل» وقياس «بعد» عشان تظهر المقارنة هنا.", 12.5, "TextDim", null, TextWrapping.Wrap));

        if (history.Count == 0) return;
        results.Children.Add(Ui.Cols(new[] { "*", "70", "70", "70", "30" }, Ui.Txt("القياس", 12, "TextMute"), Ui.Txt("متوسط", 12, "TextMute"), Ui.Txt("1% Low", 12, "TextMute"), Ui.Txt("0.1%", 12, "TextMute"), null).M(0, 0, 0, 6));
        foreach (var h in Enumerable.Reverse(history).Take(10))
        {
            var del = new Button { Style = Ui.S("IconBtn"), Content = "", Width = 26, Height = 24, ToolTip = "حذف" };
            var item = h;
            del.Click += (_, _) => { history.Remove(item); SaveHistory(); RenderResults(); };
            results.Children.Add(Ui.Cols(new[] { "*", "70", "70", "70", "30" },
                Ui.VStack(Ui.Txt(h.Name, 12.5, "Text", FontWeights.SemiBold), Ui.Txt($"{h.Time:yyyy-MM-dd HH:mm}  •  {h.Seconds:0} ث", 11, "TextMute")),
                Ui.Txt($"{h.AvgFps:0}", 13, "Accent2", FontWeights.Bold), Ui.Txt($"{h.Low1:0}", 13), Ui.Txt($"{h.Low01:0}", 13), del).M(0, 4, 0, 4));
        }
    }
}
