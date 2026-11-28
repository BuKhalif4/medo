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
            var icon = new Border { Width = 40, Height = 40, CornerRadius = Ui.Cr(12), Background = Ui.B("AccentGrad"), Child = Ui.Icon(glyph, 18) };
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

        // ---- drive health (SMART-like)
        var diskList = new StackPanel();
        var diskBtn = Ui.Btn("افحص الأقراص", "\uE721", null);
        diskBtn.HorizontalAlignment = HorizontalAlignment.Left;
        diskBtn.Click += async (_, _) =>
        {
            diskBtn.IsEnabled = false; diskList.Children.Clear();
            diskList.Children.Add(Ui.Txt("جاري فحص الأقراص...", 12.5, "TextDim"));
            var disks = await Task.Run(DiskHealth.Read);
            diskList.Children.Clear();
            if (disks.Count == 0) diskList.Children.Add(Ui.Txt("ما قدرت أقرأ بيانات الأقراص (الجهاز الافتراضي أو تعريف مخصص).", 12.5, "TextMute"));
            foreach (var d in disks)
            {
                string state = d.Bad ? "خطر" : d.Warn ? "تحذير" : "سليم";
                string color = d.Bad ? "Bad" : d.Warn ? "Warn" : "Good";
                var info = new List<string>();
                if (d.Temp != null) info.Add($"الحرارة {d.Temp:0} درجة");
                if (d.Wear != null) info.Add($"استهلاك العمر {d.Wear:0} بالمئة");
                if (d.Hours != null) info.Add($"ساعات التشغيل {d.Hours:N0} ({d.Hours / 24:N0} يوم)");
                if ((d.Errors ?? 0) > 0) info.Add($"أخطاء {d.Errors:N0}");
                var name = Ui.Txt($"{d.Name}  •  {Util.Size(d.Size)}  •  {d.Media}", 13, "Text", FontWeights.SemiBold).Ltr();
                name.HorizontalAlignment = HorizontalAlignment.Right;
                var line = Ui.Txt(info.Count > 0 ? string.Join("   •   ", info) : "ما في تفاصيل إضافية من القرص", 12, "TextDim", null, TextWrapping.Wrap);
                var row = Ui.Cols(new[] { "*", "Auto" }, Ui.VStack(name, line.M(0, 3, 0, 0)), Ui.Badge(state, color, d.Bad ? "#33EF4444" : d.Warn ? "#33F59E0B" : "#3322C55E"));
                diskList.Children.Add(new Border { Child = row, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 8), CornerRadius = Ui.Cr(10), Background = Ui.B("Surface2") });
            }
            diskBtn.IsEnabled = true;
        };
        root.Children.Add(Ui.Card(Ui.VStack(
            Ui.HStack(10, Ui.Icon("\uEDA2", 18, "Accent2"), Ui.Txt("صحة الأقراص", 15, "Text", FontWeights.Bold)),
            Ui.Txt("يقرأ حالة الـ SSD/HDD (العمر المستهلك، الحرارة، الأخطاء) وينبهك قبل ما يخرب. البرنامج يفحصها لحاله مرة عند كل تشغيل.", 12, "TextDim", null, TextWrapping.Wrap).M(0, 8, 0, 12),
            diskBtn.M(0, 0, 0, 12), diskList), 20).M(0, 6, 0, 10));
        Children.Add(Ui.Scroll(root));
        Ui.Stagger(grid, 40, 100);
    }
}
