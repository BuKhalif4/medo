using System.Text;

namespace MedoBoost.Core;

public class BloatApp
{
    public string Id;            // Appx package name (wildcards allowed: *TikTok*)
    public string Name;          // shown to user
    public string Group;
    public string Note = "";
    public bool DefaultOn = true;
    public bool Installed;
}

/// <summary>
/// Removes Windows preinstalled apps (Appx) for all users and stops them from coming back for new users.
/// Never touches: Microsoft Store, App Installer (winget), Terminal, Calculator, Notepad, Paint, Photos,
/// runtime frameworks (VCLibs, UI.Xaml, WindowsAppRuntime), WebView2, or anything WSL/Docker/emulators need.
/// </summary>
public static class Debloat
{
    public static readonly string[] Groups = { "تطبيقات مايكروسوفت الزائدة", "Xbox", "تطبيقات إعلانية مثبتة مسبقاً", "اختياري" };

    public static List<BloatApp> Apps { get; } = Build();

    static List<BloatApp> Build()
    {
        string g0 = Groups[0], g1 = Groups[1], g2 = Groups[2], g3 = Groups[3];
        return new()
        {
            new() { Id = "Microsoft.BingNews", Name = "الأخبار (Microsoft News)", Group = g0 },
            new() { Id = "Microsoft.BingWeather", Name = "الطقس (Weather)", Group = g0 },
            new() { Id = "Microsoft.BingSearch", Name = "Bing Search", Group = g0 },
            new() { Id = "Microsoft.549981C3F5F10", Name = "Cortana", Group = g0 },
            new() { Id = "Microsoft.Copilot", Name = "Copilot (التطبيق)", Group = g0 },
            new() { Id = "Microsoft.Windows.Ai.Copilot.Provider", Name = "Copilot Provider", Group = g0 },
            new() { Id = "MSTeams", Name = "Microsoft Teams (الشخصي)", Group = g0 },
            new() { Id = "MicrosoftTeams", Name = "Teams Chat (القديم)", Group = g0 },
            new() { Id = "Clipchamp.Clipchamp", Name = "Clipchamp (محرر فيديو)", Group = g0 },
            new() { Id = "Microsoft.GetHelp", Name = "Get Help", Group = g0 },
            new() { Id = "Microsoft.Getstarted", Name = "Tips / البدء", Group = g0 },
            new() { Id = "Microsoft.MicrosoftOfficeHub", Name = "Microsoft 365 (Office Hub)", Group = g0 },
            new() { Id = "Microsoft.MicrosoftSolitaireCollection", Name = "Solitaire Collection", Group = g0 },
            new() { Id = "Microsoft.People", Name = "People", Group = g0 },
            new() { Id = "Microsoft.PowerAutomateDesktop", Name = "Power Automate", Group = g0 },
            new() { Id = "Microsoft.Todos", Name = "Microsoft To Do", Group = g0 },
            new() { Id = "Microsoft.WindowsFeedbackHub", Name = "Feedback Hub", Group = g0 },
            new() { Id = "Microsoft.WindowsMaps", Name = "الخرائط (Maps)", Group = g0 },
            new() { Id = "Microsoft.ZuneVideo", Name = "Movies & TV", Group = g0 },
            new() { Id = "microsoft.windowscommunicationsapps", Name = "Mail & Calendar", Group = g0 },
            new() { Id = "Microsoft.OutlookForWindows", Name = "Outlook (الجديد)", Group = g0 },
            new() { Id = "Microsoft.YourPhone", Name = "Phone Link", Group = g0 },
            new() { Id = "Microsoft.SkypeApp", Name = "Skype", Group = g0 },
            new() { Id = "Microsoft.MicrosoftJournal", Name = "Journal", Group = g0 },
            new() { Id = "Microsoft.Windows.DevHome", Name = "Dev Home (متوقف من مايكروسوفت)", Group = g0 },
            new() { Id = "MicrosoftCorporationII.MicrosoftFamily", Name = "Microsoft Family", Group = g0 },
            new() { Id = "Microsoft.Wallet", Name = "Wallet", Group = g0 },
            new() { Id = "Microsoft.MixedReality.Portal", Name = "Mixed Reality Portal", Group = g0 },
            new() { Id = "Microsoft.Microsoft3DViewer", Name = "3D Viewer", Group = g0 },

            new() { Id = "Microsoft.GamingApp", Name = "Xbox App", Group = g1, Note = "ما تحتاجه إذا ما تستخدم Game Pass" },
            new() { Id = "Microsoft.XboxApp", Name = "Xbox Console Companion", Group = g1 },
            new() { Id = "Microsoft.XboxGamingOverlay", Name = "Xbox Game Bar (Win+G)", Group = g1, Note = "تسجيل الشاشة بـ Win+G يروح" },
            new() { Id = "Microsoft.XboxGameOverlay", Name = "Xbox Game Overlay", Group = g1 },
            new() { Id = "Microsoft.XboxSpeechToTextOverlay", Name = "Xbox Speech To Text", Group = g1 },
            new() { Id = "Microsoft.Xbox.TCUI", Name = "Xbox TCUI", Group = g1 },
            new() { Id = "Microsoft.XboxIdentityProvider", Name = "Xbox Identity Provider", Group = g1, DefaultOn = false,
                    Note = "بعض الألعاب (Minecraft، Forza، ألعاب المتجر) تحتاجه لتسجيل الدخول" },

            new() { Id = "*CandyCrush*", Name = "Candy Crush", Group = g2 },
            new() { Id = "*BubbleWitch*", Name = "Bubble Witch", Group = g2 },
            new() { Id = "king.com.*", Name = "ألعاب King", Group = g2 },
            new() { Id = "*Disney*", Name = "Disney+", Group = g2 },
            new() { Id = "*TikTok*", Name = "TikTok", Group = g2 },
            new() { Id = "*Instagram*", Name = "Instagram", Group = g2 },
            new() { Id = "*Facebook*", Name = "Facebook", Group = g2 },
            new() { Id = "*LinkedIn*", Name = "LinkedIn", Group = g2 },
            new() { Id = "*AmazonPrimeVideo*", Name = "Prime Video", Group = g2 },
            new() { Id = "AmazonVideo.PrimeVideo", Name = "Prime Video", Group = g2 },
            new() { Id = "*Netflix*", Name = "Netflix", Group = g2, DefaultOn = false },
            new() { Id = "SpotifyAB.SpotifyMusic", Name = "Spotify (نسخة المتجر)", Group = g2, DefaultOn = false },
            new() { Id = "*McAfee*", Name = "McAfee", Group = g2 },

            new() { Id = "Microsoft.ZuneMusic", Name = "Media Player", Group = g3, DefaultOn = false, Note = "مشغل الأغاني والفيديو الافتراضي" },
            new() { Id = "Microsoft.MicrosoftStickyNotes", Name = "Sticky Notes", Group = g3, DefaultOn = false },
            new() { Id = "Microsoft.WindowsAlarms", Name = "الساعة والمنبه", Group = g3, DefaultOn = false },
            new() { Id = "Microsoft.WindowsSoundRecorder", Name = "مسجل الصوت", Group = g3, DefaultOn = false },
            new() { Id = "MicrosoftCorporationII.QuickAssist", Name = "Quick Assist (مساعدة عن بعد)", Group = g3, DefaultOn = false },
            new() { Id = "Microsoft.WindowsCamera", Name = "الكاميرا", Group = g3, DefaultOn = false },
            new() { Id = "Microsoft.ScreenSketch", Name = "Snipping Tool (لقطة الشاشة)", Group = g3, DefaultOn = false, Note = "Win+Shift+S يحتاجه" },
        };
    }

