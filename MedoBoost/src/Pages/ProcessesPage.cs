using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class ProcessesPage : PageBase
{
    class RowUi
    {
        public Border Root;
        public Image Icon;
        public TextBlock Title, Sub, Ram, Cpu, Gpu;
        public ProgressBar RamBar;
        public Button CacheBtn;
        public AppGroup G;
    }

    readonly StackPanel list = new();
    readonly Dictionary<string, RowUi> rows = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly TextBox search = new() { Style = Ui.S("Input"), Width = 240 };
    readonly TextBlock ramSummary = Ui.Txt("", 13, "TextDim").Ltr();
    readonly ProgressBar ramBar = Ui.Progress();
    readonly TextBlock countText = Ui.Txt("", 12, "TextMute");
    bool showSystem, refreshing;
    string sortBy = "ram";
    static readonly Dictionary<string, ImageSource> iconCache = new(StringComparer.OrdinalIgnoreCase);

    public ProcessesPage()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var freeAll = Ui.Btn("تحرير الرام للكل", "", async () =>
        {
            var f = await Task.Run(() => Memory.CleanAll());
            Dialogs.Toast($"تم تحرير {Util.Size(f)} من الرام");
            await Refresh();
        });
        var boost = Ui.Btn("Game Boost", "", async () => await GameBoost.Run(), "BtnGhost");
        var sysToggle = Ui.Check(Ui.Txt("إظهار عمليات النظام", 12.5, "TextDim"), false, v => { showSystem = v; _ = Refresh(); });
        search.TextChanged += (_, _) => ApplyFilter();
        var searchBox = new Grid();
        searchBox.Children.Add(search);
        var ph = Ui.Txt("🔍  بحث عن برنامج...", 12.5, "TextMute");
        ph.IsHitTestVisible = false; ph.Margin = new Thickness(12, 0, 12, 0);
        search.TextChanged += (_, _) => ph.Visibility = string.IsNullOrEmpty(search.Text) ? Visibility.Visible : Visibility.Collapsed;
        searchBox.Children.Add(ph);

        var head = Ui.VStack(
            Ui.Title("البرامج والرامات"),
            Ui.Txt("شوف كل برنامج شغال وكم ياكل رام ومعالج وكرت. تقدر تخفف رامه، تمسح كاشه (ديسكورد، المتصفحات، ستيم...) أو تسكره. كليك يمين لخيارات أكثر.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 14),
            Ui.Card(Ui.VStack(Ui.Cols(new[] { "*", "Auto" }, Ui.Txt("استهلاك الرام", 13, "Text", FontWeights.SemiBold), ramSummary), ramBar.M(0, 8, 0, 0)), 14),
            Ui.Cols(new[] { "Auto", "Auto", "*", "Auto", "Auto" }, freeAll, boost.M(10, 0, 0, 0), null, sysToggle.M(0, 0, 14, 0), searchBox).M(0, 14, 0, 12));
        Children.Add(head);

        // column header (clickable sort)
        TextBlock H(string t, string key)
        {
            var tb = Ui.Txt(t, 12, "TextMute", FontWeights.SemiBold);
            tb.Cursor = System.Windows.Input.Cursors.Hand;
            tb.MouseLeftButtonUp += (_, _) => { sortBy = key; _ = Refresh(); };
            return tb;
        }
        var colHead = Ui.Cols(new[] { "44", "*", "140", "60", "60", "200" }, null, H("البرنامج", "name"), H("الرام ▾", "ram"), H("CPU", "cpu"), H("GPU", "gpu"), Ui.Txt("إجراءات", 12, "TextMute", FontWeights.SemiBold));
        colHead.Margin = new Thickness(14, 0, 14, 6);
        Children.Add(Ui.VStack(colHead, countText.M(14, 0, 0, 6)));
        Children[^1].SetValue(Grid.RowProperty, 1);

        var sv = Ui.Scroll(list);
        Grid.SetRow(sv, 2);
        Children.Add(sv);
        timer.Tick += async (_, _) => await Refresh();
    }

    public override void OnShow() { _ = Refresh(); timer.Start(); }
    public override void OnHide() => timer.Stop();

    async Task Refresh()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            bool sys = showSystem;
            var groups = await Task.Run(() => ProcessManager.Instance.Snapshot(sys));
            var hw = HardwareMonitor.Instance.Last;
            if (hw.RamTotal > 0)
            {
                ramBar.Value = hw.RamLoad;
                ramSummary.Text = $"{Util.Size((long)(hw.RamTotal - hw.RamAvail))} / {Util.Size((long)hw.RamTotal)}  ({hw.RamLoad:0}%)";
            }
            groups = sortBy switch
            {
                "cpu" => groups.OrderByDescending(g => g.Cpu).ToList(),
                "gpu" => groups.OrderByDescending(g => g.Gpu).ToList(),
                "name" => groups.OrderBy(g => g.Title).ToList(),
                _ => groups.OrderByDescending(g => g.Ram).ToList(),
            };
            long maxRam = groups.Count > 0 ? Math.Max(1, groups.Max(g => g.Ram)) : 1;
            var alive = new HashSet<string>();
            int idx = 0;
            foreach (var g in groups)
            {
                alive.Add(g.Key);
                if (!rows.TryGetValue(g.Key, out var r)) { r = Build(g); rows[g.Key] = r; Ui.FadeIn(r.Root, 0, 8, 250); }
                r.G = g;
                r.Sub.Text = $"{g.Name}.exe" + (g.Pids.Count > 1 ? $"  ×{g.Pids.Count}" : "");
                r.Ram.Text = Util.Size(g.Ram);
                r.RamBar.Value = g.Ram * 100.0 / maxRam;
                r.Ram.Foreground = g.Ram > 1500L * 1024 * 1024 ? Ui.B("Warn") : Ui.B("Text");
                r.Cpu.Text = $"{g.Cpu:0.0}%";
                r.Cpu.Foreground = g.Cpu > 25 ? Ui.B("Warn") : Ui.B("Text");
                r.Gpu.Text = g.Gpu > 0.05 ? $"{g.Gpu:0}%" : "0%";
                int cur = list.Children.IndexOf(r.Root);
                if (cur != idx)
                {
                    if (cur >= 0) list.Children.RemoveAt(cur);
                    list.Children.Insert(Math.Min(idx, list.Children.Count), r.Root);
                }
                idx++;
            }
            foreach (var k in rows.Keys.Where(k => !alive.Contains(k)).ToList())
            {
                list.Children.Remove(rows[k].Root);
                rows.Remove(k);
            }
            countText.Text = $"{groups.Count} برنامج  •  {groups.Sum(g => g.Pids.Count)} عملية";
            ApplyFilter();
        }
        catch (Exception ex) { Log.Warn("Processes: " + ex.Message); }
        finally { refreshing = false; }
    }

    void ApplyFilter()
    {
        var q = search.Text?.Trim() ?? "";
        foreach (var r in rows.Values)
            r.Root.Visibility = q.Length == 0 || r.G.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || r.G.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    RowUi Build(AppGroup g)
    {
        var r = new RowUi { G = g };
        r.Icon = new Image { Width = 26, Height = 26, Source = GetIcon(g.Path) };
        r.Title = Ui.Txt(g.Title, 13.5, "Text", FontWeights.SemiBold);
        r.Title.TextTrimming = TextTrimming.CharacterEllipsis;
        r.Sub = Ui.Txt("", 11.5, "TextMute");
        r.Ram = Ui.Txt("", 13, "Text", FontWeights.SemiBold).Ltr();
        r.Ram.HorizontalAlignment = HorizontalAlignment.Right;
        r.RamBar = Ui.Progress(); r.RamBar.Height = 4; r.RamBar.Width = 120; r.RamBar.HorizontalAlignment = HorizontalAlignment.Left;
        r.Cpu = Ui.Txt("", 13).Ltr(); r.Gpu = Ui.Txt("", 13).Ltr();
        r.Cpu.HorizontalAlignment = r.Gpu.HorizontalAlignment = HorizontalAlignment.Right;

        var trim = SmallBtn("", null, async () =>
        {
            var f = await Task.Run(() => ProcessManager.Trim(r.G));
            Dialogs.Toast($"{r.G.Title}: تم تحرير {Util.Size(f)}");
        });
        r.CacheBtn = SmallBtn("", "الكاش", async () =>
        {
            if (!await Dialogs.Confirm($"مسح كاش {r.G.Title}", $"سيتم إغلاق {r.G.Title}، حذف الكاش ({r.G.Cache.Name})، ثم إعادة فتحه تلقائياً.\nلن يتم تسجيل خروجك أو حذف إعداداتك.", "امسح الكاش")) return;
            var f = await Task.Run(() => ProcessManager.ClearCacheAndRestart(r.G, true));
            Dialogs.Toast($"{r.G.Title}: تم حذف {Util.Size(f)} كاش");
        });
        r.CacheBtn.Visibility = g.Cache != null ? Visibility.Visible : Visibility.Hidden;
        var kill = SmallBtn("", null, async () =>
        {
            if (!await Dialogs.Confirm($"إنهاء {r.G.Title}", "أي شغل غير محفوظ في هذا البرنامج سيضيع.", "إنهاء", true)) return;
            await Task.Run(() => ProcessManager.Kill(r.G));
            await Refresh();
        }, "BtnDanger");
        trim.ToolTip = "تخفيف الرام (يفرّغ الذاكرة غير المستخدمة من هذا البرنامج)";
        r.CacheBtn.ToolTip = "مسح الكاش: يسكر البرنامج، يحذف الكاش، ويرجع يفتحه";
        kill.ToolTip = "إنهاء البرنامج";
        var actions = Ui.HStack(6, trim, r.CacheBtn, kill);

        var g2 = Ui.Cols(new[] { "44", "*", "140", "60", "60", "200" },
            r.Icon,
            Ui.VStack(r.Title, r.Sub).M(0, 0, 12, 0),
            Ui.VStack(r.Ram, r.RamBar.M(0, 4, 0, 0)),
            r.Cpu, r.Gpu, actions);
        r.Root = new Border
        {
            Child = g2, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 0, 6), CornerRadius = new CornerRadius(12),
            Background = Ui.B("Surface"), BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(1),
        };
        r.Root.MouseEnter += (_, _) => r.Root.BorderBrush = Ui.B("Accent");
        r.Root.MouseLeave += (_, _) => r.Root.BorderBrush = Ui.B("BorderB");

        // context menu: priority, open location, boost list
        var cm = new ContextMenu();
        void MI(string text, Action a) { var m = new MenuItem { Header = text }; m.Click += (_, _) => a(); cm.Items.Add(m); }
        MI("الأولوية: عالية (High)", () => ProcessManager.SetPriority(r.G, ProcessPriorityClass.High));
        MI("الأولوية: فوق العادي", () => ProcessManager.SetPriority(r.G, ProcessPriorityClass.AboveNormal));
        MI("الأولوية: عادية", () => ProcessManager.SetPriority(r.G, ProcessPriorityClass.Normal));
        MI("الأولوية: منخفضة (للبرامج اللي بالخلفية)", () => ProcessManager.SetPriority(r.G, ProcessPriorityClass.BelowNormal));
        MI("فتح مكان الملف", () => { if (r.G.Path != null) Util.OpenFolder(r.G.Path); });
        MI("إضافة/إزالة من قائمة الإغلاق في Game Boost", () =>
        {
            var l = Settings.Current.BoostKillList;
            if (l.Remove(r.G.Name)) Dialogs.Toast($"{r.G.Title} انشال من قائمة Game Boost", "info");
            else { l.Add(r.G.Name); Dialogs.Toast($"{r.G.Title} رح يتسكر تلقائياً عند Game Boost", "info"); }
            Settings.Save();
        });
        r.Root.ContextMenu = cm;
        r.Root.FlowDirection = FlowDirection.RightToLeft;
        return r;
    }

    static Button SmallBtn(string glyph, string text, Func<Task> act, string style = "BtnGhost")
    {
        var b = text == null ? new Button { Style = Ui.S(style), Content = Ui.Icon(glyph, 13) } : Ui.Btn(text, glyph, null, style);
        if (text == null) ((TextBlock)b.Content).ClearValue(TextBlock.ForegroundProperty);
        b.Padding = new Thickness(10, 5, 10, 5);
        b.FontSize = 12;
        b.Click += async (_, _) => { b.IsEnabled = false; try { await act(); } catch (Exception ex) { Dialogs.Toast(ex.Message, "err"); } b.IsEnabled = true; };
        return b;
    }

    public static ImageSource GetIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (iconCache.TryGetValue(path, out var img)) return img;
        try
        {
            var info = new Native.SHFILEINFO();
            Native.SHGetFileInfo(path, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf(info), 0x100 /*SHGFI_ICON*/ | 0x0 /*large*/);
            if (info.hIcon != IntPtr.Zero)
            {
                img = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                img.Freeze();
                Native.DestroyIcon(info.hIcon);
            }
        }
        catch { img = null; }
        iconCache[path] = img;
        return img;
    }
}
