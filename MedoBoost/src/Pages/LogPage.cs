using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class LogPage : PageBase
{
    readonly StackPanel list = new();
    readonly ScrollViewer sv;

    public LogPage()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var about = Ui.Card(Ui.VStack(
            Ui.HStack(14, new Image { Source = Ui.Logo(), Width = 56, Height = 56 },
                Ui.VStack(Ui.GradText("KHA Boost", 24), Ui.Txt($"نسخة {Updater.CurrentText.TrimStart('v')} – برنامج تسريع وتنظيف ومراقبة، مصنوع خصيصاً لك", 12.5, "TextDim"))),
            Ui.Txt("• البرنامج ما يرسل أي بيانات. الاتصال الوحيد: يتحقق من التحديثات على صفحة المشروع في GitHub (BuKhalif4/medo).\n• كل ملف ينحذف ينكتب في «deleted-files» وكل تعديل ينحفظ أصله في backup.json عشان ترجعه.\n• ما يلمس ملفاتك الشخصية أو كلمات السر أو الألعاب أو ملفات الحفظ.\n• الحساسات من مكتبة LibreHardwareMonitor المفتوحة المصدر، وعداد FPS من أحداث Windows (ETW) بدون حقن.", 12.5, "TextDim", null, TextWrapping.Wrap).M(0, 14, 0, 14),
            Ui.HStack(10,
                Ui.Btn("فتح مجلد السجلات", "", () => Util.OpenFolder(Path.Combine(Util.DataDir, "logs")), "BtnGhost"),
                Ui.Btn("تحقق من التحديثات", "\uE895", () => _ = UpdateUi.CheckAndPrompt(false)),
                Ui.Btn("فتح مجلد البيانات والنسخ الاحتياطية", "", () => Util.OpenFolder(Util.DataDir), "BtnGhost"))));
        var autoUpd = Ui.Check(Ui.Txt("تحقق من التحديثات تلقائياً عند التشغيل", 12.5, "TextDim"), Settings.Current.AutoUpdateCheck, v => { Settings.Current.AutoUpdateCheck = v; Settings.Save(); });
        var head = Ui.VStack(Ui.Title("السجل وحول البرنامج"), about.M(0, 14, 0, 8), autoUpd.M(0, 0, 0, 14), Ui.Txt("سجل العمليات (مباشر)", 15, "Text", FontWeights.Bold).M(0, 0, 0, 8));
        Children.Add(head);

        sv = Ui.Scroll(list);
        var card = Ui.Card(sv, 14);
        Grid.SetRow(card, 1);
        Children.Add(card);

        foreach (var e in Log.All.TakeLast(400)) Add(e);
        Log.Added += e => Dispatcher.BeginInvoke(() => { Add(e); sv.ScrollToEnd(); });
    }

    void Add(Log.Entry e)
    {
        string color = e.Level switch { "OK" => "Good", "WARN" => "Warn", "ERR" => "Bad", _ => "TextDim" };
        var line = Ui.Cols(new[] { "70", "50", "*" },
            Ui.Txt(e.Time.ToString("HH:mm:ss"), 11.5, "TextMute"),
            Ui.Txt(e.Level, 11.5, color, FontWeights.SemiBold),
            Ui.Txt(e.Text, 12, "Text", null, TextWrapping.Wrap));
        line.Margin = new Thickness(0, 2, 0, 2);
        list.Children.Add(line);
        if (list.Children.Count > 600) list.Children.RemoveAt(0);
    }

    public override void OnShow() => sv.ScrollToEnd();
}
