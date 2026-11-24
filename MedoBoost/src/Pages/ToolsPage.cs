using System.Windows;
using System.Windows.Controls;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class ToolsPage : PageBase
{
    public ToolsPage()
    {
        var root = new StackPanel();
        root.Children.Add(Ui.Title("أدوات الإصلاح"));
        root.Children.Add(Ui.Txt("أدوات Windows الرسمية لإصلاح ملفات النظام والقرص والشبكة – بضغطة زر. الأدوات الطويلة تفتح بنافذة لتشوف التقدم.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 18));

        var grid = new WrapPanel();
        void Tool(string glyph, string title, string desc, string btn, Func<Task> act, bool confirm = false)
        {
            var b = Ui.Btn(btn, null, null, "BtnGhost");
            b.HorizontalAlignment = HorizontalAlignment.Left;
            b.Click += async (_, _) =>
            {
                if (confirm && !await Dialogs.Confirm(title, desc, btn)) return;
                b.IsEnabled = false;
                try { await act(); } catch (Exception ex) { Dialogs.Toast(ex.Message, "err"); }
                b.IsEnabled = true;
            };
            var icon = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.B("AccentGrad"), Child = Ui.Icon(glyph, 18) };
            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var top = Ui.VStack(icon.M(0, 0, 0, 12), Ui.Txt(title, 14, "Text", FontWeights.SemiBold), Ui.Txt(desc, 12, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 12));
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            content.Children.Add(top);
            Grid.SetRow(b, 1);
            content.Children.Add(b);
            var card = Ui.Card(content, 18);
            card.Width = 300; card.Height = 230; card.Margin = new Thickness(0, 0, 14, 14);
            grid.Children.Add(card);
        }
        Task Visible(string cmd) { Util.RunVisible(cmd); return Task.CompletedTask; }

        Tool("", "فحص ملفات النظام (SFC)", "يفحص ملفات Windows ويصلح التالف منها. يأخذ 10-20 دقيقة.", "ابدأ الفحص", () => Visible("sfc /scannow"));
        Tool("", "إصلاح صورة Windows (DISM)", "يصلح مخزن مكونات Windows من سيرفرات مايكروسوفت. شغله قبل SFC إذا SFC ما قدر يصلح.", "ابدأ الإصلاح", () => Visible("DISM /Online /Cleanup-Image /RestoreHealth"));
        Tool("", "فحص القرص (CHKDSK)", "يفحص أخطاء نظام الملفات على القرص C بدون إعادة تشغيل.", "افحص القرص", () => Visible($"chkdsk {Util.SystemDrive.TrimEnd('\\')} /scan"));
        Tool("", "تحسين SSD (TRIM)", "يرسل أمر TRIM لكل أقراص SSD – يحافظ على سرعتها. (للقرص العادي HDD يسوي إلغاء تجزئة).", "حسّن الأقراص", () => Visible("powershell -NoProfile -Command \"Get-Volume | Where-Object DriveLetter | ForEach-Object { Optimize-Volume -DriveLetter $_.DriveLetter -Verbose }\""));
        Tool("", "إعادة ضبط الشبكة", "يصلح مشاكل النت والبنق (Winsock + TCP/IP + DNS). يحتاج إعادة تشغيل بعدها.", "أعد ضبط الشبكة", async () =>
        {
            await Task.Run(() =>
            {
                Util.Run("netsh", "winsock reset"); Util.Run("netsh", "int ip reset"); Util.Run("ipconfig", "/flushdns");
                Util.Run("ipconfig", "/release"); Util.Run("ipconfig", "/renew", 60000);
            });
            Log.Ok("تم إعادة ضبط الشبكة");
            Dialogs.Toast("تم إعادة ضبط الشبكة – أعد تشغيل الجهاز");
        }, true);
        Tool("", "إعادة تشغيل Explorer", "إذا شريط المهام أو سطح المكتب علق أو صار بطيء.", "أعد التشغيل", async () =>
        {
            await Task.Run(() => { Util.Run("taskkill", "/f /im explorer.exe"); Thread.Sleep(800); });
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        });
        Tool("", "نقطة استعادة النظام", "تنشئ نقطة استعادة ترجع لها إذا صار أي شي.", "أنشئ نقطة", async () =>
        {
            Dialogs.Toast("جاري الإنشاء...", "info");
            var (ok, msg) = await Task.Run(Tweaks.CreateRestorePoint);
            Dialogs.Toast(ok ? "تم إنشاء نقطة الاستعادة ✔" : "فشل: " + msg, ok ? "ok" : "err");
        });
        Tool("", "استعادة النظام", "يفتح أداة Windows للرجوع لنقطة استعادة سابقة.", "افتح", () => { Util.Run("rstrui.exe", "", 1000); return Task.CompletedTask; });
        Tool("", "فحص الرامات", "أداة Windows Memory Diagnostic – تفحص الرام بعد إعادة التشغيل.", "افتح", () => { Util.OpenUrl("mdsched.exe"); return Task.CompletedTask; });
        Tool("", "تقرير البطارية (لابتوب)", "يحفظ تقرير صحة البطارية على سطح المكتب.", "أنشئ التقرير", async () =>
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "battery-report.html");
            var r = await Task.Run(() => Util.Run("powercfg", $"/batteryreport /output \"{file}\""));
            if (File.Exists(file)) Util.OpenUrl(file); else Dialogs.Toast("ما في بطارية بهالجهاز", "info");
        });
        Tool("", "تنظيف القرص (Windows)", "أداة Disk Cleanup الرسمية من Windows (تشمل ملفات النظام).", "افتح", () => { Util.OpenUrl("cleanmgr.exe"); return Task.CompletedTask; });
        Tool("", "إدارة الأجهزة", "لفحص التعريفات والأجهزة اللي فيها مشاكل.", "افتح", () => { Util.OpenUrl("devmgmt.msc"); return Task.CompletedTask; });

        root.Children.Add(grid);
        Children.Add(Ui.Scroll(root));
        Ui.Stagger(grid, 40, 100);
    }
}
