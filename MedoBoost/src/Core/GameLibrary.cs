using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MedoBoost.Core;

public class GameInfo
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";        // Steam | Epic | GOG
    public string AppId { get; set; } = "";         // Steam app id (or Epic app name)
    public string Dir { get; set; } = "";
    public string ExePath { get; set; } = "";       // main executable, "" if not found
    public long SizeBytes { get; set; }
    public string LaunchUri { get; set; } = "";     // steam://… for Steam; otherwise ExePath is started

    public string ProcessName => string.IsNullOrEmpty(ExePath) ? "" : Path.GetFileNameWithoutExtension(ExePath);
}

/// <summary>
/// Finds the games installed from Steam, Epic Games and GOG by reading those launchers' own files and registry keys.
/// Read-only: nothing is changed and nothing is sent anywhere.
/// </summary>
public static class GameLibrary
{
    static readonly string[] NotGames =
    {
        "Steamworks Common Redistributables", "Steam Linux Runtime", "Proton", "Steam Controller Configs", "SteamVR", "Wallpaper Engine",
        "Source SDK", "Dedicated Server", "Redistributable",
    };

    // executables that live inside game folders but are not the game
    static readonly Regex JunkExe = new(@"(unins|uninstall|crashhandler|crashreport|crashpad|vcredist|vc_redist|dxsetup|dotnet|easyanticheat|battleye|cefprocess|installer|redist|prereq|^setup)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<GameInfo> Scan()
    {
        var list = new List<GameInfo>();
        Safe("Steam", () => list.AddRange(Steam()));
        Safe("Epic", () => list.AddRange(Epic()));
        Safe("GOG", () => list.AddRange(Gog()));
        foreach (var g in list)
            if (string.IsNullOrEmpty(g.ExePath) && Directory.Exists(g.Dir)) g.ExePath = FindMainExe(g.Dir);
        return list.GroupBy(g => g.Dir.ToLowerInvariant()).Select(x => x.First()).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static void Safe(string what, Action a)
    {
        try { a(); } catch (Exception ex) { Log.Warn($"ألعاب {what}: {ex.Message}"); }
    }

    // ---------------- Steam ----------------
    static IEnumerable<GameInfo> Steam()
    {
        string root = Reg.Get("HKCU", @"Software\Valve\Steam", "SteamPath") as string
                      ?? Reg.Get("HKLM", @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") as string;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) yield break;
        root = root.Replace('/', '\\');

        var libs = new List<string> { root };
        var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
            {
                var p = m.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(p) && !libs.Contains(p, StringComparer.OrdinalIgnoreCase)) libs.Add(p);
            }

        foreach (var lib in libs)
        {
            var apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var acf in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                string txt;
                try { txt = File.ReadAllText(acf); } catch (IOException) { continue; }
                string V(string key) { var m = Regex.Match(txt, "\"" + key + "\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase); return m.Success ? m.Groups[1].Value : ""; }
                string name = V("name"), dir = V("installdir"), id = V("appid");
                if (name == "" || dir == "" || id == "") continue;
                if (NotGames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase))) continue;
                var full = Path.Combine(apps, "common", dir);
                if (!Directory.Exists(full)) continue;       // listed but not (fully) installed
                long.TryParse(V("SizeOnDisk"), out long size);
                yield return new GameInfo { Name = name, Source = "Steam", AppId = id, Dir = full, SizeBytes = size, LaunchUri = "steam://rungameid/" + id };
            }
        }
    }

    // ---------------- Epic ----------------
    static IEnumerable<GameInfo> Epic()
    {
        var dir = Path.Combine(Util.ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(dir)) yield break;
        foreach (var f in Directory.EnumerateFiles(dir, "*.item"))
        {
            GameInfo g = null;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                var r = doc.RootElement;
                string Str(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                var name = Str("DisplayName"); var loc = Str("InstallLocation");
                if (name == "" || !Directory.Exists(loc)) continue;
                if (r.TryGetProperty("bIsApplication", out var isApp) && isApp.ValueKind == JsonValueKind.False) continue;
                long size = r.TryGetProperty("InstallSize", out var sz) && sz.TryGetInt64(out var l) ? l : 0;
                var exe = Str("LaunchExecutable");
                g = new GameInfo { Name = name, Source = "Epic", AppId = Str("AppName"), Dir = loc, SizeBytes = size, ExePath = exe == "" ? "" : Path.Combine(loc, exe) };
                if (!File.Exists(g.ExePath)) g.ExePath = "";
            }
            catch (Exception ex) when (ex is JsonException or IOException) { Log.Warn($"Epic {Path.GetFileName(f)}: {ex.Message}"); }
            if (g != null) yield return g;
        }
    }

    // ---------------- GOG ----------------
    static IEnumerable<GameInfo> Gog()
    {
        using var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
        if (root == null) yield break;
        foreach (var sub in root.GetSubKeyNames())
        {
            using var k = root.OpenSubKey(sub);
            var name = k?.GetValue("gameName") as string; var path = k?.GetValue("path") as string; var exe = k?.GetValue("exe") as string;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;
            yield return new GameInfo { Name = name, Source = "GOG", AppId = sub, Dir = path, ExePath = !string.IsNullOrEmpty(exe) && File.Exists(exe) ? exe : "" };
        }
    }

    /// <summary>The biggest plausible .exe in the game folder (top two levels).</summary>
    static string FindMainExe(string dir)
    {
        try
        {
            var opt = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return new DirectoryInfo(dir).EnumerateFiles("*.exe", opt)
                .Where(f => !JunkExe.IsMatch(f.Name))
                .OrderByDescending(f => f.Length).FirstOrDefault()?.FullName ?? "";
        }
        catch (Exception ex) { Log.Warn($"exe in {dir}: {ex.Message}"); return ""; }
    }

    // ---------------- launching ----------------
    public static bool Launch(GameInfo g)
    {
        try
        {
            if (g.LaunchUri != "") Process.Start(new ProcessStartInfo(g.LaunchUri) { UseShellExecute = true });
            else if (g.ExePath != "") Process.Start(new ProcessStartInfo(g.ExePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(g.ExePath) });
            else return false;
            Log.Info("تشغيل لعبة: " + g.Name);
            return true;
        }
        catch (Exception ex) { Log.Error($"تشغيل {g.Name}: {ex.Message}"); return false; }
    }
}
