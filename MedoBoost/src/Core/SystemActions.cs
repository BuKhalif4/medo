using System.Diagnostics;

namespace MedoBoost.Core;

/// <summary>Windows Update, drivers, network repair/speed, and Cloudflare WARP helpers.</summary>
public static class SystemActions
{
    // ---------------- Windows Update ----------------
    public static void OpenWindowsUpdate() => Util.OpenUrl("ms-settings:windowsupdate");

    public static void CheckForUpdates()
    {
        // UsoClient drives the same engine as the Settings "Check for updates" button
        Util.Run(Path.Combine(Util.WinDir, @"System32\UsoClient.exe"), "StartInteractiveScan", 20000);
        OpenWindowsUpdate();
        Log.Info("فحص تحديثات Windows");
    }

    public static void PauseUpdates(int days)
    {
        var now = DateTime.UtcNow;
        var end = now.AddDays(days).ToString("yyyy-MM-ddTHH:mm:ssZ");
        const string ux = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        Reg.Set("HKLM", ux, "PauseUpdatesStartTime", now.ToString("yyyy-MM-ddTHH:mm:ssZ"), Microsoft.Win32.RegistryValueKind.String);
        Reg.Set("HKLM", ux, "PauseUpdatesExpiryTime", end, Microsoft.Win32.RegistryValueKind.String);
        Reg.Set("HKLM", ux, "PauseFeatureUpdatesStartTime", now.ToString("yyyy-MM-ddTHH:mm:ssZ"), Microsoft.Win32.RegistryValueKind.String);
        Reg.Set("HKLM", ux, "PauseFeatureUpdatesEndTime", end, Microsoft.Win32.RegistryValueKind.String);
        Reg.Set("HKLM", ux, "PauseQualityUpdatesStartTime", now.ToString("yyyy-MM-ddTHH:mm:ssZ"), Microsoft.Win32.RegistryValueKind.String);
        Reg.Set("HKLM", ux, "PauseQualityUpdatesEndTime", end, Microsoft.Win32.RegistryValueKind.String);
        Log.Ok($"تم إيقاف تحديثات Windows مؤقتاً لمدة {days} يوم");
    }

    public static void ResumeUpdates()
    {
        const string ux = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        foreach (var v in new[] { "PauseUpdatesStartTime", "PauseUpdatesExpiryTime", "PauseFeatureUpdatesStartTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime" })
            Reg.Delete("HKLM", ux, v);
        Log.Ok("تم استئناف تحديثات Windows");
    }

