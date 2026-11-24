using Microsoft.Win32;

namespace MedoBoost.Core;

public class StartupEntry
{
    public string Name;
    public string Value;         // registry value name / file name
    public string Command;
    public string Path;
    public string Source;        // shown to user
    public string Hive;          // HKCU / HKLM or null for folder
    public string RunKey;        // registry key or folder
    public string ApprovedKey;   // StartupApproved key
    public string ApprovedHive;
    public bool IsFile;
    public bool Enabled;
    public string Publisher;
}

/// <summary>Reads/changes startup apps exactly like Task Manager does (StartupApproved keys), so it's 100% reversible.</summary>
public static class StartupManager
{
    const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    public static List<StartupEntry> List()
    {
        var l = new List<StartupEntry>();
        ReadRun(l, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "Run", "المستخدم (Registry)");
        ReadRun(l, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run", "كل المستخدمين (Registry)");
        ReadRun(l, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32", "كل المستخدمين 32-bit");
        ReadFolder(l, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "HKCU", "مجلد Startup (المستخدم)");
        ReadFolder(l, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "HKLM", "مجلد Startup (الكل)");
        foreach (var e in l)
        {
            try
            {
                if (e.Path != null && File.Exists(e.Path))
                {
                    var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(e.Path);
                    e.Publisher = vi.CompanyName;
                    if (!string.IsNullOrWhiteSpace(vi.FileDescription)) e.Name = vi.FileDescription.Trim() + (vi.FileDescription.Trim() == e.Name ? "" : $"  ({e.Name})");
                }
            }
            catch { }
        }
        return l;
    }

    static void ReadRun(List<StartupEntry> l, string hive, string key, string approvedName, string source)
    {
        try
        {
            using var k = Reg.Base(hive).OpenSubKey(key);
            if (k == null) return;
            foreach (var n in k.GetValueNames())
            {
                if (string.IsNullOrEmpty(n)) continue;
                var cmd = k.GetValue(n)?.ToString() ?? "";
                var e = new StartupEntry
                {
                    Name = n, Value = n, Command = cmd, Path = ExePath(cmd), Source = source, Hive = hive, RunKey = key,
                    ApprovedHive = hive, ApprovedKey = Approved + approvedName,
                };
                e.Enabled = IsEnabled(e, n);
                l.Add(e);
            }
        }
        catch { }
    }

    static void ReadFolder(List<StartupEntry> l, string folder, string hive, string source)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            foreach (var f in Directory.GetFiles(folder))
            {
                var fn = System.IO.Path.GetFileName(f);
                if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                var e = new StartupEntry
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(f), Value = fn, Command = f, Path = f, Source = source, IsFile = true,
                    RunKey = folder, ApprovedHive = hive, ApprovedKey = Approved + "StartupFolder",
                };
                e.Enabled = IsEnabled(e, fn);
                l.Add(e);
            }
        }
        catch { }
    }

    static string ValueName(StartupEntry e) => e.Value;
    static string RawName(StartupEntry e) => e.Value;

    static bool IsEnabled(StartupEntry e, string valueName)
    {
        try
        {
            using var k = Reg.Base(e.ApprovedHive).OpenSubKey(e.ApprovedKey);
            if (k?.GetValue(valueName) is byte[] b && b.Length > 0) return (b[0] & 1) == 0;
        }
        catch { }
        return true;
    }

    public static void SetEnabled(StartupEntry e, bool enabled)
    {
        var data = new byte[12];
        data[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled) BitConverter.GetBytes(DateTime.Now.ToFileTime()).CopyTo(data, 4);
        using var k = Reg.Base(e.ApprovedHive).CreateSubKey(e.ApprovedKey, true);
        k.SetValue(ValueName(e), data, RegistryValueKind.Binary);
        e.Enabled = enabled;
        Log.Info($"بدء التشغيل: {e.Name} → {(enabled ? "مفعل" : "معطل")}");
    }

    public static void Remove(StartupEntry e)
    {
        if (e.IsFile)
        {
            var backupDir = System.IO.Path.Combine(Util.DataDir, "startup-backup");
            Directory.CreateDirectory(backupDir);
            File.Copy(e.Command, System.IO.Path.Combine(backupDir, System.IO.Path.GetFileName(e.Command)), true);
            File.Delete(e.Command);
        }
        else
        {
            File.AppendAllText(System.IO.Path.Combine(Util.DataDir, "startup-removed.reg.txt"), $"[{e.Hive}\\{e.RunKey}]\r\n\"{RawName(e)}\"=\"{e.Command}\"\r\n\r\n");
            using var k = Reg.Base(e.Hive).OpenSubKey(e.RunKey, true);
            k?.DeleteValue(RawName(e), false);
        }
        Log.Warn($"حذف من بدء التشغيل: {e.Name} ({e.Command})");
    }

    public static string ExePath(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return null;
        cmd = Environment.ExpandEnvironmentVariables(cmd.Trim());
        if (cmd.StartsWith("\""))
        {
            int end = cmd.IndexOf('"', 1);
            return end > 1 ? cmd[1..end] : null;
        }
        int i = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return i > 0 ? cmd[..(i + 4)] : cmd.Split(' ')[0];
    }
}
