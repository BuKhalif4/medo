using System.Diagnostics;
using System.Text;

namespace MedoBoost.Core;

internal static class Util
{
    public static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";
    public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string RoamingAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    public static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    public static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static string LocalLow => Path.Combine(UserProfile, "AppData", "LocalLow");
    public static string SystemDrive => Path.GetPathRoot(WinDir) ?? "C:\\";

    public static string DataDir
    {
        get { var d = Path.Combine(LocalAppData, "MedoBoost"); Directory.CreateDirectory(d); return d; }
    }

    public static string Size(long bytes)
    {
        if (bytes < 0) bytes = 0;
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{v:0} {u[i]}" : $"{v:0.0} {u[i]}";
    }

    /// <summary>Runs a command hidden and returns (exitCode, output).</summary>
    public static (int code, string output) Run(string file, string args, int timeoutMs = 120000)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            var sb = new StringBuilder();
            p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return (-1, sb + "\n(timeout)"); }
            p.WaitForExit();
            return (p.ExitCode, sb.ToString());
        }
        catch (Exception ex) { return (-1, ex.Message); }
    }

    public static (int code, string output) PowerShell(string script, int timeoutMs = 120000)
    {
        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return Run("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {enc}", timeoutMs);
    }

    /// <summary>Opens a visible console window that runs a command and stays open (for SFC / DISM etc).</summary>
    public static void RunVisible(string command)
    {
        try { Process.Start(new ProcessStartInfo("cmd.exe", "/k " + command) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("RunVisible: " + ex.Message); }
    }

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            if (File.Exists(path)) Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (Directory.Exists(path)) Process.Start("explorer.exe", $"\"{path}\"");
        }
        catch { }
    }
}
