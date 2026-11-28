using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

/// <summary>Defender control, Windows Update, drivers, network repair/speed, and Cloudflare WARP.</summary>
public class SecurityPage : PageBase
{
    readonly ToggleButton rtSwitch;
    readonly TextBlock defState = Ui.Txt("...", 13, "TextDim");
    readonly TextBlock sigState = Ui.Txt("", 12, "TextMute");
    readonly Border defDot;
    readonly ToggleButton warpSwitch;
    readonly TextBlock warpState = Ui.Txt("...", 12.5, "TextDim");
    bool updatingSwitch;

    public SecurityPage()
    {
        var root = new StackPanel();
        root.Children.Add(Ui.Title("الحماية والتحديثات"));
        root.Children.Add(Ui.Txt("تحكّم بـ Windows Defender، حدّث النظام والتعريفات، أصلح وسرّع الشبكة، وفعّل VPN مجاني سريع.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 18));

        // ===== Defender =====
        rtSwitch = Ui.Switch(true);
        rtSwitch.Checked += async (_, _) => await ToggleRealtime(true);
        rtSwitch.Unchecked += async (_, _) => await ToggleRealtime(false);
        defDot = new Border { Width = 12, Height = 12, CornerRadius = Ui.Cr(6), Background = Ui.B("Good"), VerticalAlignment = VerticalAlignment.Center };

        var scanQuick = Ui.Btn("فحص سريع", "", () => { Security.QuickScan(); Dialogs.Toast("بدأ الفحص السريع – شوف Windows Security"); Security.OpenSecurityCenter(); }, "BtnGhost");
        var scanFull = Ui.Btn("فحص كامل", "", async () =>
        {
            if (await Dialogs.Confirm("فحص كامل", "الفحص الكامل يفحص كل الجهاز وممكن ياخذ ساعة أو أكثر. يكمل بالخلفية.", "ابدأ")) { Security.FullScan(); Dialogs.Toast("بدأ الفحص الكامل"); Security.OpenSecurityCenter(); }
        }, "BtnGhost");
        var scanFolder = Ui.Btn("فحص ملف/مجلد", "", () =>
        {
            var d = new OpenFileDialog { Title = "اختر ملف للفحص (أو أي ملف داخل المجلد)", CheckFileExists = false, FileName = "اختر" };
            if (d.ShowDialog() == true) { var p = Path.GetDirectoryName(d.FileName); Security.ScanPath(p); Dialogs.Toast("جاري فحص: " + p); Security.OpenSecurityCenter(); }
        }, "BtnGhost");
        var updateDefs = Ui.Btn("تحديث تعريفات الحماية", "", async () =>
        {
            Dialogs.Toast("جاري تحديث تعريفات الحماية...", "info");
            await Task.Run(Security.UpdateSignatures);
            await Refresh();
            Dialogs.Toast("تم تحديث تعريفات الحماية ✔");
        }, "BtnGhost");
        var openSec = Ui.Btn("فتح مركز الحماية", "", Security.OpenSecurityCenter, "BtnGhost");
        var addExcl = Ui.Btn("استثناء مجلد (لألعاب/مودات)", "", () =>
        {
            var d = new OpenFolderDialog { Title = "اختر مجلد لاستثنائه من الحماية" };
            if (d.ShowDialog() == true) { Security.AddExclusion(d.FolderName); Dialogs.Toast("تم استثناء المجلد من الفحص"); }
        }, "BtnGhost");

        var defButtons = new WrapPanel();
        foreach (var b in new[] { scanQuick, scanFull, scanFolder, updateDefs, addExcl, openSec }) { b.Margin = new Thickness(0, 0, 8, 8); defButtons.Children.Add(b); }

        var defCard = Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "Auto", "*", "Auto" },
                Ui.HStack(10, Ui.Icon("", 22, "Good"), Ui.VStack(Ui.Txt("Windows Defender", 15, "Text", FontWeights.Bold), Ui.HStack(7, defDot, defState))),
                null,
                Ui.VStack(Ui.Txt("الحماية اللحظية", 11.5, "TextDim"), rtSwitch.M(0, 4, 0, 0))),
            sigState.M(0, 8, 0, 0),
            Ui.Sep(),
            defButtons,
            Ui.Txt("ملاحظة: إيقاف الحماية نهائياً يحتاج تطفي Tamper Protection من إعدادات ويندوز أولاً (البرنامج رح يفتحها لك).", 11.5, "TextMute", null, TextWrapping.Wrap)), 20);
        root.Children.Add(defCard);

        // ===== Windows Update =====
        var updCard = Section("", "تحديثات Windows", new[]
        {
            Ui.Btn("فحص عن تحديثات", "", SystemActions.CheckForUpdates, "BtnGhost"),
            Ui.Btn("إيقاف التحديثات 7 أيام", "", () => { SystemActions.PauseUpdates(7); Dialogs.Toast("تم إيقاف التحديثات 7 أيام"); }, "BtnGhost"),
            Ui.Btn("استئناف التحديثات", "", () => { SystemActions.ResumeUpdates(); Dialogs.Toast("تم استئناف التحديثات"); }, "BtnGhost"),
            Ui.Btn("فتح إعدادات التحديث", "", SystemActions.OpenWindowsUpdate, "BtnGhost"),
        }, "حدّث ويندوز، أو أوقف التحديثات مؤقتاً عشان ما تنزل وأنت تلعب.");
        root.Children.Add(updCard.M(0, 16, 0, 0));

        // ===== Drivers =====
        var (gpuLabel, gpuUrl) = SystemActions.GpuDriverSource();
        var drvCard = Section("", "التعريفات", new[]
        {
            Ui.Btn(gpuLabel, "", () => Util.OpenUrl(gpuUrl), "BtnGhost"),
            Ui.Btn("تثبيت تعريفات ناقصة", "", async () => { Dialogs.Toast("جاري الفحص...", "info"); await Task.Run(SystemActions.ScanHardwareChanges); Dialogs.Toast("تم فحص الأجهزة"); }, "BtnGhost"),
            Ui.Btn("تعريفات اختيارية (Windows)", "", SystemActions.OpenOptionalDrivers, "BtnGhost"),
            Ui.Btn("إدارة الأجهزة", "", SystemActions.OpenDeviceManager, "BtnGhost"),
        }, "تعريف كرت الشاشة دايماً من الموقع الرسمي (ما نحمّل تعريفات مجهولة). ويندوز يركّب باقي التعريفات.");
        root.Children.Add(drvCard.M(0, 16, 0, 0));

        // ===== Network =====
        var dnsWrap = new WrapPanel();
        foreach (var (name, dns) in SystemActions.DnsChoices)
        {
            var b = Ui.Btn(name, "", () => { Task.Run(() => SystemActions.SetDns(dns)); Dialogs.Toast($"تم ضبط DNS → {name}"); }, "BtnGhost");
            b.Margin = new Thickness(0, 0, 8, 8); dnsWrap.Children.Add(b);
        }
        var netActions = new WrapPanel();
        foreach (var b in new[]
        {
            Ui.Btn("إصلاح الشبكة كامل", "", async () =>
            {
                if (!await Dialogs.Confirm("إصلاح الشبكة", "يعيد ضبط Winsock و TCP/IP و DNS والجدار الناري. يحل أغلب مشاكل النت. يحتاج إعادة تشغيل بعده.", "أصلح")) return;
                Dialogs.Toast("جاري الإصلاح...", "info");
                await Task.Run(SystemActions.RepairNetwork);
                Dialogs.Toast("تم إصلاح الشبكة – أعد تشغيل الجهاز");
            }, "BtnGhost"),
            Ui.Btn("تسريع الشبكة", "", async () => { await Task.Run(SystemActions.SpeedUpNetwork); Dialogs.Toast("تم تطبيق تحسينات السرعة"); }, "BtnGhost"),
            Ui.Btn("إرجاع DNS تلقائي", "", () => { Task.Run(SystemActions.ResetDns); Dialogs.Toast("رجع DNS تلقائي"); }, "BtnGhost"),
        }) { b.Margin = new Thickness(0, 0, 8, 8); netActions.Children.Add(b); }

        var netCard = Ui.Card(Ui.VStack(
            Ui.HStack(10, Ui.Icon("", 18, "Accent2"), Ui.Txt("الشبكة والإنترنت", 15, "Text", FontWeights.Bold)),
            Ui.Txt("غيّر DNS لأسرع وأأمن، أصلح مشاكل النت، أو طبّق تحسينات السرعة. (تويكات البنق للألعاب في صفحة «تسريع وتويكات»).", 12, "TextDim", null, TextWrapping.Wrap).M(0, 8, 0, 10),
            Ui.Txt("سيرفر DNS أسرع:", 12.5, "TextDim", FontWeights.SemiBold).M(0, 0, 0, 8), dnsWrap,
            Ui.Sep(), netActions), 20);
        root.Children.Add(netCard.M(0, 16, 0, 0));

        // ===== VPN (Cloudflare WARP) =====
        warpSwitch = Ui.Switch(false);
        warpSwitch.Checked += async (_, _) => await ToggleWarp(true);
        warpSwitch.Unchecked += async (_, _) => await ToggleWarp(false);
        var installWarp = Ui.Btn("تثبيت WARP", "", async () =>
        {
            Dialogs.Toast("جاري تثبيت Cloudflare WARP... (ممكن ياخذ دقيقة)", "info");
            await Task.Run(SystemActions.InstallWarp);
            await Refresh();
            Dialogs.Toast(SystemActions.WarpInstalled ? "تم تثبيت WARP ✔ فعّله من المفتاح" : "افتح الموقع ونزّله يدوي", SystemActions.WarpInstalled ? "ok" : "warn");
        }, "BtnGhost");
        var vpnCard = Ui.Card(Ui.VStack(
            Ui.Cols(new[] { "Auto", "*", "Auto" },
                Ui.HStack(10, Ui.Icon("", 20, "Accent2"), Ui.VStack(Ui.Txt("VPN مجاني وسريع (Cloudflare WARP)", 15, "Text", FontWeights.Bold), warpState)),
                null, warpSwitch),
            Ui.Txt("WARP من Cloudflare، أكبر وأسرع شبكة بالعالم. يخفي عنوانك ويحسّن الاتصال، مجاني بالكامل. (VPN حقيقي يحتاج سيرفرات، فاستخدمنا شبكة Cloudflare بدل ما نوعدك بشي ما نقدر نوفّره).", 12, "TextDim", null, TextWrapping.Wrap).M(0, 8, 0, 10),
            installWarp), 20);
        root.Children.Add(vpnCard.M(0, 16, 0, 10));

        Children.Add(Ui.Scroll(root));
        Ui.Stagger(root, 60);
    }

    static Border Section(string glyph, string title, Button[] buttons, string desc)
    {
        var wrap = new WrapPanel();
        foreach (var b in buttons) { b.Margin = new Thickness(0, 0, 8, 8); wrap.Children.Add(b); }
        return Ui.Card(Ui.VStack(
            Ui.HStack(10, Ui.Icon(glyph, 18, "Accent2"), Ui.Txt(title, 15, "Text", FontWeights.Bold)),
            Ui.Txt(desc, 12, "TextDim", null, TextWrapping.Wrap).M(0, 8, 0, 10),
            wrap), 20);
    }

    public override void OnShow() => _ = Refresh();

    async Task Refresh()
    {
        var st = await Task.Run(Security.Status);
        updatingSwitch = true;
        rtSwitch.IsChecked = st.RealtimeOn;
        updatingSwitch = false;
        if (!st.Available) { defState.Text = "غير متوفر (ربما عندك مضاد فيروسات آخر)"; defDot.Background = Ui.B("TextMute"); }
        else
        {
            defState.Text = st.RealtimeOn ? "الحماية شغّالة" : "الحماية متوقفة";
            defDot.Background = Ui.B(st.RealtimeOn ? "Good" : "Bad");
            sigState.Text = (st.Signature.Length > 0 ? $"إصدار التعريفات: {st.Signature}" : "") + (st.LastScan != null ? $"   •   آخر فحص: {st.LastScan:yyyy-MM-dd HH:mm}" : "");
            sigState.FlowDirection = FlowDirection.LeftToRight;
        }
        var warp = await Task.Run(() => (SystemActions.WarpInstalled, SystemActions.WarpConnected()));
        updatingSwitch = true;
        warpSwitch.IsChecked = warp.Item2;
        updatingSwitch = false;
        warpState.Text = !warp.Item1 ? "غير مثبت – اضغط «تثبيت WARP»" : warp.Item2 ? "متصل ✔" : "مثبت – مفصول";
    }

    async Task ToggleRealtime(bool on)
    {
        if (updatingSwitch) return;
        rtSwitch.IsEnabled = false;
        try
        {
            if (!on && !await Dialogs.Confirm("إيقاف الحماية", "إيقاف الحماية اللحظية يخلي جهازك أقل أماناً. فعّلها مرة ثانية بعد ما تخلص.\nمتأكد؟", "أوقف الحماية", true))
            { updatingSwitch = true; rtSwitch.IsChecked = true; updatingSwitch = false; return; }
            var res = await Task.Run(() => Security.SetRealtime(on));
            if (res == Security.ToggleResult.TamperBlocked)
            {
                updatingSwitch = true; rtSwitch.IsChecked = !on; updatingSwitch = false;
                if (await Dialogs.Confirm("Tamper Protection مفعّل",
                        "ويندوز يحمي Defender بميزة Tamper Protection، فما نقدر نطفيه إلا إذا طفيتها إنت أول.\nرح أفتحلك الصفحة: طفّي «Tamper Protection» وارجع جرّب.", "افتح الإعدادات"))
                    Security.OpenTamperSettings();
            }
            else if (res == Security.ToggleResult.Failed)
            {
                updatingSwitch = true; rtSwitch.IsChecked = !on; updatingSwitch = false;
                Dialogs.Toast("ما قدرت أغيّر الحماية", "err");
            }
            else Dialogs.Toast(on ? "تم تفعيل الحماية ✔" : "تم إيقاف الحماية اللحظية");
        }
        finally { rtSwitch.IsEnabled = true; await Refresh(); }
    }

    async Task ToggleWarp(bool on)
    {
        if (updatingSwitch) return;
        warpSwitch.IsEnabled = false;
        try
        {
            if (!SystemActions.WarpInstalled)
            {
                updatingSwitch = true; warpSwitch.IsChecked = false; updatingSwitch = false;
                Dialogs.Toast("ثبّت WARP أول", "warn");
                return;
            }
            await Task.Run(() => SystemActions.WarpConnect(on));
            Dialogs.Toast(on ? "VPN متصل ✔" : "VPN مفصول");
        }
        finally { warpSwitch.IsEnabled = true; await Refresh(); }
    }
}
