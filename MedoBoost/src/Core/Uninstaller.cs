using System.Diagnostics;
using Microsoft.Win32;

namespace MedoBoost.Core;

public class InstalledApp
{
    public string Name;
    public string Publisher;
    public string Version;
    public string UninstallString;
    public string QuietUninstallString;
    public string InstallLocation;
    public string Icon;
    public long EstimatedSize;      // bytes
    public DateTime? InstallDate;
    public bool IsAppx;
    public string AppxId;
    public bool System;
}

/// <summary>
/// Lists installed programs (registry Uninstall keys + Store apps) and removes them, then cleans the
/// leftover folders they leave in Program Files / AppData / ProgramData — like a lightweight Revo Uninstaller.
/// </summary>
public static class Uninstaller
{
    static readonly (string hive, string path)[] Keys =
    {
        ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        ("HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        ("HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    };

    public static List<InstalledApp> List()
    {
        var map = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, path) in Keys)
        {
            try
            {
                using var root = Reg.Base(hive).OpenSubKey(path);
                if (root == null) continue;
                foreach (var sub in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(sub);
                    if (k == null) continue;
                    var name = k.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if ((k.GetValue("SystemComponent") as int? ?? 0) == 1) continue;
                    if (k.GetValue("ParentKeyName") != null || k.GetValue("ReleaseType") is "Update" or "Hotfix" or "Security Update") continue;
                    var uninstall = k.GetValue("UninstallString") as string;
                    var quiet = k.GetValue("QuietUninstallString") as string;
                    if (string.IsNullOrWhiteSpace(uninstall) && string.IsNullOrWhiteSpace(quiet)) continue;
                    if (name.StartsWith("KB") && name.Contains("Update")) continue;
                    var app = new InstalledApp
                    {
                        Name = name.Trim(),
                        Publisher = (k.GetValue("Publisher") as string ?? "").Trim(),
                        Version = (k.GetValue("DisplayVersion") as string ?? "").Trim(),
                        UninstallString = uninstall,
                        QuietUninstallString = quiet,
                        InstallLocation = (k.GetValue("InstallLocation") as string ?? "").Trim(),
                        Icon = (k.GetValue("DisplayIcon") as string ?? "").Split(',')[0].Trim('"'),
                        EstimatedSize = (long)((k.GetValue("EstimatedSize") as int? ?? 0)) * 1024,
                    };
                    if (DateTime.TryParseExact(k.GetValue("InstallDate") as string ?? "", "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d)) app.InstallDate = d;
                    if (!map.ContainsKey(app.Name)) map[app.Name] = app;
                }
            }
            catch (Exception ex) { Log.Warn("Uninstaller list: " + ex.Message); }
        }
        return map.Values.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Runs the program's own uninstaller (silently when it supports it). Returns true if it launched.</summary>
    public static bool RunUninstall(InstalledApp app, bool silent)
    {
        try
        {
            string cmd = (silent && !string.IsNullOrWhiteSpace(app.QuietUninstallString)) ? app.QuietUninstallString : app.UninstallString;
            if (string.IsNullOrWhiteSpace(cmd)) return false;
            string file, args;
            if (cmd.StartsWith("\"") && cmd.IndexOf('"', 1) > 0) { int e = cmd.IndexOf('"', 1); file = cmd[1..e]; args = cmd[(e + 1)..].Trim(); }
            else if (cmd.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase))
            {
                file = "msiexec.exe";
                args = cmd[(cmd.IndexOf(' ') + 1)..].Replace("/I", "/X");
                if (silent && !args.Contains("/quiet") && !args.Contains("/qn")) args += " /quiet /norestart";
            }
            else { var sp = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase); if (sp > 0) { file = cmd[..(sp + 4)]; args = cmd[(sp + 4)..].Trim(); } else { file = cmd; args = ""; } }

            var psi = new ProcessStartInfo(file, args) { UseShellExecute = true };
            var p = Process.Start(psi);
            Log.Info($"إلغاء تثبيت: {app.Name}");
            p?.WaitForExit(600000);
            return true;
        }
        catch (Exception ex) { Log.Error($"إلغاء {app.Name}: {ex.Message}"); return false; }
    }

    /// <summary>Folders the program may leave behind after its uninstaller runs.</summary>
    public static List<string> FindLeftovers(InstalledApp app)
    {
        var found = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(app.InstallLocation)) names.Add(Path.GetFileName(app.InstallLocation.TrimEnd('\\')));
        // only the product's own name and its install folder. Never the publisher: "Google" or "Microsoft" folders are shared by other apps.
        var prod = app.Name.Split('(', '-').First().Trim();
        if (prod.Length > 3) names.Add(prod);

        var roots = new[]
        {
            app.InstallLocation,
            Util.ProgramFiles, Util.ProgramFilesX86, Util.LocalAppData, Util.RoamingAppData, Util.ProgramData,
        };
        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)))
        {
            if (string.Equals(root, app.InstallLocation, StringComparison.OrdinalIgnoreCase))
            {
                if (Cleaner.IsSafeDir(root) && Directory.Exists(root)) found.Add(root);
                continue;
            }
            foreach (var n in names)
            {
                var cand = Path.Combine(root, n);
                try { if (Directory.Exists(cand) && Cleaner.IsSafeDir(cand)) found.Add(cand); } catch { }
            }
        }
        return found.Distinct(StringComparer.OrdinalIgnoreCase).Where(Cleaner.IsSafeDir).ToList();
    }

    public static long DeleteLeftovers(IEnumerable<string> dirs, StreamWriter deleted)
    {
        long freed = 0;
        foreach (var d in dirs)
        {
            if (!Cleaner.IsSafeDir(d) || !Directory.Exists(d)) continue;
            try
            {
                long size = 0;
                foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)) { try { size += new FileInfo(f).Length; } catch { } }
                Directory.Delete(d, true);
                freed += size;
                deleted?.WriteLine(d);
                Log.Info("حذف بقايا: " + d);
            }
            catch (Exception ex) { Log.Warn($"بقايا {d}: {ex.Message}"); }
        }
        return freed;
    }

    public static long CleanTemp()
    {
        var items = Cleaner.Items.Where(i => i.Id is "user_temp" or "win_temp").ToList();
        long freed = 0;
        using var w = ProcessManager.DeletedListWriter();
        foreach (var i in items) { Cleaner.Scan(i); Cleaner.Clean(i, w); freed += i.Freed; }
        return freed;
    }
}
