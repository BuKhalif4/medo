using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class StartupPage : PageBase
{
    readonly StackPanel list = new();
    readonly TextBlock summary = Ui.Txt("", 13, "TextDim");

    public StartupPage()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var head = Ui.VStack(
            Ui.Title("برامج بدء التشغيل"),
            Ui.Txt("البرامج اللي تشتغل لحالها مع Windows تبطئ الإقلاع وتاكل رام. عطّل اللي ما تحتاجه (نفس طريقة Task Manager – تقدر ترجعه بأي وقت).", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 14),
            Ui.Cols(new[] { "Auto", "*", "Auto" }, Ui.Btn("تحديث", "", Load, "BtnGhost"), null, summary).M(0, 0, 0, 14));
        Children.Add(head);
        var sv = Ui.Scroll(list);
        Grid.SetRow(sv, 1);
        Children.Add(sv);
    }

    public override void OnShow() => Load();

    async void Load()
    {
        var entries = await Task.Run(StartupManager.List);
        list.Children.Clear();
        foreach (var e in entries.OrderByDescending(e => e.Enabled).ThenBy(e => e.Name)) list.Children.Add(Row(e));
        if (entries.Count == 0) list.Children.Add(Ui.Txt("ما في برامج بدء تشغيل 👌", 14, "TextDim"));
        summary.Text = $"مفعّل {entries.Count(e => e.Enabled)} من {entries.Count}";
        Ui.Stagger(list, 30);
    }

    UIElement Row(StartupEntry e)
    {
        var icon = new Image { Width = 28, Height = 28, Source = ProcessesPage.GetIcon(e.Path != null && File.Exists(e.Path) ? e.Path : null) };
        var title = Ui.Txt(e.Name, 13.5, "Text", FontWeights.SemiBold);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        var sub = Ui.Txt($"{(string.IsNullOrWhiteSpace(e.Publisher) ? "ناشر غير معروف" : e.Publisher)}  •  {e.Source}", 11.5, "TextMute");
        var cmd = Ui.Txt(e.Command, 11, "TextMute");
        cmd.TextTrimming = TextTrimming.CharacterEllipsis;
        cmd.FlowDirection = FlowDirection.LeftToRight;
        cmd.HorizontalAlignment = HorizontalAlignment.Right;
        cmd.ToolTip = e.Command;
        var sw = Ui.Switch(e.Enabled);
        sw.ToolTip = "تشغيل مع Windows";
        sw.Checked += (_, _) => Set(e, true);
        sw.Unchecked += (_, _) => Set(e, false);
        var open = new Button { Style = Ui.S("BtnGhost"), Content = Ui.Icon("", 13), Padding = new Thickness(9, 6, 9, 6), ToolTip = "فتح مكان الملف" };
        ((TextBlock)open.Content).ClearValue(TextBlock.ForegroundProperty);
        open.Click += (_, _) => { if (e.Path != null) Util.OpenFolder(e.Path); };
        var del = new Button { Style = Ui.S("BtnDanger"), Content = Ui.Icon("", 13), Padding = new Thickness(9, 6, 9, 6), ToolTip = "حذف من بدء التشغيل نهائياً (يتم حفظ نسخة احتياطية)" };
        ((TextBlock)del.Content).ClearValue(TextBlock.ForegroundProperty);
        Border root = null;
        del.Click += async (_, _) =>
        {
            if (!await Dialogs.Confirm("حذف من بدء التشغيل", $"حذف «{e.Name}» من بدء التشغيل؟\nالبرنامج نفسه لن يُحذف. ننصح بالتعطيل بدل الحذف.", "حذف", true)) return;
            try { StartupManager.Remove(e); list.Children.Remove(root); Dialogs.Toast("تم الحذف"); } catch (Exception ex) { Dialogs.Toast(ex.Message, "err"); }
        };
        var g = Ui.Cols(new[] { "44", "*", "Auto" }, icon, Ui.VStack(title, sub, cmd.M(0, 2, 0, 0)).M(0, 0, 16, 0), Ui.HStack(8, open, del, sw.M(8, 0, 0, 0)));
        root = new Border
        {
            Child = g, Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 8), CornerRadius = new CornerRadius(12),
            Background = Ui.B("Surface"), BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(1),
            Opacity = e.Enabled ? 1 : 0.6,
        };
        sw.Checked += (_, _) => root.Opacity = 1;
        sw.Unchecked += (_, _) => root.Opacity = 0.6;
        return root;
    }

    void Set(StartupEntry e, bool on)
    {
        try { StartupManager.SetEnabled(e, on); Dialogs.Toast(on ? $"{e.Name} رح يشتغل مع Windows" : $"تم تعطيل {e.Name} من بدء التشغيل"); }
        catch (Exception ex) { Dialogs.Toast(ex.Message, "err"); }
    }
}
