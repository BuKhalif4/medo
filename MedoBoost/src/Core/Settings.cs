using System.Text.Json;

namespace MedoBoost.Core;

public class AppSettings
{
    public bool OverlayEnabled { get; set; } = false;
    public int OverlayCorner { get; set; } = 0;          // 0 top-left, 1 top-right, 2 bottom-left, 3 bottom-right
    public double OverlayOpacity { get; set; } = 0.85;
    public double OverlayScale { get; set; } = 1.0;
    public bool OverlayHorizontal { get; set; } = true;
    public bool ShowFps { get; set; } = true;
    public bool ShowFrameTime { get; set; } = true;
    public bool ShowCpu { get; set; } = true;
    public bool ShowCpuTemp { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowGpuTemp { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowVram { get; set; } = false;
    public bool ShowClock { get; set; } = false;
    public int BenchmarkSeconds { get; set; } = 60;
    public List<string> CleanerUnchecked { get; set; } = new();
    public List<string> CleanerChecked { get; set; } = new();
    public List<string> BoostKillList { get; set; } = new();
    public bool StartMinimized { get; set; } = false;
    public bool AutoGamePriority { get; set; } = false;
    public bool AutoUpdateCheck { get; set; } = true;
    public string DismissedUpdateTag { get; set; } = "";
    public string Language { get; set; } = "ar";          // ar | en
    public string Design { get; set; } = "prime";         // prime | classic
    public bool WelcomeDone { get; set; } = false;
    // Game Ready (what "جهّز للعب" does)
    public bool GameReadyClose { get; set; } = true;
    public bool GameReadyRam { get; set; } = true;
    public bool GameReadyDns { get; set; } = true;
    public bool GameReadyPower { get; set; } = true;
    public bool GameReadyServices { get; set; } = true;
    public bool GameReadyPauseUpdates { get; set; } = true;
    public bool GameReadyMute { get; set; } = true;
    public bool GameReadyAuto { get; set; } = false;
    // tray + alerts
    public bool MinimizeToTray { get; set; } = true;
    public bool TrayHintShown { get; set; } = false;
    public bool ThermalAlerts { get; set; } = true;
    public int CpuTempLimit { get; set; } = 90;
    public int GpuTempLimit { get; set; } = 85;
    public string BenchLabel { get; set; } = "قبل";
    public bool AutoRamClean { get; set; } = false;
    public int AutoRamThreshold { get; set; } = 85;
    // play sessions
    public bool RecordSessions { get; set; } = true;
    // prayer times (calculated offline, nothing is sent anywhere)
    public bool PrayerAlerts { get; set; } = false;
    public bool PrayerSound { get; set; } = true;
    public int PrayerMinutesBefore { get; set; } = 10;
    public string PrayerCity { get; set; } = "";          // key of Prayer.Cities, "" = not chosen yet
    public double PrayerLat { get; set; } = 21.4225;
    public double PrayerLon { get; set; } = 39.8262;
    public string PrayerMethod { get; set; } = "MWL";     // MWL | Makkah | Egypt | Karachi | ISNA
}

internal static class Settings
{
    static string FilePath => Path.Combine(Util.DataDir, "settings.json");
    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try { if (File.Exists(FilePath)) Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { Current = new(); }
    }

    public static void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }
}
