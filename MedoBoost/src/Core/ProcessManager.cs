using System.Diagnostics;

namespace MedoBoost.Core;

public class AppGroup
{
    public string Key;           // process name (lowercase)
    public string Name;          // process name
    public string Title;         // friendly name (file description)
    public string Path;
    public List<int> Pids = new();
    public long Ram;             // private working set (like Task Manager)
    public double Cpu;           // %
    public double Gpu;           // %
    public bool IsSystem;
    public CleanItem Cache;      // known cache for this app (Discord, Chrome ...)
}

/// <summary>Groups running processes by app (e.g. all Discord.exe processes) with RAM / CPU / GPU usage.</summary>
public sealed class ProcessManager
{
    public static ProcessManager Instance { get; } = new();
    readonly Dictionary<int, (TimeSpan cpu, DateTime at)> prevCpu = new();
    readonly Dictionary<int, string> pathCache = new();
    readonly Dictionary<string, string> titleCache = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> SystemNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "services", "lsass", "winlogon", "svchost", "fontdrvhost", "dwm",
        "Memory Compression", "spoolsv", "WmiPrvSE", "conhost", "sihost", "taskhostw", "ctfmon", "RuntimeBroker", "dllhost",
        "SearchIndexer", "SecurityHealthService", "MsMpEng", "NisSrv", "audiodg", "LsaIso", "SgrmBroker", "WUDFHost", "explorer",
        "StartMenuExperienceHost", "ShellExperienceHost", "SearchHost", "TextInputHost", "smartscreen", "Secure System", "MpDefenderCoreService",
    };

    static Dictionary<int, long> PrivateWorkingSets()
    {
        var map = new Dictionary<int, long>();
        try
        {
            var cat = new PerformanceCounterCategory("Process");
            var data = cat.ReadCategory();
            var ids = data["ID Process"];
            var ws = data["Working Set - Private"];
            if (ids == null || ws == null) return map;
            foreach (InstanceData inst in ids.Values)
            {
                int pid = (int)inst.RawValue;
                var w = ws[inst.InstanceName];
                if (w != null && pid > 0) map[pid] = w.RawValue;
            }
        }
        catch { }
        return map;
    }

    public List<AppGroup> Snapshot(bool includeSystem)
    {
        var now = DateTime.UtcNow;
        int cores = Environment.ProcessorCount;
        var pws = PrivateWorkingSets();
        var gpu = HardwareMonitor.Instance.Last.GpuByPid;
        var groups = new Dictionary<string, AppGroup>(StringComparer.OrdinalIgnoreCase);
        var alive = new HashSet<int>();
        int self = Environment.ProcessId;

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                int pid = p.Id;
                if (pid <= 4 || pid == self) continue;
                alive.Add(pid);
                string name = p.ProcessName;
                if (!pathCache.TryGetValue(pid, out var path)) pathCache[pid] = path = Native.ProcessPath(pid);
                bool sys = SystemNames.Contains(name) || path == null || path.StartsWith(Util.WinDir, StringComparison.OrdinalIgnoreCase);
                if (sys && !includeSystem) continue;

                if (!groups.TryGetValue(name, out var g))
                {
                    groups[name] = g = new AppGroup { Key = name.ToLowerInvariant(), Name = name, Path = path, IsSystem = sys };
                    g.Title = Title(path, name);
                    g.Cache = Cleaner.ForProcess(name);
                }
                if (g.Path == null && path != null) g.Path = path;
                g.Pids.Add(pid);
                g.Ram += pws.TryGetValue(pid, out var r) ? r : p.WorkingSet64;
                if (gpu != null && gpu.TryGetValue(pid, out var gv)) g.Gpu = Math.Max(g.Gpu, gv);
                try
                {
                    var t = p.TotalProcessorTime;
                    if (prevCpu.TryGetValue(pid, out var prev))
                    {
                        double dt = (now - prev.at).TotalMilliseconds;
                        if (dt > 0) g.Cpu += (t - prev.cpu).TotalMilliseconds / dt / cores * 100;
                    }
                    prevCpu[pid] = (t, now);
                }
                catch { }
            }
            catch { }
            finally { p.Dispose(); }
        }
        foreach (var k in prevCpu.Keys.Where(k => !alive.Contains(k)).ToList()) { prevCpu.Remove(k); pathCache.Remove(k); }
        foreach (var g in groups.Values) g.Cpu = Math.Min(100, Math.Max(0, g.Cpu));
        return groups.Values.ToList();
    }

    string Title(string path, string name)
    {
        if (path == null) return name;
        if (titleCache.TryGetValue(path, out var t)) return t;
        try
        {
            var vi = FileVersionInfo.GetVersionInfo(path);
            t = string.IsNullOrWhiteSpace(vi.FileDescription) ? name : vi.FileDescription.Trim();
        }
        catch { t = name; }
        titleCache[path] = t;
        return t;
    }

    public static int Kill(AppGroup g)
    {
        int n = 0;
        foreach (var pid in g.Pids)
        {
            try { using var p = Process.GetProcessById(pid); p.Kill(true); n++; } catch { }
        }
        Log.Info($"إنهاء {g.Name} ({n} عملية)");
        return n;
    }

    public static long Trim(AppGroup g)
    {
        var before = Memory.AvailBytes();
        foreach (var pid in g.Pids) Memory.TrimProcess(pid);
        Thread.Sleep(150);
        long freed = Math.Max(0, (long)Memory.AvailBytes() - (long)before);
        Log.Info($"تخفيف رام {g.Name}: {Util.Size(freed)}");
        return freed;
    }

    public static void SetPriority(AppGroup g, ProcessPriorityClass pc)
    {
        foreach (var pid in g.Pids)
            try { using var p = Process.GetProcessById(pid); p.PriorityClass = pc; } catch { }
        Log.Info($"أولوية {g.Name} → {pc}");
    }

    /// <summary>Close the app, delete its cache, start it again.</summary>
    public static long ClearCacheAndRestart(AppGroup g, bool restart)
    {
        if (g.Cache == null) return 0;
        string exe = g.Path;
        // Discord-like apps: restart through their own launcher if present
        foreach (var n in g.Cache.Processes)
            foreach (var p in Process.GetProcessesByName(n)) { try { p.Kill(true); } catch { } finally { p.Dispose(); } }
        Thread.Sleep(1200);
        Cleaner.Scan(g.Cache);
        using (var w = DeletedListWriter()) Cleaner.Clean(g.Cache, w);
        if (restart && exe != null && File.Exists(exe))
        {
            try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(exe) }); } catch { }
        }
        return g.Cache.Freed;
    }

    public static StreamWriter DeletedListWriter()
    {
        var dir = System.IO.Path.Combine(Util.DataDir, "logs");
        Directory.CreateDirectory(dir);
        return new StreamWriter(System.IO.Path.Combine(dir, $"deleted-files-{DateTime.Now:yyyy-MM-dd}.txt"), true) { AutoFlush = false };
    }
}
