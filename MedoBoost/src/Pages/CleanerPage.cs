using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class CleanerPage : PageBase
{
    class Row
    {
        public CleanItem Item;
        public CheckBox Check;
        public TextBlock Size;
        public Border Running;
    }

    readonly List<Row> rows = new();
    readonly Dictionary<string, TextBlock> catTotals = new();
    readonly TextBlock bigTotal = Ui.GradText("0 B", 40);
    readonly TextBlock status = Ui.Txt("اضغط «فحص» لمعرفة حجم الملفات الزائدة", 13, "TextDim");
    readonly ProgressBar progress = Ui.Progress();
    readonly Button scanBtn, cleanBtn;
    readonly CheckBox closeApps;
    readonly TextBlock spinner = Ui.Icon("", 18, "Accent2");
    bool busy, scannedOnce;

    public CleanerPage()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        scanBtn = Ui.Btn("فحص", "", async () => await Scan(), "BtnGhost");
        cleanBtn = Ui.Btn("تنظيف المحدد", "", async () => await Clean());
        closeApps = Ui.Check(Ui.Txt("إغلاق البرامج المفتوحة تلقائياً (ديسكورد، المتصفحات...) لتنظيف أعمق", 12.5, "TextDim"), false);
        var showDeleted = Ui.Btn("عرض الملفات المحذوفة", "", () =>
        {
            var f = Path.Combine(Util.DataDir, "logs", $"deleted-files-{DateTime.Now:yyyy-MM-dd}.txt");
            if (File.Exists(f)) Process.Start(new ProcessStartInfo("notepad.exe", $"\"{f}\"") { UseShellExecute = true });
            else Dialogs.Toast("ما في ملفات محذوفة اليوم بعد", "info");
        }, "BtnGhost");
        progress.Visibility = Visibility.Hidden;
        spinner.Visibility = Visibility.Collapsed;

        var head = Ui.Cols(new[] { "*", "Auto" },
            Ui.VStack(Ui.Title("التنظيف العميق"),
                Ui.Txt("ينظف الكاش والملفات المؤقتة من Windows والألعاب والمتصفحات والبرامج. أنت تختار شو ينحذف – وكل ملف محذوف ينسجل.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 0)),
            Ui.Card(Ui.VStack(Ui.Txt("المساحة القابلة للتحرير", 12, "TextDim"), bigTotal), 16).M(16, 0, 0, 0));
        var actions = Ui.Cols(new[] { "Auto", "Auto", "Auto", "*", "Auto" }, scanBtn, cleanBtn.M(10, 0, 0, 0), showDeleted.M(10, 0, 0, 0), null, closeApps);
        var stat = Ui.Cols(new[] { "Auto", "*" }, spinner.M(0, 0, 8, 0), status);
        var top = Ui.VStack(head, actions.M(0, 18, 0, 0), stat.M(0, 14, 0, 6), progress);
        top.Margin = new Thickness(0, 0, 0, 12);
        Children.Add(top);

        var list = new StackPanel();
        foreach (var cat in Cleaner.Categories)
        {
            var items = Cleaner.Items.Where(i => i.Category == cat).ToList();
            if (items.Count == 0) continue;
            var body = new StackPanel();
            var catTotal = Ui.Txt("", 13, "Accent2", FontWeights.SemiBold);
            catTotals[cat] = catTotal;
            var catRows = new List<Row>();
            foreach (var it in items) { var r = BuildRow(it); catRows.Add(r); rows.Add(r); body.Children.Add(RowUi(r)); }
            var all = Ui.Btn("تحديد الكل", null, () =>
            {
                bool any = catRows.Any(r => r.Check.IsChecked != true);
                foreach (var r in catRows) r.Check.IsChecked = any;
            }, "BtnGhost");
            all.Padding = new Thickness(10, 4, 10, 4); all.FontSize = 11.5;
            var catHead = Ui.Cols(new[] { "*", "Auto", "Auto" }, Ui.Txt(cat, 16, "Text", FontWeights.Bold), catTotal.M(0, 0, 12, 0), all);
            var card = Ui.Card(Ui.VStack(catHead, Ui.Sep(), body), 18);
            card.Margin = new Thickness(0, 0, 0, 14);
            list.Children.Add(card);
        }
        var sv = Ui.Scroll(list);
        Grid.SetRow(sv, 1);
        Children.Add(sv);
        Ui.Stagger(list, 60, 100);
    }

    Row BuildRow(CleanItem it)
    {
        var s = Settings.Current;
        bool on = s.CleanerChecked.Contains(it.Id) || (it.DefaultOn && !s.CleanerUnchecked.Contains(it.Id));
        var r = new Row { Item = it, Size = Ui.Txt(it.NoSizeInfo ? "" : "—", 13, "TextDim", FontWeights.SemiBold) };
        r.Check = Ui.Check(null, on, v =>
        {
            s.CleanerChecked.Remove(it.Id); s.CleanerUnchecked.Remove(it.Id);
            if (v && !it.DefaultOn) s.CleanerChecked.Add(it.Id);
            if (!v && it.DefaultOn) s.CleanerUnchecked.Add(it.Id);
            Settings.Save();
            UpdateTotal();
        });
        r.Running = Ui.Badge("البرنامج مفتوح", "Warn", "#22F59E0B");
        r.Running.Visibility = Visibility.Collapsed;
        return r;
    }

    UIElement RowUi(Row r)
    {
        var it = r.Item;
        var title = Ui.HStack(0, Ui.Txt(it.Name, 13.5, "Text", FontWeights.SemiBold));
        if (it.Risk == Risk.Caution) title.Children.Add(Ui.Badge("انتبه", "Warn", "#22F59E0B"));
        if (!it.DefaultOn) title.Children.Add(Ui.Badge("اختياري", "TextDim", "#22FFFFFF"));
        title.Children.Add(r.Running);
        var desc = Ui.Txt(it.Description, 12, "TextDim", null, TextWrapping.Wrap).M(0, 3, 0, 0);
        var g = Ui.Cols(new[] { "Auto", "*", "Auto" }, r.Check, Ui.VStack(title, desc).M(4, 0, 16, 0), r.Size);
        g.Margin = new Thickness(0, 7, 0, 7);
        g.Background = Brushes.Transparent;
        g.Cursor = System.Windows.Input.Cursors.Hand;
        g.MouseLeftButtonUp += (_, e) => { if (!r.Check.IsMouseOver) r.Check.IsChecked = r.Check.IsChecked != true; };
        return g;
    }

    void UpdateTotal()
    {
        long total = rows.Where(r => r.Check.IsChecked == true).Sum(r => r.Item.Size);
        bigTotal.Text = Util.Size(total);
        foreach (var cat in catTotals.Keys)
            catTotals[cat].Text = scannedOnce ? Util.Size(rows.Where(r => r.Item.Category == cat).Sum(r => r.Item.Size)) : "";
    }

    void SetBusy(bool b, string text)
    {
        busy = b;
        scanBtn.IsEnabled = cleanBtn.IsEnabled = !b;
        progress.Visibility = b ? Visibility.Visible : Visibility.Hidden;
        spinner.Visibility = b ? Visibility.Visible : Visibility.Collapsed;
        Ui.Spin(spinner, b);
        status.Text = text;
    }

    void RefreshRunning()
    {
        foreach (var r in rows)
            r.Running.Visibility = r.Item.Processes.Length > 0 && Cleaner.AnyRunning(r.Item, out var ps) && ps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public override void OnShow()
    {
        RefreshRunning();
        if (!scannedOnce && !busy) _ = Scan();
    }

    async Task Scan()
    {
        if (busy) return;
        SetBusy(true, "جاري الفحص...");
        long before = rows.Where(r => r.Check.IsChecked == true).Sum(r => r.Item.Size);
        int i = 0;
        foreach (var r in rows)
        {
            status.Text = "فحص: " + r.Item.Name;
            await Task.Run(() => Cleaner.Scan(r.Item));
            r.Size.Text = r.Item.NoSizeInfo ? "" : Util.Size(r.Item.Size);
            r.Size.Foreground = r.Item.Size > 500L * 1024 * 1024 ? Ui.B("Warn") : r.Item.Size > 0 ? Ui.B("Text") : Ui.B("TextMute");
            progress.Value = ++i * 100.0 / rows.Count;
        }
        scannedOnce = true;
        RefreshRunning();
        long total = rows.Where(r => r.Check.IsChecked == true).Sum(r => r.Item.Size);
        UpdateTotal();
        Ui.CountTo(bigTotal, before, total, v => Util.Size((long)v));
        SetBusy(false, $"اكتمل الفحص – {rows.Sum(r => r.Item.Files):N0} ملف قابل للحذف");
    }

    async Task Clean()
    {
        if (busy) return;
        var sel = rows.Where(r => r.Check.IsChecked == true).ToList();
        if (sel.Count == 0) { Dialogs.Toast("اختر شي للتنظيف أول", "warn"); return; }
        var caution = sel.Where(r => r.Item.Risk == Risk.Caution).Select(r => "• " + r.Item.Name).ToList();
        string msg = $"سيتم تنظيف {sel.Count} عنصر.\nالملفات المستخدمة حالياً يتم تخطيها تلقائياً بدون أي ضرر.";
        if (caution.Count > 0) msg += "\n\nعناصر تحتاج انتباه:\n" + string.Join("\n", caution);
        if (!await Dialogs.Confirm("تأكيد التنظيف", msg, "نظّف الآن")) return;

        SetBusy(true, "جاري التنظيف...");
        bool kill = closeApps.IsChecked == true;
        long freed = 0; int skipped = 0, i = 0;
        var restart = new List<string>();
        using (var w = ProcessManager.DeletedListWriter())
        {
            w.WriteLine($"===== {DateTime.Now} =====");
            foreach (var r in sel)
            {
                status.Text = "تنظيف: " + r.Item.Name;
                await Task.Run(() =>
                {
                    if (kill && Cleaner.AnyRunning(r.Item, out var procs))
                    {
                        foreach (var p in procs)
                        {
                            try { var path = Native.ProcessPath(p.Id); if (path != null && !restart.Contains(path) && p.MainWindowHandle != IntPtr.Zero) restart.Add(path); p.Kill(true); } catch { }
                        }
                        Thread.Sleep(1000);
                    }
                    if (!r.Item.Scanned || r.Item.CustomScan != null) Cleaner.Scan(r.Item);
                    Cleaner.Clean(r.Item, w);
                });
                freed += r.Item.Freed; skipped += r.Item.Skipped;
                r.Size.Text = r.Item.NoSizeInfo ? "✔" : Util.Size(r.Item.Size);
                r.Size.Foreground = Ui.B("Good");
                progress.Value = ++i * 100.0 / sel.Count;
            }
        }
        foreach (var p in restart) { try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); } catch { } }
        UpdateTotal();
        RefreshRunning();
        SetBusy(false, $"✔ تم تحرير {Util.Size(freed)}" + (skipped > 0 ? $" – تم تخطي {skipped:N0} ملف قيد الاستخدام" : ""));
        Dialogs.Toast($"تم التنظيف! حررت {Util.Size(freed)} 🎉");
    }
}