    // ---------------- Drivers ----------------
    /// <summary>The GPU vendor's official driver download page (no bundled drivers — always the real source).</summary>
    public static (string label, string url) GpuDriverSource()
    {
        var name = HardwareMonitor.Instance.GpuName;
        if (string.IsNullOrEmpty(name)) try { name = SystemInfo.Read().Gpu; } catch { }
        name ??= "";
        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) || name.Contains("RTX", StringComparison.OrdinalIgnoreCase) || name.Contains("GTX", StringComparison.OrdinalIgnoreCase))
            return ("تعريف NVIDIA (موقع رسمي)", "https://www.nvidia.com/Download/index.aspx");
        if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
            return ("تعريف AMD (موقع رسمي)", "https://www.amd.com/en/support");
        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase) || name.Contains("Arc", StringComparison.OrdinalIgnoreCase))
            return ("تعريف Intel (موقع رسمي)", "https://www.intel.com/content/www/us/en/download-center/home.html");
        return ("تعريفات كرت الشاشة", "https://www.google.com/search?q=" + Uri.EscapeDataString(name + " driver download"));
    }

    public static void ScanHardwareChanges()
    {
        Util.Run("pnputil.exe", "/scan-devices", 60000);
        Log.Info("فحص تغييرات الأجهزة (تثبيت تعريفات ناقصة)");
    }

    public static void OpenOptionalDrivers() => Util.OpenUrl("ms-settings:windowsupdate-optionalupdates");
    public static void OpenDeviceManager() => Util.Run("devmgmt.msc", "", 2000);

    // ---------------- Network: repair + speed ----------------
    public static (string name, string[] dns)[] DnsChoices =
    {
        ("Cloudflare (الأسرع عادة)", new[] { "1.1.1.1", "1.0.0.1" }),
        ("Google", new[] { "8.8.8.8", "8.8.4.4" }),
        ("Cloudflare + حماية من المواقع الضارة", new[] { "1.1.1.2", "1.0.0.2" }),
        ("Quad9 (حماية)", new[] { "9.9.9.9", "149.112.112.112" }),
    };

    public static string[] ActiveAdapters()
    {
        var (_, o) = Util.PowerShell("Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -ExpandProperty Name", 30000);
        return o.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
    }

    public static void SetDns(string[] servers)
    {
        var list = string.Join(",", servers.Select(s => "'" + s + "'"));
        Util.PowerShell($"Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object {{ Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses {list} -ErrorAction SilentlyContinue }}", 60000);
        Util.Run("ipconfig", "/flushdns", 15000);
        Log.Ok("تم ضبط DNS → " + string.Join(", ", servers));
    }

    public static void ResetDns()
    {
        Util.PowerShell("Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ResetServerAddresses -ErrorAction SilentlyContinue }", 60000);
        Util.Run("ipconfig", "/flushdns", 15000);
        Log.Ok("تم إرجاع DNS للوضع التلقائي");
    }

    /// <summary>Full network repair. Needs a restart afterwards.</summary>
    public static void RepairNetwork()
    {
        Log.Info("إصلاح الشبكة...");
        Util.Run("ipconfig", "/flushdns", 15000);
        Util.Run("ipconfig", "/registerdns", 30000);
        Util.Run("ipconfig", "/release", 30000);
        Util.Run("ipconfig", "/renew", 60000);
        Util.Run("netsh", "winsock reset", 30000);
        Util.Run("netsh", "int ip reset", 30000);
        Util.Run("netsh", "interface ipv4 reset", 30000);
        Util.Run("netsh", "interface tcp reset", 30000);
        Log.Ok("تم إصلاح الشبكة – أعد تشغيل الجهاز");
    }

    /// <summary>Network speed tweaks (TCP auto-tuning, throttling off, flush). Reversible network tweaks are in the Tweaks page.</summary>
    public static void SpeedUpNetwork()
    {
        Util.Run("netsh", "int tcp set global autotuninglevel=normal", 20000);
        Util.Run("netsh", "int tcp set global rss=enabled", 20000);
        Util.Run("netsh", "int tcp set global ecncapability=enabled", 20000);
        Util.Run("netsh", "int tcp set supplemental internet congestionprovider=ctcp", 20000);
        Util.Run("ipconfig", "/flushdns", 15000);
        Log.Ok("تم تطبيق تحسينات سرعة الشبكة");
    }

    // ---------------- Cloudflare WARP (free fast VPN-like) ----------------
    public static string WarpCliPath
    {
        get
        {
            var p = Path.Combine(Util.ProgramFiles, @"Cloudflare\Cloudflare WARP\warp-cli.exe");
            return File.Exists(p) ? p : null;
        }
    }

    public static bool WarpInstalled => WarpCliPath != null ||
        File.Exists(Path.Combine(Util.ProgramFiles, @"Cloudflare\Cloudflare WARP\Cloudflare WARP.exe"));

    public static void OpenWarpDownload() => Util.OpenUrl("https://1.1.1.1/");

    public static void InstallWarp()
    {
        // winget is on Windows 10/11 by default; falls back to the download page
        Log.Info("تثبيت Cloudflare WARP...");
        var r = Util.PowerShell("winget install --id Cloudflare.Warp --silent --accept-source-agreements --accept-package-agreements", 600000);
        if (!WarpInstalled && r.code != 0) OpenWarpDownload();
    }

    public static bool WarpConnected()
    {
        var cli = WarpCliPath;
        if (cli == null) return false;
        var (_, o) = Util.Run(cli, "status", 15000);
        return o.Contains("Connected", StringComparison.OrdinalIgnoreCase);
    }

    public static void WarpConnect(bool on)
    {
        var cli = WarpCliPath;
        if (cli == null) { OpenWarpDownload(); return; }
        Util.Run(cli, "--accept-tos registration new", 30000);
        Util.Run(cli, on ? "--accept-tos connect" : "--accept-tos disconnect", 30000);
        Log.Ok("Cloudflare WARP → " + (on ? "متصل" : "مفصول"));
    }
}
