using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

/// <summary>"Clean from scratch": one-click Gaming + Programming profile and removal of preinstalled Windows apps.</summary>
public class FreshPage : PageBase
{
    // Tweaks applied by the profile on top of every "recommended + safe" tweak
    static readonly string[] ExtraProfileTweaks = { "s_xbox", "d_hidden", "d_wslmem" };

    readonly Dictionary<BloatApp, CheckBox> appChecks = new();
    readonly Dictionary<BloatApp, Border> appRows = new();
    readonly StackPanel appsList = new();
    readonly TextBlock appsStatus = Ui.Txt("جاري فحص التطبيقات المثبتة...", 12.5, "TextDim");
    readonly StackPanel steps = new();
    readonly ProgressBar progress = Ui.Progress();
    readonly Button runBtn;
    readonly CheckBox optRestore, optTweaks, optApps, optOneDrive, optClean, optHvci;
    readonly TextBlock oneDriveState = Ui.Txt("", 12.5, "TextDim");
    bool loaded, busy;

    public FreshPage()
    {
        var root = new StackPanel();
        root.Children.Add(Ui.Title("تنظيف من الصفر"));
        root.Children.Add(Ui.Txt("للجهاز اللي للألعاب والبرمجة بس: يشيل كل شي زايد من Windows ويضبط النظام بضغطة وحدة. WSL و Docker و Android Studio و VS Code والمتجر ما ينلمسوا.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 18));

        // ---------- profile card ----------
        CheckBox Opt(string text, string sub, bool on, bool warn = false)
        {
            var content = Ui.VStack(Ui.HStack(0, Ui.Txt(text, 13.5, "Text", FontWeights.SemiBold), warn ? Ui.Badge("انتبه", "Warn", "#22F59E0B") : new Border()),
                                    Ui.Txt(sub, 12, "TextDim", null, TextWrapping.Wrap));
            var c = Ui.Check(content, on);
            c.Margin = new Thickness(0, 0, 0, 10);
            return c;
        }
        optRestore = Opt("1. نقطة استعادة", "عشان ترجع لكل شي إذا ما عجبك.", true);
        optTweaks = Opt("2. تويكات الألعاب + البرمجة", "كل التويكات المنصوح بها + Ultimate Performance + Nagle + إيقاف خدمات Xbox والتتبع + Long Paths + وضع المطور + تحديد رام WSL.", true);
        optApps = Opt("3. إزالة التطبيقات الزائدة", "News، Weather، Cortana، Teams، Clipchamp، Xbox، Candy Crush… (القائمة تحت، تقدر تعدّلها).", true);
        optOneDrive = Opt("4. إزالة OneDrive", "تأكد إن ملفاتك اللي على OneDrive موجودة بجهازك قبل.", true, true);
        optClean = Opt("5. تنظيف عميق + تحرير الرام", "كل عناصر التنظيف الآمنة.", true);
        optHvci = Opt("إيقاف Memory Integrity (اختياري)", "يرفع FPS على أجهزة كثيرة لكن يقلل الحماية. يحتاج ريستارت.", false, true);

        runBtn = Ui.Btn("طبّق بروفايل الألعاب + البرمجة", "", async () => await RunProfile());
        runBtn.Padding = new Thickness(24, 12, 24, 12);
        runBtn.FontSize = 14.5;
        runBtn.HorizontalAlignment = HorizontalAlignment.Left;
        progress.Visibility = Visibility.Collapsed;

        var head = Ui.Cols(new[] { "Auto", "*" },
            new Border { Width = 54, Height = 54, CornerRadius = Ui.Cr(16), Background = Ui.B("AccentGrad"), Child = Ui.Icon("", 24) },
            Ui.VStack(Ui.Txt("بروفايل الألعاب + البرمجة", 18, "Text", FontWeights.Bold), Ui.Txt("كل شي بالترتيب الصح بضغطة وحدة. كل خطوة تنسجل وكل تويك يرجع.", 12.5, "TextDim")).M(14, 0, 0, 0));
        var profileCard = Ui.Card(Ui.VStack(
            head, Ui.Sep(),
            Ui.Cols(new[] { "*", "24", "*" }, Ui.VStack(optRestore, optTweaks, optApps), null, Ui.VStack(optOneDrive, optClean, optHvci)),
            runBtn.M(0, 6, 0, 0), progress.M(0, 14, 0, 0), steps.M(0, 8, 0, 0)), 22);
        profileCard.BorderBrush = Ui.B("Accent");
        root.Children.Add(profileCard);

        // ---------- apps ----------
        var selAll = Ui.Btn("تحديد المنصوح به", null, () => { foreach (var (a, c) in appChecks) c.IsChecked = a.DefaultOn; }, "BtnGhost");
        var removeBtn = Ui.Btn("إزالة المحدد", "", async () => await RemoveSelected());
        var refresh = Ui.Btn("تحديث", "", async () => await LoadApps(), "BtnGhost");
        var appsCard = Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "*", "Auto" }, Ui.VStack(Ui.Txt("التطبيقات المثبتة مسبقاً", 16, "Text", FontWeights.Bold), appsStatus),
                Ui.HStack(8, refresh, selAll, removeBtn)),
            Ui.Txt("يتم حذفها لكل المستخدمين وما ترجع تتثبت لحالها. تقدر ترجع أي واحد من Microsoft Store. ما نلمس: المتجر، winget، Terminal، الحاسبة، Notepad، Paint، الصور، ومكتبات التشغيل.", 12, "TextMute", null, TextWrapping.Wrap).M(0, 8, 0, 4),
            Ui.Sep(), appsList), 20);
        root.Children.Add(appsCard.M(0, 16, 0, 0));

        // ---------- OneDrive ----------
        var odBtn = Ui.Btn("إزالة OneDrive", "", async () =>
        {
            if (!await Dialogs.Confirm("إزالة OneDrive", "تأكد إن كل ملفاتك اللي على OneDrive محمّلة على جهازك (مش «Online-only»).\nالملفات بمجلد OneDrive تضل موجودة بس ما رح تتزامن.", "أزل OneDrive", true)) return;
            await Task.Run(Debloat.RemoveOneDrive);
            UpdateOneDrive();
            Dialogs.Toast("تمت إزالة OneDrive");
        }, "BtnGhost");
        root.Children.Add(Ui.Card(Ui.Cols(new[] { "Auto", "*", "Auto" }, Ui.Icon("", 26, "Accent2").M(0, 0, 14, 0),
            Ui.VStack(Ui.Txt("OneDrive", 15, "Text", FontWeights.Bold), oneDriveState), odBtn), 18).M(0, 16, 0, 10));

        Children.Add(Ui.Scroll(root));
        Ui.Stagger(root, 70);
    }

    public override void OnShow()
    {
        UpdateOneDrive();
        if (!loaded) { loaded = true; _ = LoadApps(); }
    }

    void UpdateOneDrive() => oneDriveState.Text = Debloat.OneDriveInstalled() ? "مثبت – يشتغل مع Windows ويزامن بالخلفية" : "غير مثبت ✔";

    async Task LoadApps()
    {
        appsStatus.Text = "جاري فحص التطبيقات المثبتة...";
        try { await Task.Run(Debloat.RefreshInstalled); }
        catch (Exception ex) { appsStatus.Text = "تعذر الفحص: " + ex.Message; return; }
        appsList.Children.Clear(); appChecks.Clear(); appRows.Clear();
        foreach (var g in Debloat.Groups)
        {
            var items = Debloat.Apps.Where(a => a.Group == g && a.Installed).ToList();
            if (items.Count == 0) continue;
            appsList.Children.Add(Ui.Txt(g, 13.5, "Accent2", FontWeights.SemiBold).M(0, 10, 0, 6));
            var wrap = new WrapPanel();
            foreach (var a in items)
            {
                var label = Ui.VStack(Ui.Txt(a.Name, 13), a.Note.Length > 0 ? Ui.Txt(a.Note, 11, "TextMute", null, TextWrapping.Wrap) : new Border());
                var c = Ui.Check(label, a.DefaultOn);
                appChecks[a] = c;
                var cell = new Border
                {
                    Width = 250, Margin = new Thickness(0, 0, 10, 8), Padding = new Thickness(12, 9, 12, 9), CornerRadius = Ui.Cr(10),
                    Background = Ui.B("Surface2"), BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(1), Child = c,
                };
                appRows[a] = cell;
                wrap.Children.Add(cell);
            }
            appsList.Children.Add(wrap);
        }
        int n = Debloat.Apps.Count(a => a.Installed);
        appsStatus.Text = n == 0 ? "جهازك نظيف – ما في تطبيقات زائدة 👌" : $"لقيت {n} تطبيق زايد مثبت";
        if (n == 0) appsList.Children.Add(Ui.Txt("✔ ما في شي للإزالة", 13, "Good"));
    }

    async Task<int> RemoveApps(IEnumerable<BloatApp> apps, Action<string> report)
    {
        int ok = 0;
        foreach (var a in apps.ToList())
        {
            report($"إزالة {a.Name}...");
            if (await Task.Run(() => Debloat.Remove(a)))
            {
                ok++;
                if (appRows.TryGetValue(a, out var row)) row.Opacity = 0.35;
                if (appChecks.TryGetValue(a, out var c)) { c.IsChecked = false; c.IsEnabled = false; }
            }
        }
        return ok;
    }

    async Task RemoveSelected()
    {
        if (busy) return;
        var sel = appChecks.Where(kv => kv.Value.IsChecked == true && kv.Value.IsEnabled).Select(kv => kv.Key).ToList();
        if (sel.Count == 0) { Dialogs.Toast("اختر تطبيقات أول", "warn"); return; }
        if (!await Dialogs.Confirm("إزالة التطبيقات", $"سيتم حذف {sel.Count} تطبيق:\n" + string.Join("، ", sel.Select(a => a.Name)), "احذف")) return;
        busy = true;
        int ok = await RemoveApps(sel, t => appsStatus.Text = t);
        busy = false;
        appsStatus.Text = $"✔ تم حذف {ok} من {sel.Count}";
        Dialogs.Toast($"تم حذف {ok} تطبيق");
    }

    void Step(string text, string state)
    {
        string glyph = state switch { "ok" => "", "err" => "", "skip" => "", _ => "" };
        string color = state switch { "ok" => "Good", "err" => "Bad", "skip" => "TextMute", _ => "Accent2" };
        var row = Ui.HStack(10, Ui.Icon(glyph, 13, color), Ui.Txt(text, 12.5, state == "run" ? "Text" : "TextDim", null, TextWrapping.Wrap));
        row.Margin = new Thickness(0, 3, 0, 3);
        steps.Children.Add(row);
        Ui.FadeIn(row, 0, 6, 220);
    }

    async Task RunProfile()
    {
        if (busy) return;
        if (!await Dialogs.Confirm("بروفايل الألعاب + البرمجة",
            "رح أطبق الخطوات المحددة بالترتيب. ممكن تاخذ من 3 لـ 10 دقايق.\nسكّر ألعابك وبرامجك المهمة قبل. بالآخر لازم تعيد تشغيل الجهاز.", "ابدأ")) return;
        busy = true; runBtn.IsEnabled = false;
        steps.Children.Clear();
        progress.Visibility = Visibility.Visible; progress.Value = 0;
        int total = new[] { optRestore, optTweaks, optApps, optOneDrive, optClean, optHvci }.Count(c => c.IsChecked == true), done = 0;
        void Next() => progress.Value = ++done * 100.0 / Math.Max(1, total);
        Log.Info("=== بروفايل الألعاب + البرمجة ===");
        try
        {
            if (optRestore.IsChecked == true)
            {
                Step("إنشاء نقطة استعادة (ممكن دقيقة)...", "run");
                var (ok, msg) = await Task.Run(Tweaks.CreateRestorePoint);
                Step(ok ? "نقطة الاستعادة جاهزة" : "نقطة الاستعادة فشلت: " + msg, ok ? "ok" : "err");
                if (!ok && !await Dialogs.Confirm("نقطة الاستعادة فشلت", "غالباً «حماية النظام» مطفية. تكمل بدونها؟ (كل التويكات تضل تقدر ترجعها من البرنامج)", "كمّل")) return;
                Next();
            }
            if (optTweaks.IsChecked == true)
            {
                var list = Tweaks.All.Where(t => (t.Recommended && t.Risk == Risk.Safe) || ExtraProfileTweaks.Contains(t.Id)).ToList();
                Step($"تطبيق {list.Count} تويك...", "run");
                var (applied, failed) = await Task.Run(() =>
                {
                    int a = 0; var f = new List<string>();
                    foreach (var t in list)
                    {
                        try { if (!t.IsApplied()) { t.Apply(); a++; } } catch (Exception ex) { f.Add(t.Name); Log.Error(t.Name + ": " + ex.Message); }
                    }
                    return (a, f);
                });
                Step($"تم تطبيق {applied} تويك" + (failed.Count > 0 ? $" (فشل: {string.Join("، ", failed)})" : ""), failed.Count > 0 ? "err" : "ok");
                Next();
            }
            if (optHvci.IsChecked == true)
            {
                var t = Tweaks.All.First(x => x.Id == "g_hvci");
                await Task.Run(t.Apply);
                Step("تم إيقاف Memory Integrity (بعد الريستارت)", "ok");
                Next();
            }
            if (optApps.IsChecked == true)
            {
                if (appChecks.Count == 0) { loaded = true; await LoadApps(); }
                var sel = appChecks.Where(kv => kv.Value.IsChecked == true && kv.Value.IsEnabled).Select(kv => kv.Key).ToList();
                Step($"إزالة {sel.Count} تطبيق زايد...", "run");
                var last = steps.Children[^1] as StackPanel;
                int ok = await RemoveApps(sel, txt => { if (last?.Children[1] is TextBlock tb) tb.Text = txt; });
                Step($"تم حذف {ok} من {sel.Count} تطبيق", ok == sel.Count ? "ok" : "err");
                Next();
            }
            if (optOneDrive.IsChecked == true)
            {
                if (Debloat.OneDriveInstalled())
                {
                    Step("إزالة OneDrive...", "run");
                    await Task.Run(Debloat.RemoveOneDrive);
                    Step(Debloat.OneDriveInstalled() ? "OneDrive ما انحذف بالكامل" : "تمت إزالة OneDrive", Debloat.OneDriveInstalled() ? "err" : "ok");
                }
                else Step("OneDrive مش مثبت", "skip");
                UpdateOneDrive();
                Next();
            }
            if (optClean.IsChecked == true)
            {
                Step("تنظيف عميق...", "run");
                var items = Cleaner.Items.Where(i => i.DefaultOn && i.Risk == Risk.Safe).ToList();
                long freed = await Task.Run(() =>
                {
                    long f = 0;
                    using var w = ProcessManager.DeletedListWriter();
                    w.WriteLine($"===== Profile {DateTime.Now} =====");
                    foreach (var i in items) { Cleaner.Scan(i); Cleaner.Clean(i, w); f += i.Freed; }
                    return f;
                });
                long ram = await Task.Run(() => Memory.CleanAll());
                Step($"تم حذف {Util.Size(freed)} ملفات زائدة وتحرير {Util.Size(ram)} رام", "ok");
                Next();
            }
            Step("خلصنا! 🎉 أعد تشغيل الجهاز عشان كل التويكات تشتغل.", "ok");
            Log.Ok("=== البروفايل اكتمل ===");
            if (await Dialogs.Confirm("خلصنا 🎉", "كل الخطوات اكتملت. لازم تعيد تشغيل الجهاز عشان HAGS وخطة الطاقة وباقي التويكات تشتغل.\nتعيد التشغيل الحين؟", "أعد التشغيل", false, "بعدين"))
                Util.Run("shutdown.exe", "/r /t 5 /c \"KHA Boost\"", 10000);
        }
        catch (Exception ex)
        {
            Step("خطأ: " + ex.Message, "err");
            Log.Error("Profile: " + ex);
        }
        finally { busy = false; runBtn.IsEnabled = true; }
    }
}
