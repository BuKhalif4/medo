namespace MedoBoost.Core;

public class DefenderStatus
{
    public bool RealtimeOn = true;
    public bool TamperOn = true;
    public bool AntivirusEnabled = true;
    public string Signature = "";
    public DateTime? LastScan;
    public bool Available = true;
}

/// <summary>
/// Windows Defender control. Modern Windows guards Defender with Tamper Protection, which NO program can
/// bypass programmatically (by design). So disabling real-time protection only works if the user turned
/// Tamper Protection off first; otherwise we tell them and open the page. Scans / updates / exclusions always work.
/// </summary>
public static class Security
{
    public static DefenderStatus Status()
    {
        var s = new DefenderStatus();
        var (_, o) = Util.PowerShell(
            "$m=Get-MpComputerStatus; " +
            "'RT='+$m.RealTimeProtectionEnabled; 'TP='+$m.IsTamperProtected; 'AV='+$m.AntivirusEnabled; " +
            "'SIG='+$m.AntivirusSignatureVersion; 'SCAN='+$m.QuickScanEndTime", 40000);
        if (string.IsNullOrWhiteSpace(o)) { s.Available = false; return s; }
        foreach (var line in o.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("RT=")) s.RealtimeOn = line.EndsWith("True", StringComparison.OrdinalIgnoreCase);
            else if (line.StartsWith("TP=")) s.TamperOn = line.EndsWith("True", StringComparison.OrdinalIgnoreCase);
            else if (line.StartsWith("AV=")) s.AntivirusEnabled = line.EndsWith("True", StringComparison.OrdinalIgnoreCase);
            else if (line.StartsWith("SIG=")) s.Signature = line[4..].Trim();
            else if (line.StartsWith("SCAN=") && DateTime.TryParse(line[5..].Trim(), out var d)) s.LastScan = d;
        }
        return s;
    }

    public enum ToggleResult { Done, TamperBlocked, Failed }

    /// <summary>Turn real-time protection off/on. Returns TamperBlocked if Tamper Protection prevents it.</summary>
    public static ToggleResult SetRealtime(bool on)
    {
        var st = Status();
        if (!on && st.TamperOn) return ToggleResult.TamperBlocked;
        var val = on ? "$false" : "$true";
        var (_, o) = Util.PowerShell(
            $"try {{ Set-MpPreference -DisableRealtimeMonitoring {val} -ErrorAction Stop; 'OK' }} catch {{ 'ERR:'+$_.Exception.Message }}", 40000);
        if (o.Contains("OK"))
        {
            // policy fallback (helps on some editions, ignored where tamper protection is on)
            Reg.Set("HKLM", @"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", on ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
            var after = Status();
            Log.Ok($"الحماية اللحظية → {(on ? "مفعلة" : "متوقفة")}");
            return after.RealtimeOn == on ? ToggleResult.Done : ToggleResult.TamperBlocked;
        }
        if (o.Contains("0x800106ba") || o.Contains("Tamper", StringComparison.OrdinalIgnoreCase) || o.Contains("denied", StringComparison.OrdinalIgnoreCase))
            return ToggleResult.TamperBlocked;
        Log.Warn("Defender toggle: " + o.Trim());
        return ToggleResult.Failed;
    }

    public static void OpenTamperSettings() => Util.OpenUrl("windowsdefender://threatsettings");
    public static void OpenSecurityCenter() => Util.OpenUrl("windowsdefender://");

    public static (int code, string output) UpdateSignatures()
    {
        Log.Info("تحديث تعريفات الحماية...");
        var exe = Path.Combine(Util.ProgramFiles, @"Windows Defender\MpCmdRun.exe");
        if (File.Exists(exe)) return Util.Run(exe, "-SignatureUpdate", 300000);
        return Util.PowerShell("Update-MpSignature", 300000);
    }

    public static void QuickScan() { Log.Info("فحص سريع..."); Util.PowerShell("Start-MpScan -ScanType QuickScan", 5000); }
    public static void FullScan() { Log.Info("فحص كامل..."); Util.PowerShell("Start-MpScan -ScanType FullScan", 5000); }
    public static void ScanPath(string path) { Log.Info("فحص: " + path); Util.PowerShell($"Start-MpScan -ScanType CustomScan -ScanPath '{path.Replace("'", "''")}'", 5000); }

    public static void AddExclusion(string path)
    {
        Util.PowerShell($"Add-MpPreference -ExclusionPath '{path.Replace("'", "''")}'", 40000);
        Log.Ok("تم استثناء: " + path);
    }

    public static List<string> Exclusions()
    {
        var (_, o) = Util.PowerShell("(Get-MpPreference).ExclusionPath", 40000);
        return o.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public static void RemoveExclusion(string path)
    {
        Util.PowerShell($"Remove-MpPreference -ExclusionPath '{path.Replace("'", "''")}'", 40000);
        Log.Info("أُزيل الاستثناء: " + path);
    }

    public static string LastThreats()
    {
        var (_, o) = Util.PowerShell("Get-MpThreatDetection | Select-Object -Last 5 | ForEach-Object { $_.ThreatID }", 40000);
        return o.Trim();
    }
}
