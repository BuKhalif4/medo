using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

/// <summary>Uninstall installed programs and wipe the folders + temp files they leave behind.</summary>
public class UninstallPage : PageBase
{
    readonly StackPanel list = new();
    readonly TextBox search = new() { Style = Ui.S("Input") };
    readonly TextBlock status = Ui.Txt("", 12.5, "TextDim");
    readonly Dictionary<InstalledApp, Border> rows = new();
    List<InstalledApp> apps = new();
    bool loaded;

    public UninstallPage()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        search.TextChanged += (_, _) => Filter();
        var box = new Grid();
        box.Children.Add(search);
        var ph = Ui.Txt("🔍  بحث عن برنامج...", 12.5, "TextMute"); ph.IsHitTestVisible = false; ph.Margin = new Thickness(12, 0, 12, 0);
        search.TextChanged += (_, _) => ph.Visibility = string.IsNullOrEmpty(search.Text) ? Visibility.Visible : Visibility.Collapsed;
        box.Children.Add(ph);

        var head = Ui.VStack(
            Ui.Title("إلغاء تثبيت البرامج"),
            Ui.Txt("يشيل البرنامج كامل مع الملفات والمجلدات اللي يخليها ورا والملفات المؤقتة – مش بس يشغّل المزيل العادي.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 14),
            Ui.Cols(new[] { "Auto", "*", "260" }, Ui.Btn("تحديث", "", () => _ = Load(), "BtnGhost"), status.M(14, 0, 0, 0), box));
        Children.Add(head);

        var sv = Ui.Scroll(list);
        Grid.SetRow(sv, 1);
        Children.Add(sv);
    }

    public override void OnShow() { if (!loaded) { loaded = true; _ = Load(); } }

    async Task Load()
    {
        status.Text = "جاري قراءة البرامج المثبتة...";
        list.Children.Clear(); rows.Clear();
        apps = await Task.Run(Uninstaller.List);
        foreach (var a in apps) { var r = Row(a); rows[a] = r; list.Children.Add(r); }
        status.Text = $"{apps.Count} برنامج";
        Ui.Stagger(list, 18);
    }

    void Filter()
    {
        var q = search.Text?.Trim() ?? "";
        foreach (var (a, r) in rows)
            r.Visibility = q.Length == 0 || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || a.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    Border Row(InstalledApp a)
    {
        ImageSource icon = null;
        try { if (!string.IsNullOrWhiteSpace(a.Icon) && File.Exists(a.Icon)) icon = ProcessesPage.GetIcon(a.Icon); } catch { }
        var img = new Image { Width = 30, Height = 30, Source = icon };
        var sub = new List<string>();
        if (a.Publisher.Length > 0) sub.Add(a.Publisher);
        if (a.Version.Length > 0) sub.Add("v" + a.Version);
        if (a.EstimatedSize > 0) sub.Add(Util.Size(a.EstimatedSize));
        var subTb = Ui.Txt(string.Join("  •  ", sub), 11.5, "TextMute");
        subTb.FlowDirection = FlowDirection.LeftToRight; subTb.HorizontalAlignment = HorizontalAlignment.Right;

        var del = Ui.Btn("إلغاء التثبيت", "", null, "BtnDanger");
        del.Padding = new Thickness(12, 6, 12, 6);
        Border root = null;
        del.Click += async (_, _) => await Remove(a, root);

        var g = Ui.Cols(new[] { "42", "*", "Auto" }, img, Ui.VStack(Ui.Txt(a.Name, 13.5, "Text", FontWeights.SemiBold), subTb).M(0, 0, 14, 0), del);
        root = new Border
        {
            Child = g, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 7), CornerRadius = new CornerRadius(12),
            Background = Ui.B("Surface"), BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(1),
        };
        ((TextBlock)((StackPanel)g.Children[1]).Children[0]).TextTrimming = TextTrimming.CharacterEllipsis;
        return root;
    }

    async Task Remove(InstalledApp a, Border row)
    {
        if (!await Dialogs.Confirm($"إلغاء تثبيت {a.Name}",
            "رح يشتغل مزيل البرنامج. بعد ما يخلص، رح أدوّر على المجلدات والملفات اللي خلّاها ورا وأعرضها لك قبل الحذف.", "ابدأ", true)) return;
        bool ok = await Task.Run(() => Uninstaller.RunUninstall(a, false));
        if (!ok) { Dialogs.Toast("ما قدرت أشغّل المزيل", "err"); return; }

        var leftovers = await Task.Run(() => Uninstaller.FindLeftovers(a));
        if (leftovers.Count > 0)
        {
            if (await Dialogs.Confirm("حذف البقايا",
                $"لقيت {leftovers.Count} مجلد بقايا:\n" + string.Join("\n", leftovers.Take(8)) + (leftovers.Count > 8 ? "\n..." : "") + "\n\nأحذفها نهائياً؟", "احذف البقايا", true))
            {
                long freed = await Task.Run(() => { using var w = ProcessManager.DeletedListWriter(); return Uninstaller.DeleteLeftovers(leftovers, w); });
                Dialogs.Toast($"تم حذف بقايا {a.Name} ({Util.Size(freed)})");
            }
        }
        await Task.Run(Uninstaller.CleanTemp);
        if (row != null) list.Children.Remove(row);
        apps.Remove(a);
        status.Text = $"{apps.Count} برنامج";
        Dialogs.Toast($"تمت إزالة {a.Name}");
    }
}