    /// <summary>Names of every Appx package installed for any user.</summary>
    public static HashSet<string> InstalledPackages()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var (_, outp) = Util.PowerShell("Get-AppxPackage -AllUsers | Select-Object -ExpandProperty Name; Get-AppxProvisionedPackage -Online | Select-Object -ExpandProperty DisplayName", 120000);
        foreach (var l in outp.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) set.Add(l);
        return set;
    }

    static bool Matches(string pattern, string name)
    {
        if (!pattern.Contains('*')) return string.Equals(pattern, name, StringComparison.OrdinalIgnoreCase);
        var rx = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, rx, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public static void RefreshInstalled()
    {
        var installed = InstalledPackages();
        foreach (var a in Apps) a.Installed = installed.Any(n => Matches(a.Id, n));
    }

    /// <summary>Removes the app for all users + its provisioned copy (so Windows doesn't reinstall it for new accounts).</summary>
    public static bool Remove(BloatApp a)
    {
        string id = a.Id.Replace("'", "");
        var script = $@"
$ErrorActionPreference = 'SilentlyContinue'
Get-AppxPackage -AllUsers -Name '{id}' | ForEach-Object {{ Remove-AppxPackage -Package $_.PackageFullName -AllUsers }}
Get-AppxPackage -Name '{id}' | Remove-AppxPackage
Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -like '{id}' }} | Remove-AppxProvisionedPackage -Online -AllUsers | Out-Null
if (Get-AppxPackage -AllUsers -Name '{id}') {{ 'STILL' }} else {{ 'GONE' }}";
        var (_, outp) = Util.PowerShell(script, 300000);
        bool ok = outp.Contains("GONE");
        if (ok) Log.Ok($"إزالة تطبيق: {a.Name} ({a.Id})"); else Log.Warn($"تعذر إزالة {a.Name}: {outp.Trim()}");
        a.Installed = !ok;
        return ok;
    }

    // ---------------- OneDrive ----------------
    public static bool OneDriveInstalled()
    {
        var user = Path.Combine(Util.LocalAppData, @"Microsoft\OneDrive\OneDrive.exe");
        var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft OneDrive\OneDrive.exe");
        return File.Exists(user) || File.Exists(pf);
    }

    public static string RemoveOneDrive()
    {
        var sb = new StringBuilder();
        Util.Run("taskkill.exe", "/f /im OneDrive.exe", 15000);
        Thread.Sleep(1000);
        var candidates = new[]
        {
            Path.Combine(Util.WinDir, @"System32\OneDriveSetup.exe"),
            Path.Combine(Util.WinDir, @"SysWOW64\OneDriveSetup.exe"),
        }.Concat(Directory.Exists(Path.Combine(Util.LocalAppData, @"Microsoft\OneDrive"))
            ? Directory.GetFiles(Path.Combine(Util.LocalAppData, @"Microsoft\OneDrive"), "OneDriveSetup.exe", SearchOption.AllDirectories)
            : Array.Empty<string>());
        foreach (var setup in candidates.Where(File.Exists))
        {
            var r = Util.Run(setup, "/uninstall", 180000);
            sb.AppendLine($"{setup}: {r.code}");
        }
        Util.PowerShell("winget uninstall --id Microsoft.OneDrive --silent --accept-source-agreements", 180000);
        // keep it from coming back + remove it from the Explorer sidebar
        Reg.Set("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1, Microsoft.Win32.RegistryValueKind.DWord);
        Reg.Delete("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "OneDrive");
        try
        {
            using var k = Reg.Base("HKCU").CreateSubKey(@"Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", true);
            k.SetValue("System.IsPinnedToNameSpaceTree", 0, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { }
        Log.Ok("إزالة OneDrive: " + sb.ToString().Trim().Replace(Environment.NewLine, " | "));
        return sb.ToString();
    }
}
