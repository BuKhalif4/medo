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
        var tray = Ui.Check(Ui.Txt("زر الإغلاق ✕ يخفي البرنامج بجانب الساعة بدل ما يسكّره (للخروج: كليك يمين على الأيقونة ← خروج)", 12.5, "TextDim"), Settings.Current.MinimizeToTray, v => { Settings.Current.MinimizeToTray = v; Settings.Save(); });
        var thermal = Ui.Check(Ui.Txt("نبّهني لما ترتفع حرارة المعالج أو الكرت", 12.5, "TextDim"), Settings.Current.ThermalAlerts, v => { Settings.Current.ThermalAlerts = v; Settings.Save(); });
        FrameworkElement Choice(string label, string[] ids, string[] names, string current, Action<string> pick)
        {
            var wp = new WrapPanel();
            string g = Guid.NewGuid().ToString();
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                var rb = new RadioButton { Style = Ui.S("Pill"), Content = new TextBlock { Text = names[i] }, GroupName = g, IsChecked = id == current };
                rb.Checked += (_, _) => { if (id != current) pick(id); };
                wp.Children.Add(rb);
            }
            return Ui.Cols(new[] { "130", "*" }, Ui.Txt(label, 12.5, "TextDim"), wp);
        }
        var langRow = Choice("اللغة", new[] { "ar", "en" }, new[] { "العربية", "English" }, Settings.Current.Language, v => { Settings.Current.Language = v; Settings.Save(); Notify.Push("info", "اللغة", "جاري إعادة تشغيل البرنامج..."); Program.Restart(); });
        var designRow = Choice("التصميم", new[] { "prime", "classic" }, new[] { "Prime", "Classic" }, Settings.Current.Design, v => { Settings.Current.Design = v; Settings.Save(); Notify.Push("info", "التصميم", "جاري إعادة تشغيل البرنامج..."); Program.Restart(); });
        var intro = Ui.Btn("عرض شاشة الترحيب", "\uE768", () => MainWindow.Instance.ShowWelcome(), "BtnGhost");
        intro.HorizontalAlignment = HorizontalAlignment.Left;
        var opts = Ui.Card(Ui.VStack(Ui.Txt("الإعدادات", 14, "Text", FontWeights.Bold).M(0, 0, 0, 12), langRow.M(0, 0, 0, 6), designRow.M(0, 0, 0, 6),
            Ui.Txt("تغيير اللغة أو التصميم يعيد تشغيل البرنامج تلقائياً.", 11.5, "TextMute").M(0, 0, 0, 12),
            autoUpd.M(0, 0, 0, 10), tray.M(0, 0, 0, 10), thermal.M(0, 0, 0, 12), intro), 16);
        var head = Ui.VStack(Ui.Title("السجل وحول البرنامج"), about.M(0, 14, 0, 12), opts.M(0, 0, 0, 14), Ui.Txt("سجل العمليات (مباشر)", 15, "Text", FontWeights.Bold).M(0, 0, 0, 8));
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
