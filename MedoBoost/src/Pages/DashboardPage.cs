using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class DashboardPage : PageBase
{
    readonly RingGauge cpuG = new(), gpuG = new("#FF22C55E", "#FF06B6D4", "#FF3B82F6"), ramG = new("#FFF472B6", "#FFEC4899", "#FF8B5CF6"), diskG = new("#FFF59E0B", "#FFEC4899", "#FF8B5CF6");
    readonly Sparkline loadChart = new("#FF8B5CF6", "#FF22C55E");
    readonly Sparkline netChart = new("#FF06B6D4", "#FFEC4899") { FixedMax = 0 };
    readonly TextBlock netText = Ui.Txt("", 12, "TextDim").Ltr();
    readonly Dictionary<string, TextBlock> sensors = new();
    readonly TextBlock scoreText = Ui.GradText("--", 46);
    readonly TextBlock scoreSub = Ui.Txt("جاري فحص الجهاز...", 13, "TextDim");
    readonly TextBlock junkText = Ui.Txt("...", 14, "Text", FontWeights.SemiBold).Ltr();
    readonly TextBlock tweakText = Ui.Txt("...", 14, "Text", FontWeights.SemiBold).Ltr();
    readonly TextBlock startupText = Ui.Txt("...", 14, "Text", FontWeights.SemiBold).Ltr();
    readonly Border pawnHint;
    readonly TextBlock cpuNameT = Ui.Txt("", 12, "TextMute"), gpuNameT = Ui.Txt("", 12, "TextMute");
    bool visible, scored;

    public DashboardPage()
    {
        var root = new StackPanel();

        // ---- header
        var hw = HardwareMonitor.Instance;
        string os = OsName();
        var ramTotal = Util.Size((long)hw.Last.RamTotal);
        var header = Ui.VStack(
            Ui.Title($"أهلاً {Environment.UserName} 👋"),
            Ui.Txt($"{os}   •   {Environment.MachineName}", 13, "TextDim").M(0, 4, 0, 0));
        root.Children.Add(header);

        // ---- full PC specs (like Windows Settings → About)
        var specsRow1 = new UniformGrid4();
        var specsRow2 = new UniformGrid4();
        foreach (var (key, glyph, label) in new[] { ("cpu", "\uE950", "المعالج"), ("ram", "\uE964", "الرام"), ("gpu", "\uE7F4", "كرت الشاشة"), ("disk", "\uEDA2", "التخزين") })
            specsRow1.Add(SpecCard(key, glyph, label));
        foreach (var (key, glyph, label) in new[] { ("board", "\uE8EA", "اللوحة الأم"), ("display", "\uE7F8", "الشاشة") })
            specsRow2.Add(SpecCard(key, glyph, label));
        var copySpecs = Ui.Btn("نسخ المواصفات", "\uE8C8", () =>
        {
            if (specs == null) return;
            try { System.Windows.Clipboard.SetText(specs.ToString()); Dialogs.Toast("تم نسخ المواصفات ✔"); } catch { }
        }, "BtnGhost");
        copySpecs.Padding = new Thickness(12, 6, 12, 6);
        specsRow2.Add(new Border());
        specsRow2.Add(copySpecs);
        copySpecs.VerticalAlignment = VerticalAlignment.Center;
        copySpecs.HorizontalAlignment = HorizontalAlignment.Left;
        root.Children.Add(specsRow1.Panel.M(0, 18, 0, 0));
        root.Children.Add(specsRow2.Panel.M(0, 12, 0, 0));
        _ = LoadSpecs();

        // ---- gauges
        var gauges = new UniformGrid4();
        gauges.Add(GaugeCard(cpuG, "المعالج CPU", ""));
        gauges.Add(GaugeCard(gpuG, "كرت الشاشة GPU", ""));
        gauges.Add(GaugeCard(ramG, "الذاكرة RAM", ""));
        gauges.Add(GaugeCard(diskG, "القرص " + Util.SystemDrive.TrimEnd('\\'), ""));
        cpuG.Label = "CPU"; gpuG.Label = "GPU"; ramG.Label = "RAM"; diskG.Label = "DISK";
        root.Children.Add(gauges.Panel.M(0, 22, 0, 0));

        // ---- charts + score
        var loadCard = Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "*", "Auto" }, Ui.Txt("المعالج والكرت", 14, "Text", FontWeights.SemiBold),
                Ui.HStack(12, Legend("#FF8B5CF6", "CPU"), Legend("#FF22C55E", "GPU"))),
            loadChart.M(0, 14, 0, 0)));
        var netCard = Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "*", "Auto" }, Ui.Txt("الشبكة", 14, "Text", FontWeights.SemiBold), Ui.HStack(12, Legend("#FF06B6D4", "تحميل"), Legend("#FFEC4899", "رفع"))),
            netChart.M(0, 14, 0, 0), netText.M(0, 8, 0, 0)));

        var scoreBtn = Ui.Btn("تحسين الآن", "", () => MainWindow.Instance.Navigate("fresh"));
        scoreBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        scoreBtn.ToolTip = "يفتح بروفايل الألعاب + البرمجة";
        var scoreCard = Ui.Card(Ui.VStack(
            Ui.Txt("صحة الجهاز", 14, "Text", FontWeights.SemiBold),
            Ui.HStack(6, scoreText, Ui.Txt("/100", 16, "TextMute")).Ltr().M(0, 6, 0, 0),
            scoreSub,
            Ui.Sep(),
            Row("", "ملفات زائدة", junkText), Row("", "تويكات مفعلة", tweakText), Row("", "برامج بدء التشغيل", startupText),
            scoreBtn.M(0, 14, 0, 0)));

        var row2 = Ui.Cols(new[] { "1.4*", "16", "1*", "16", "1*" }, loadCard, null, netCard, null, scoreCard);
        root.Children.Add(row2.M(0, 16, 0, 0));

        // ---- sensors + quick actions
        var sensGrid = new WrapPanel();
        foreach (var (k, label, glyph) in new[]
        {
            ("cpuTemp", "حرارة المعالج", ""), ("cpuClock", "سرعة المعالج", ""), ("cpuPower", "طاقة المعالج", ""),
            ("gpuTemp", "حرارة الكرت", ""), ("gpuHot", "Hot Spot", ""), ("gpuClock", "سرعة الكرت", ""),
            ("gpuPower", "طاقة الكرت", ""), ("vram", "ذاكرة الكرت VRAM", ""), ("gpuFan", "مروحة الكرت", ""),
        })
        {
            var v = Ui.Txt("—", 17, "Text", FontWeights.Bold).Ltr();
            v.HorizontalAlignment = HorizontalAlignment.Right;
            sensors[k] = v;
            var cell = Ui.Card(Ui.VStack(Ui.Txt(label, 12, "TextDim"), v.M(0, 4, 0, 0)), 14, 12);
            cell.Width = 150; cell.Margin = new Thickness(0, 0, 10, 10); cell.Background = Ui.B("Surface2");
            sensGrid.Children.Add(cell);
        }
        pawnHint = Ui.Card(Ui.VStack(
            Ui.Txt("حرارة المعالج تحتاج تعريف PawnIO", 13, "Warn", FontWeights.SemiBold),
            Ui.Txt("PawnIO تعريف مفتوح المصدر وموقّع (بديل آمن لـ WinRing0 اللي يحظره Windows Defender). ثبّته مرة وحدة وأعد تشغيل البرنامج.", 12, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 8),
            Ui.Btn("تحميل PawnIO من الموقع الرسمي", "", () => Util.OpenUrl("https://pawnio.eu/"), "BtnGhost")), 14, 12);
        pawnHint.Visibility = Visibility.Collapsed;
        pawnHint.Background = Ui.Solid("#22F59E0B");
        var sensCard = Ui.Card(Ui.VStack(
            Ui.Txt("الحساسات والحرارة", 14, "Text", FontWeights.SemiBold).M(0, 0, 0, 12),
            cpuNameT, gpuNameT.M(0, 0, 0, 10),
            sensGrid, pawnHint));

        var quick = Ui.Card(Ui.VStack(
            Ui.Txt("إجراءات سريعة", 14, "Text", FontWeights.SemiBold).M(0, 0, 0, 14),
            QuickBtn("", "تنظيف سريع", "يحذف الملفات المؤقتة والكاش الآمن", async () => await QuickClean()),
            QuickBtn("", "تحرير الرام", "يفرّغ الذاكرة غير المستخدمة", async () =>
            {
                var f = await Task.Run(() => Memory.CleanAll());
                Dialogs.Toast($"تم تحرير {Util.Size(f)} من الرام");
            }),
            QuickBtn("", "Game Boost", "رام + إغلاق البرامج المختارة (Ctrl+Shift+G)", async () => await GameBoost.Run()),
            QuickBtn("", "تشغيل/إيقاف الأوفرلاي", "FPS والحرارة فوق الشاشة (Ctrl+Shift+F)", () =>
            {
                MainWindow.Instance.SetOverlay(!(MainWindow.Instance.Overlay?.IsVisible ?? false));
                return Task.CompletedTask;
            })));
        root.Children.Add(Ui.Cols(new[] { "1.6*", "16", "1*" }, sensCard, null, quick).M(0, 16, 0, 0));

        Children.Add(Ui.Scroll(root));
        Ui.Stagger(root, 70);

        hw.Updated += s => Dispatcher.BeginInvoke(() => { if (visible) Update(s); });
    }

    readonly Dictionary<string, (TextBlock value, TextBlock sub)> specCells = new();
    SystemSpecs specs;

    Border SpecCard(string key, string glyph, string label)
    {
        var v = Ui.Txt("...", 15, "Text", FontWeights.Bold, TextWrapping.Wrap);
        var sub = Ui.Txt("", 11.5, "TextDim", null, TextWrapping.Wrap);
        specCells[key] = (v, sub);
        var card = Ui.Card(Ui.VStack(Ui.HStack(8, Ui.Icon(glyph, 13, "Accent2"), Ui.Txt(label, 12, "TextDim")), v.M(0, 8, 0, 4), sub), 14, 12);
        card.Background = Ui.B("Surface2");
        return card;
    }

    async Task LoadSpecs()
    {
        specs = await Task.Run(SystemInfo.Read);
        void Set(string k, string v, string sub) { if (specCells.TryGetValue(k, out var c)) { c.value.Text = v; c.sub.Text = sub; } }
        Set("cpu", specs.Cpu, specs.CpuSub);
        Set("ram", specs.Ram, specs.RamSub);
        Set("gpu", specs.Gpu, specs.GpuSub);
        Set("disk", specs.Storage, specs.StorageSub);
        Set("board", specs.Board, specs.BoardSub);
        Set("display", specs.Display, specs.DisplaySub);
        if (specs.StorageDetail.Length > 0 && specCells.TryGetValue("disk", out var d)) ((FrameworkElement)d.value.Parent).ToolTip = specs.StorageDetail;
        // numbers + units must read left-to-right ("16 GB", "1920×1080"); keep them on the right edge like the labels
        foreach (var k in new[] { "ram", "disk", "display" })
            if (specCells.TryGetValue(k, out var c)) { c.value.FlowDirection = FlowDirection.LeftToRight; c.value.HorizontalAlignment = HorizontalAlignment.Left; }
    }

    static string OsName()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var name = k?.GetValue("ProductName") as string ?? "Windows";
            var build = k?.GetValue("CurrentBuild") as string ?? "";
            var disp = k?.GetValue("DisplayVersion") as string ?? "";
            if (int.TryParse(build, out var b) && b >= 22000) name = name.Replace("Windows 10", "Windows 11");
            return $"{name} {disp} (Build {build})";
        }
        catch { return "Windows"; }
    }

    static UIElement Legend(string color, string text) => Ui.HStack(6, new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), Background = Ui.Solid(color) }, Ui.Txt(text, 12, "TextDim"));

    static UIElement Row(string glyph, string label, TextBlock value)
        => Ui.Cols(new[] { "Auto", "*", "Auto" }, Ui.Icon(glyph, 14, "Accent2").M(0, 0, 10, 0), Ui.Txt(label, 13, "TextDim"), value).M(0, 4, 0, 4);

    static Border GaugeCard(RingGauge g, string title, string glyph)
    {
        var t = Ui.HStack(8, Ui.Icon(glyph, 14, "TextDim"), Ui.Txt(title, 13, "TextDim"));
        t.HorizontalAlignment = HorizontalAlignment.Center;
        g.Width = 160; g.Height = 160; g.HorizontalAlignment = HorizontalAlignment.Center;
        return Ui.Card(Ui.VStack(t, g.M(0, 8, 0, 0)), 16);
    }

    static Button QuickBtn(string glyph, string title, string sub, Func<Task> act)
    {
        var icon = new Border
        {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = Ui.B("AccentGrad"),
            Child = Ui.Icon(glyph, 17, "Text"),
        };
        var content = Ui.Cols(new[] { "Auto", "*" }, icon, Ui.VStack(Ui.Txt(title, 13.5, "Text", FontWeights.SemiBold), Ui.Txt(sub, 11.5, "TextDim")).M(12, 0, 0, 0));
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        var b = new Button { Style = Ui.S("BtnGhost"), Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 8) };
        b.Click += async (_, _) =>
        {
            b.IsEnabled = false;
            try { await act(); } catch (Exception ex) { Dialogs.Toast(ex.Message, "err"); }
            b.IsEnabled = true;
        };
        return b;
    }

    void Update(HwSnapshot s)
    {
        cpuG.Set(s.CpuLoad, $"{s.CpuLoad:0}%", Ui.TempText(s.CpuTemp));
        gpuG.Set(double.IsNaN(s.GpuLoad) ? 0 : s.GpuLoad, double.IsNaN(s.GpuLoad) ? "—" : $"{s.GpuLoad:0}%", Ui.TempText(s.GpuTemp));
        double used = (s.RamTotal - s.RamAvail) / 1073741824.0, total = s.RamTotal / 1073741824.0;
        ramG.Set(s.RamLoad, $"{s.RamLoad:0}%", $"{used:0.0} / {total:0} GB");
        try
        {
            var d = new DriveInfo(Util.SystemDrive);
            double pct = 100.0 * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize;
            diskG.Set(pct, $"{pct:0}%", $"متاح {Util.Size(d.TotalFreeSpace)}");
        }
        catch { }
        loadChart.Push(s.CpuLoad, double.IsNaN(s.GpuLoad) ? 0 : s.GpuLoad);
        netChart.Push(s.NetDownBps, s.NetUpBps);
        netText.Text = $"↓ {Util.Size((long)s.NetDownBps)}/s    ↑ {Util.Size((long)s.NetUpBps)}/s";

        string F(double v, string unit, string fmt = "0") => double.IsNaN(v) ? "—" : v.ToString(fmt) + unit;
        sensors["cpuTemp"].Text = F(s.CpuTemp, "°C");
        sensors["cpuClock"].Text = F(s.CpuClock, " MHz");
        sensors["cpuPower"].Text = F(s.CpuPower, " W");
        sensors["gpuTemp"].Text = F(s.GpuTemp, "°C");
        sensors["gpuHot"].Text = F(s.GpuHotSpot, "°C");
        sensors["gpuClock"].Text = F(s.GpuClock, " MHz");
        sensors["gpuPower"].Text = F(s.GpuPower, " W");
        sensors["vram"].Text = double.IsNaN(s.VramUsedMb) ? "—" : $"{s.VramUsedMb / 1024:0.0}" + (double.IsNaN(s.VramTotalMb) ? " GB" : $" / {s.VramTotalMb / 1024:0} GB");
        sensors["gpuFan"].Text = F(s.GpuFan, " RPM");
        foreach (var k in new[] { "cpuTemp", "gpuTemp", "gpuHot" })
        {
            double v = k == "cpuTemp" ? s.CpuTemp : k == "gpuTemp" ? s.GpuTemp : s.GpuHotSpot;
            sensors[k].Foreground = double.IsNaN(v) ? Ui.B("Text") : new SolidColorBrush(Ui.Heat(v, 75, 88));
        }
        cpuNameT.Text = HardwareMonitor.Instance.CpuName;
        gpuNameT.Text = HardwareMonitor.Instance.GpuName;
        pawnHint.Visibility = double.IsNaN(s.CpuTemp) && !HardwareMonitor.Instance.PawnIoInstalled ? Visibility.Visible : Visibility.Collapsed;
    }

    public override void OnShow()
    {
        visible = true;
        Update(HardwareMonitor.Instance.Last);
        if (!scored) { scored = true; _ = ComputeScore(); }
    }

    public override void OnHide() => visible = false;

    async Task ComputeScore()
    {
        var (junk, tw, twTotal, st) = await Task.Run(() =>
        {
            long j = 0;
            foreach (var i in Cleaner.Items.Where(i => i.DefaultOn && i.CustomClean == null)) { Cleaner.Scan(i); j += i.Size; }
            int applied = 0, total = 0;
            foreach (var t in Tweaks.All.Where(t => t.Recommended)) { total++; try { if (t.IsApplied()) applied++; } catch { } }
            int s = StartupManager.List().Count(e => e.Enabled);
            return (j, applied, total, s);
        });
        double score = 100;
        score -= Math.Min(30, junk / 1073741824.0 * 6);              // -6 per GB of junk (max 30)
        score -= (twTotal - tw) * 35.0 / Math.Max(1, twTotal);       // missing recommended tweaks (max 35)
        score -= Math.Max(0, st - 5) * 3;                             // more than 5 startup apps
        score -= Math.Max(0, HardwareMonitor.Instance.Last.RamLoad - 75) * 0.5;
        score = Math.Clamp(score, 5, 100);
        Ui.CountTo(scoreText, 0, score, v => $"{v:0}", 1400);
        scoreSub.Text = score >= 85 ? "جهازك بحالة ممتازة 🔥" : score >= 60 ? "جيد – في مجال للتحسين" : "يحتاج تنظيف وتحسين";
        junkText.Text = Util.Size(junk);
        tweakText.Text = $"{tw} / {twTotal}";
        startupText.Text = st.ToString();
    }

    async Task QuickClean()
    {
        var items = Cleaner.Items.Where(i => i.DefaultOn && i.Risk == Risk.Safe && (i.Category == Cleaner.Categories[0] || i.Category == Cleaner.Categories[1]) && i.Id != "wu_cache" && i.Id != "delivery_opt").ToList();
        long freed = await Task.Run(() =>
        {
            long f = 0;
            using var w = ProcessManager.DeletedListWriter();
            foreach (var i in items) { Cleaner.Scan(i); Cleaner.Clean(i, w); f += i.Freed; }
            return f;
        });
        Dialogs.Toast($"تنظيف سريع: تم حذف {Util.Size(freed)}");
        scored = false; _ = ComputeScore(); scored = true;
    }

    async Task OneClick()
    {
        if (!await Dialogs.Confirm("تحسين بضغطة واحدة",
            "سيتم: إنشاء نقطة استعادة ← تطبيق كل التويكات المنصوح بها (يمكن إرجاعها) ← تنظيف الملفات المؤقتة ← تحرير الرام.\nقد يأخذ دقيقة أو دقيقتين.", "ابدأ"))
            return;
        Dialogs.Toast("جاري إنشاء نقطة استعادة...", "info");
        await Task.Run(() => Tweaks.CreateRestorePoint());
        int n = await Task.Run(() =>
        {
            int c = 0;
            foreach (var t in Tweaks.All.Where(t => t.Recommended && t.Risk == Risk.Safe))
                try { if (!t.IsApplied()) { t.Apply(); c++; } } catch (Exception ex) { Log.Error(t.Name + ": " + ex.Message); }
            return c;
        });
        await QuickClean();
        var f = await Task.Run(() => Memory.CleanAll());
        Dialogs.Toast($"✔ تم! طُبق {n} تويك وتحرر {Util.Size(f)} رام. يفضل إعادة تشغيل الجهاز.");
    }
}

/// <summary>Responsive 4-column card grid.</summary>
public class UniformGrid4
{
    public Grid Panel { get; } = new();
    int n;
    public void Add(UIElement e)
    {
        if (Panel.ColumnDefinitions.Count > 0) Panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        Panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(e, Panel.ColumnDefinitions.Count - 1);
        Panel.Children.Add(e);
        n++;
    }
}
