using System.Diagnostics;
using Microsoft.Win32;

namespace MedoBoost.Core;

public enum Risk { Safe, Caution }

/// <summary>A folder (or file pattern) that a clean item deletes. Only the CONTENTS are removed, never the folder itself.</summary>
public record CleanTarget(string Dir, string Pattern = "*", bool Recursive = true, int MinAgeHours = 0);

public class CleanItem
{
    public string Id;
    public string Category;
    public string Name;
    public string Description;
    public Risk Risk = Risk.Safe;
    public bool DefaultOn = true;
    public string[] Processes = Array.Empty<string>();   // apps that lock these files
    public Func<IEnumerable<CleanTarget>> Targets = () => Array.Empty<CleanTarget>();
    public Func<long> CustomScan;                         // for special items (recycle bin...)
    public Func<CleanItem, long> CustomClean;             // returns bytes freed
    public string Glyph = "\uE74D";

    // results
    public long Size;
    public int Files;
    public long Freed;
    public int Skipped;
    public bool Scanned;
    public bool NoSizeInfo;
}

public static class Cleaner
{
    static string L => Util.LocalAppData;
    static string R => Util.RoamingAppData;
    static string PD => Util.ProgramData;
    static string W => Util.WinDir;
    static string LL => Util.LocalLow;
    static string SD => Util.SystemDrive;

    public static readonly string[] Categories =
    {
        "النظام (Windows)", "كرت الشاشة والألعاب", "المتصفحات", "التطبيقات", "الخصوصية والسجلات", "تنظيف متقدم"
    };

    public static List<CleanItem> Items { get; } = Build();

    // ---------------- Safety ----------------
    static readonly string[] Forbidden =
    {
        Util.WinDir, Path.Combine(Util.WinDir, "System32"), Path.Combine(Util.WinDir, "SysWOW64"),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Util.UserProfile, Util.LocalAppData, Util.RoamingAppData, Util.ProgramData, Util.LocalLow,
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
    };

    /// <summary>Never clean a drive root or an important top-level folder, even if a path resolves strangely.</summary>
    public static bool IsSafeDir(string dir)
    {
        try
        {
            var full = Path.GetFullPath(dir).TrimEnd('\\');
            if (full.Length <= 3) return false;
            foreach (var f in Forbidden)
                if (!string.IsNullOrEmpty(f) && string.Equals(full, f.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        catch { return false; }
    }

    static readonly EnumerationOptions Enum = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint, // never follow junctions/symlinks out of the folder
        ReturnSpecialDirectories = false,
    };
    static readonly EnumerationOptions EnumTop = new()
    {
        IgnoreInaccessible = true, RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint,
    };

    static IEnumerable<FileInfo> FilesOf(CleanTarget t)
    {
        if (!Directory.Exists(t.Dir)) yield break;
        // A protected folder (like C:\Windows) may only be used with a specific, non-recursive file pattern
        if (!IsSafeDir(t.Dir) && (t.Recursive || t.Pattern == "*")) yield break;
        var di = new DirectoryInfo(t.Dir);
        if ((di.Attributes & FileAttributes.ReparsePoint) != 0) yield break;
        IEnumerable<FileInfo> files;
        try { files = di.EnumerateFiles(t.Pattern, t.Recursive ? Enum : EnumTop); }
        catch { yield break; }
        var cutoff = DateTime.Now.AddHours(-t.MinAgeHours);
        using var e = files.GetEnumerator();
        while (true)
        {
            FileInfo f;
            try { if (!e.MoveNext()) break; f = e.Current; }
            catch { break; }
            if (t.MinAgeHours > 0)
            {
                try { if (f.LastWriteTime > cutoff) continue; } catch { continue; }
            }
            yield return f;
        }
    }

    // ---------------- Scan / Clean ----------------
    public static void Scan(CleanItem item)
    {
        item.Size = 0; item.Files = 0; item.Freed = 0; item.Skipped = 0;
        try
        {
            if (item.CustomScan != null) { item.Size = item.CustomScan(); }
            else
            {
                foreach (var t in SafeTargets(item))
                    foreach (var f in FilesOf(t))
                    {
                        try { item.Size += f.Length; item.Files++; } catch { }
                    }
            }
        }
        catch (Exception ex) { Log.Warn($"Scan {item.Name}: {ex.Message}"); }
        item.Scanned = true;
    }

    static IEnumerable<CleanTarget> SafeTargets(CleanItem item)
    {
        List<CleanTarget> list;
        try { list = item.Targets().Where(t => !string.IsNullOrWhiteSpace(t.Dir)).ToList(); }
        catch { yield break; }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in list)
            if (seen.Add(t.Dir + "|" + t.Pattern)) yield return t;
    }

    public static void Clean(CleanItem item, StreamWriter deletedList)
    {
        item.Freed = 0; item.Skipped = 0;
        try
        {
            if (item.CustomClean != null)
            {
                item.Freed = item.CustomClean(item);
            }
            else
            {
                foreach (var t in SafeTargets(item))
                {
                    foreach (var f in FilesOf(t).ToList())
                    {
                        try
                        {
                            long len = f.Length;
                            if ((f.Attributes & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden)) != 0)
                                f.Attributes = FileAttributes.Normal;
                            f.Delete();
                            item.Freed += len;
                            deletedList?.WriteLine(f.FullName);
                        }
                        catch { item.Skipped++; }
                    }
                    if (t.Recursive && t.Pattern == "*") RemoveEmptyDirs(t.Dir);
                }
            }
            Log.Ok($"تنظيف [{item.Name}] → تم تحرير {Util.Size(item.Freed)}" + (item.Skipped > 0 ? $" (تخطي {item.Skipped} ملف قيد الاستخدام)" : ""));
        }
        catch (Exception ex) { Log.Error($"Clean {item.Name}: {ex.Message}"); }
        item.Size = Math.Max(0, item.Size - item.Freed);
    }

    static void RemoveEmptyDirs(string root)
    {
        if (!Directory.Exists(root) || !IsSafeDir(root)) return;
        try
        {
            var dirs = new DirectoryInfo(root).EnumerateDirectories("*", Enum).OrderByDescending(d => d.FullName.Length).ToList();
            foreach (var d in dirs)
            {
                try { if (!d.EnumerateFileSystemInfos().Any()) d.Delete(false); } catch { }
            }
        }
        catch { }
    }

    public static bool AnyRunning(CleanItem item, out List<Process> procs)
    {
        procs = new List<Process>();
        foreach (var n in item.Processes)
            try { procs.AddRange(Process.GetProcessesByName(n)); } catch { }
        return procs.Count > 0;
    }

    // ---------------- Helpers to build targets ----------------
    static CleanTarget T(string dir, string pattern = "*", bool rec = true, int age = 0) => new(dir, pattern, rec, age);

    static IEnumerable<string> Subdirs(string dir, string pattern = "*")
    {
        try { return Directory.Exists(dir) ? Directory.EnumerateDirectories(dir, pattern, EnumTop).ToList() : Enumerable.Empty<string>(); }
        catch { return Enumerable.Empty<string>(); }
    }

    static readonly string[] ChromiumProfileCaches =
    {
        "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache",
        @"Service Worker\CacheStorage", @"Service Worker\ScriptCache", "Media Cache", "Application Cache",
    };
    static readonly string[] ChromiumRootCaches = { "ShaderCache", "GrShaderCache", "GraphiteDawnCache", @"Crashpad\reports", "component_crx_cache" };

    static IEnumerable<CleanTarget> ChromiumCache(string userData)
    {
        if (!Directory.Exists(userData)) yield break;
        foreach (var r in ChromiumRootCaches) yield return T(Path.Combine(userData, r));
        var profiles = Subdirs(userData).Where(d =>
        {
            var n = Path.GetFileName(d);
            return n == "Default" || n.StartsWith("Profile ") || n == "Guest Profile" || n == "System Profile";
        }).ToList();
        foreach (var p in profiles)
            foreach (var c in ChromiumProfileCaches) yield return T(Path.Combine(p, c));
    }

    static IEnumerable<CleanTarget> ChromiumHistory(string userData)
    {
        foreach (var p in Subdirs(userData).Where(d => { var n = Path.GetFileName(d); return n == "Default" || n.StartsWith("Profile "); }))
            foreach (var f in new[] { "History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal", "Shortcuts", "Shortcuts-journal" })
                yield return T(p, f, false);
    }

    /// <summary>Electron apps (Discord, Slack, VS Code ...) store cache like Chromium.</summary>
    static IEnumerable<CleanTarget> Electron(string root)
    {
        foreach (var c in new[] { "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache", "Crashpad\\reports", "logs" })
            yield return T(Path.Combine(root, c));
    }

    public static string SteamPath()
    {
        try
        {
            var p = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
            if (!string.IsNullOrEmpty(p)) return p.Replace('/', '\\');
            p = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")?.GetValue("InstallPath") as string;
            return p;
        }
        catch { return null; }
    }

    static IEnumerable<string> SteamLibraries()
    {
        var sp = SteamPath();
        if (sp == null) yield break;
        yield return sp;
        var vdf = Path.Combine(sp, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        string[] lines;
        try { lines = File.ReadAllLines(vdf); } catch { yield break; }
        foreach (var line in lines)
        {
            var l = line.Trim();
            if (!l.StartsWith("\"path\"")) continue;
            var parts = l.Split('"', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3) yield return parts[^1].Replace(@"\\", @"\");
        }
    }

    static long DirSize(string dir)
    {
        long s = 0;
        foreach (var f in FilesOf(T(dir))) try { s += f.Length; } catch { }
        return s;
    }

    static long StopServicesAndClean(CleanItem item, string[] services, string dir)
    {
        foreach (var s in services) Util.Run("net", $"stop {s} /y", 60000);
        long freed = 0;
        foreach (var f in FilesOf(T(dir)).ToList())
        {
            try { long l = f.Length; f.Attributes = FileAttributes.Normal; f.Delete(); freed += l; } catch { item.Skipped++; }
        }
        RemoveEmptyDirs(dir);
        foreach (var s in Enumerable.Reverse(services)) Util.Run("net", $"start {s}", 60000);
        return freed;
    }

    static long TakeOwnAndDelete(string dir)
    {
        if (!Directory.Exists(dir)) return 0;
        long size = DirSize(dir);
        Util.Run("takeown.exe", $"/F \"{dir}\" /R /A /D Y", 1800000);
        Util.Run("icacls.exe", $"\"{dir}\" /grant *S-1-5-32-544:F /T /C /Q", 1800000);
        Util.Run("cmd.exe", $"/c rd /s /q \"{dir}\"", 1800000);
        return Directory.Exists(dir) ? size - DirSize(dir) : size;
    }

    // ---------------- Catalogue ----------------
    static List<CleanItem> Build()
    {
        string c0 = Categories[0], c1 = Categories[1], c2 = Categories[2], c3 = Categories[3], c4 = Categories[4], c5 = Categories[5];
        var list = new List<CleanItem>
        {
            // ======== Windows ========
            new() { Id = "user_temp", Category = c0, Name = "ملفات المستخدم المؤقتة (%TEMP%)",
                Description = "ملفات مؤقتة تتركها البرامج والمثبّتات. آمن تماماً – الملفات المستخدمة حالياً يتم تخطيها.",
                Targets = () => new[] { T(Path.GetTempPath()), T(Path.Combine(L, "Temp")) } },
            new() { Id = "win_temp", Category = c0, Name = "ملفات Windows المؤقتة",
                Description = "مجلد C:\\Windows\\Temp – بقايا التحديثات والتثبيتات.",
                Targets = () => new[] { T(Path.Combine(W, "Temp")) } },
            new() { Id = "wu_cache", Category = c0, Name = "كاش تحديثات Windows",
                Description = "ملفات التحديثات التي تم تحميلها وتثبيتها مسبقاً (SoftwareDistribution\\Download). يتم إيقاف خدمة التحديث مؤقتاً ثم إعادتها.",
                Targets = () => new[] { T(Path.Combine(W, "SoftwareDistribution", "Download")) },
                CustomClean = i => StopServicesAndClean(i, new[] { "wuauserv", "bits" }, Path.Combine(W, "SoftwareDistribution", "Download")) },
            new() { Id = "delivery_opt", Category = c0, Name = "ملفات Delivery Optimization",
                Description = "ملفات تحديثات Windows المشاركة مع أجهزة أخرى (P2P).",
                Targets = () => new[] { T(Path.Combine(W, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache")),
                                        T(Path.Combine(W, "SoftwareDistribution", "DeliveryOptimization")) },
                CustomClean = i =>
                {
                    long before = DirSize(Path.Combine(W, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"));
                    Util.PowerShell("Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue", 180000);
                    long after = DirSize(Path.Combine(W, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"));
                    return Math.Max(0, before - after);
                } },
            new() { Id = "wer", Category = c0, Name = "تقارير الأخطاء (Windows Error Reporting)",
                Description = "تقارير الأعطال التي يرسلها Windows لمايكروسوفت.",
                Targets = () => new[] { T(Path.Combine(PD, @"Microsoft\Windows\WER\ReportArchive")), T(Path.Combine(PD, @"Microsoft\Windows\WER\ReportQueue")),
                                        T(Path.Combine(PD, @"Microsoft\Windows\WER\Temp")), T(Path.Combine(L, @"Microsoft\Windows\WER")) } },
            new() { Id = "dumps", Category = c0, Name = "ملفات Crash Dumps",
                Description = "نسخ من الذاكرة عند انهيار برنامج أو النظام (قد تكون بالجيجات).",
                Targets = () => new[] { T(Path.Combine(L, "CrashDumps")), T(Path.Combine(W, "Minidump")), T(W, "MEMORY.DMP", false),
                                        T(Path.Combine(W, "LiveKernelReports"), "*.dmp") } },
            new() { Id = "win_logs", Category = c0, Name = "سجلات Windows (Logs)",
                Description = "ملفات السجل لـ CBS و DISM و Windows Update وغيرها.",
                Targets = () => new[] { T(Path.Combine(W, @"Logs\CBS")), T(Path.Combine(W, @"Logs\DISM")), T(Path.Combine(W, @"Logs\WindowsUpdate")),
                                        T(Path.Combine(W, @"Logs\NetSetup")), T(Path.Combine(W, @"Logs\SIH")), T(Path.Combine(W, @"Logs\waasmedic")),
                                        T(Path.Combine(W, @"SoftwareDistribution\DataStore\Logs")), T(Path.Combine(W, "Panther"), "*.log", false),
                                        T(W, "*.log", false), T(Path.Combine(W, @"System32\LogFiles\Setupcln")) } },
            new() { Id = "inet_cache", Category = c0, Name = "كاش الإنترنت لـ Windows (INetCache)",
                Description = "كاش يستخدمه Windows و Office وبعض البرامج القديمة.",
                Targets = () => new[] { T(Path.Combine(L, @"Microsoft\Windows\INetCache")) } },
            new() { Id = "thumbs", Category = c0, Name = "كاش الصور المصغرة والأيقونات", DefaultOn = false,
                Description = "يُعاد بناؤه تلقائياً. مفيد إذا الأيقونات/الصور المصغرة صارت خربانة.",
                Targets = () => new[] { T(Path.Combine(L, @"Microsoft\Windows\Explorer"), "thumbcache_*.db", false), T(Path.Combine(L, @"Microsoft\Windows\Explorer"), "iconcache_*.db", false) } },
            new() { Id = "prefetch", Category = c0, Name = "ملفات Prefetch", DefaultOn = false, Risk = Risk.Caution,
                Description = "Windows يستخدمها لتسريع فتح البرامج. حذفها يبطئ أول تشغيل للبرامج مؤقتاً – غير منصوح إلا لحل مشاكل.",
                Targets = () => new[] { T(Path.Combine(W, "Prefetch"), "*.pf", false) } },
            new() { Id = "recycle", Category = c0, Name = "سلة المحذوفات", Glyph = "\uE74D",
                Description = "تفريغ سلة المحذوفات لكل الأقراص.",
                CustomScan = () => { var i = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.SHQUERYRBINFO>() }; Native.SHQueryRecycleBin(null, ref i); return i.i64Size; },
                CustomClean = it => { long s = it.Size; Native.SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4); return s; } },
            new() { Id = "dns", Category = c0, Name = "كاش DNS", NoSizeInfo = true,
                Description = "مسح كاش عناوين المواقع – يحل مشاكل اتصال ومواقع ما تفتح.",
                CustomScan = () => 0, CustomClean = _ => { Native.DnsFlushResolverCache(); return 0; } },

            // ======== GPU & Games ========
            new() { Id = "d3d_cache", Category = c1, Name = "كاش DirectX Shader",
                Description = "كاش الشيدر العام لـ DirectX. إذا الألعاب صار فيها تقطيع (stutter) بعد تحديث تعريف، حذفه يحل المشكلة. يُعاد بناؤه تلقائياً.",
                Targets = () => new[] { T(Path.Combine(L, "D3DSCache")) } },
            new() { Id = "nv_cache", Category = c1, Name = "كاش NVIDIA (DX/GL/NV)",
                Description = "كاش الشيدر لكروت NVIDIA. حذفه بعد تحديث التعريف يقلل التقطيع ويحرر مساحة.",
                Targets = () => new[] { T(Path.Combine(L, @"NVIDIA\DXCache")), T(Path.Combine(L, @"NVIDIA\GLCache")), T(Path.Combine(LL, @"NVIDIA\PerDriverVersion\DXCache")),
                                        T(Path.Combine(LL, @"NVIDIA\PerDriverVersion\GLCache")), T(Path.Combine(L, @"NVIDIA Corporation\NV_Cache")), T(Path.Combine(PD, @"NVIDIA Corporation\NV_Cache")) } },
            new() { Id = "nv_installer", Category = c1, Name = "بقايا تثبيت تعريفات NVIDIA",
                Description = "ملفات التعريفات المفكوكة (C:\\NVIDIA) والتحميلات القديمة. لا تؤثر على التعريف المثبت.",
                Targets = () => new[] { T(Path.Combine(SD, "NVIDIA")), T(Path.Combine(PD, @"NVIDIA Corporation\Downloader")), T(Path.Combine(PD, @"NVIDIA Corporation\NVIDIA App\Downloader")) } },
            new() { Id = "amd_cache", Category = c1, Name = "كاش AMD (DX/Vulkan/GL)",
                Description = "كاش الشيدر لكروت AMD Radeon.",
                Targets = () => new[] { T(Path.Combine(L, @"AMD\DxCache")), T(Path.Combine(L, @"AMD\DxcCache")), T(Path.Combine(L, @"AMD\GLCache")),
                                        T(Path.Combine(L, @"AMD\VkCache")), T(Path.Combine(L, @"AMD\OglCache")), T(Path.Combine(LL, @"AMD\DxCache")) } },
            new() { Id = "amd_installer", Category = c1, Name = "بقايا تثبيت تعريفات AMD",
                Description = "مجلد C:\\AMD الذي يبقى بعد تثبيت التعريفات.",
                Targets = () => new[] { T(Path.Combine(SD, "AMD")) } },
            new() { Id = "intel_cache", Category = c1, Name = "كاش Intel Graphics",
                Description = "كاش الشيدر لكروت/معالجات Intel.",
                Targets = () => new[] { T(Path.Combine(LL, @"Intel\ShaderCache")), T(Path.Combine(L, @"Intel\ShaderCache")) } },
            new() { Id = "steam_web", Category = c1, Name = "Steam – كاش المتصفح والسجلات", Processes = new[] { "steam", "steamwebhelper" },
                Description = "كاش واجهة Steam (المتجر والمكتبة) والسجلات وملفات الأعطال. لا يمس الألعاب أو الحفظ.",
                Targets = () =>
                {
                    var sp = SteamPath(); var l = new List<CleanTarget> { T(Path.Combine(L, @"Steam\htmlcache")) };
                    if (sp != null) { l.Add(T(Path.Combine(sp, "logs"))); l.Add(T(Path.Combine(sp, "dumps"))); l.Add(T(Path.Combine(sp, @"appcache\httpcache"))); l.Add(T(Path.Combine(sp, "depotcache"), "*", true, 24 * 7)); }
                    return l;
                } },
            new() { Id = "steam_shader", Category = c1, Name = "Steam – كاش الشيدر للألعاب", DefaultOn = false, Risk = Risk.Caution, Processes = new[] { "steam" },
                Description = "شيدرات الألعاب المحفوظة. حذفه يحل مشاكل التقطيع بعد تحديث التعريف، لكن أول تشغيل للعبة قد يكون أبطأ.",
                Targets = () => SteamLibraries().Select(lib => T(Path.Combine(lib, @"steamapps\shadercache"))) },
            new() { Id = "epic", Category = c1, Name = "Epic Games Launcher – الكاش", Processes = new[] { "EpicGamesLauncher", "EpicWebHelper" },
                Description = "كاش الويب والسجلات للانشر Epic.",
                Targets = () => Subdirs(Path.Combine(L, @"EpicGamesLauncher\Saved"), "webcache*").Select(d => T(d)).Append(T(Path.Combine(L, @"EpicGamesLauncher\Saved\Logs"))) },
            new() { Id = "battlenet", Category = c1, Name = "Battle.net – الكاش", Processes = new[] { "Battle.net", "Agent" },
                Description = "كاش وسجلات Battle.net.",
                Targets = () => new[] { T(Path.Combine(L, @"Battle.net\Cache")), T(Path.Combine(L, @"Battle.net\BrowserCaches")), T(Path.Combine(PD, @"Battle.net\Agent\Logs")), T(Path.Combine(L, @"Blizzard Entertainment\Battle.net\Cache")) } },
            new() { Id = "ea", Category = c1, Name = "EA App – الكاش والسجلات", Processes = new[] { "EADesktop", "EABackgroundService" },
                Description = "كاش وسجلات تطبيق EA.",
                Targets = () => new[] { T(Path.Combine(L, @"Electronic Arts\EA Desktop\Logs")), T(Path.Combine(L, @"Electronic Arts\EA Desktop\CEF\BrowserCache")), T(Path.Combine(PD, @"EA Desktop\Logs")) } },
            new() { Id = "riot", Category = c1, Name = "Riot Client – السجلات", Processes = new[] { "RiotClientServices", "RiotClientUx" },
                Description = "سجلات Riot Client (Valorant / LoL).",
                Targets = () => new[] { T(Path.Combine(L, @"Riot Games\Riot Client\Logs")), T(Path.Combine(L, @"Riot Games\Riot Client\Data\Cache")) } },

            // ======== Browsers ========
            Browser("chrome", "Google Chrome", Path.Combine(L, @"Google\Chrome\User Data"), "chrome"),
            Browser("edge", "Microsoft Edge", Path.Combine(L, @"Microsoft\Edge\User Data"), "msedge"),
            Browser("brave", "Brave", Path.Combine(L, @"BraveSoftware\Brave-Browser\User Data"), "brave"),
            Browser("vivaldi", "Vivaldi", Path.Combine(L, @"Vivaldi\User Data"), "vivaldi"),
            new() { Id = "opera", Category = c2, Name = "Opera / Opera GX – الكاش", Processes = new[] { "opera" },
                Description = "كاش Opera و Opera GX.",
                Targets = () => new[] { "Opera Stable", "Opera GX Stable" }.SelectMany(n => new[]
                {
                    T(Path.Combine(L, "Opera Software", n, "Cache")), T(Path.Combine(R, "Opera Software", n, "Code Cache")), T(Path.Combine(R, "Opera Software", n, "GPUCache")),
                    T(Path.Combine(R, "Opera Software", n, "ShaderCache")), T(Path.Combine(R, "Opera Software", n, "GrShaderCache")), T(Path.Combine(R, "Opera Software", n, @"Service Worker\CacheStorage")),
                    T(Path.Combine(R, "Opera Software", n, "Default", "Code Cache")), T(Path.Combine(R, "Opera Software", n, "Default", "GPUCache")), T(Path.Combine(L, "Opera Software", n, "Default", "Cache")),
                })},
            new() { Id = "firefox", Category = c2, Name = "Firefox – الكاش", Processes = new[] { "firefox" },
                Description = "كاش Firefox (cache2, startupCache, shader).",
                Targets = () => Subdirs(Path.Combine(L, @"Mozilla\Firefox\Profiles")).SelectMany(p => new[] { T(Path.Combine(p, "cache2")), T(Path.Combine(p, "startupCache")), T(Path.Combine(p, "thumbnails")), T(Path.Combine(p, "jumpListCache")) })
                                 .Concat(Subdirs(Path.Combine(R, @"Mozilla\Firefox\Profiles")).Select(p => T(Path.Combine(p, "shader-cache")))) },
            new() { Id = "browser_history", Category = c2, Name = "سجل التصفح (Chrome / Edge / Brave)", DefaultOn = false, Risk = Risk.Caution, Processes = new[] { "chrome", "msedge", "brave" },
                Description = "يحذف سجل المواقع التي زرتها. لا يحذف كلمات السر أو الكوكيز أو المفضلة. أغلق المتصفح أولاً.",
                Targets = () => ChromiumHistory(Path.Combine(L, @"Google\Chrome\User Data")).Concat(ChromiumHistory(Path.Combine(L, @"Microsoft\Edge\User Data"))).Concat(ChromiumHistory(Path.Combine(L, @"BraveSoftware\Brave-Browser\User Data"))) },

            // ======== Apps ========
            new() { Id = "discord", Category = c3, Name = "Discord – الكاش", Processes = new[] { "Discord", "DiscordPTB", "DiscordCanary" },
                Description = "كاش الصور والفيديوهات والـ GPU لديسكورد. ممكن يكون بالجيجات! لا يسجل خروجك.",
                Targets = () => new[] { "discord", "discordptb", "discordcanary" }.SelectMany(n => Electron(Path.Combine(R, n))) },
            new() { Id = "spotify", Category = c3, Name = "Spotify – الكاش", Processes = new[] { "Spotify" },
                Description = "الأغاني المحفوظة مؤقتاً وكاش الواجهة. (لا يحذف الأغاني المحملة للاستماع بدون نت إذا كانت في مكان مخصص).",
                Targets = () => new[] { T(Path.Combine(L, @"Spotify\Data")), T(Path.Combine(L, @"Spotify\Browser\Cache")), T(Path.Combine(L, @"Spotify\Default\Cache")),
                                        T(Path.Combine(L, @"Packages\SpotifyAB.SpotifyMusic_zpdnekdrzrea0\LocalCache\Spotify\Data")) } },
            new() { Id = "teams", Category = c3, Name = "Microsoft Teams – الكاش", Processes = new[] { "ms-teams", "Teams" },
                Description = "كاش Teams الجديد والقديم.",
                Targets = () => Electron(Path.Combine(L, @"Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default")).Concat(Electron(Path.Combine(R, @"Microsoft\Teams"))) },
            new() { Id = "slack", Category = c3, Name = "Slack – الكاش", Processes = new[] { "slack" },
                Description = "كاش Slack.", Targets = () => Electron(Path.Combine(R, "Slack")) },
            new() { Id = "vscode", Category = c3, Name = "VS Code – الكاش", Processes = new[] { "Code" },
                Description = "كاش VS Code والسجلات.",
                Targets = () => Electron(Path.Combine(R, "Code")).Append(T(Path.Combine(R, @"Code\CachedData"))).Append(T(Path.Combine(R, @"Code\CachedExtensionVSIXs"))) },
            new() { Id = "telegram", Category = c3, Name = "Telegram – كاش الوسائط", Processes = new[] { "Telegram" },
                Description = "الصور والفيديوهات المؤقتة في تيليجرام ديسكتوب.",
                Targets = () => new[] { T(Path.Combine(R, @"Telegram Desktop\tdata\user_data\cache")), T(Path.Combine(R, @"Telegram Desktop\tdata\user_data\media_cache")) } },
            new() { Id = "obs", Category = c3, Name = "OBS Studio – السجلات", Processes = new[] { "obs64" },
                Description = "سجلات OBS وتقارير الأعطال.",
                Targets = () => new[] { T(Path.Combine(R, @"obs-studio\logs")), T(Path.Combine(R, @"obs-studio\crashes")) } },
            new() { Id = "adobe", Category = c3, Name = "Adobe – Media Cache", DefaultOn = false,
                Description = "كاش Premiere / After Effects (ممكن يكون كبير جداً).",
                Targets = () => new[] { T(Path.Combine(R, @"Adobe\Common\Media Cache Files")), T(Path.Combine(R, @"Adobe\Common\Media Cache")), T(Path.Combine(L, @"Adobe\Common\Media Cache Files")) } },
            new() { Id = "store_temp", Category = c3, Name = "تطبيقات Microsoft Store – الملفات المؤقتة", DefaultOn = false,
                Description = "ملفات Temp و INetCache داخل تطبيقات المتجر.",
                Targets = () => Subdirs(Path.Combine(L, "Packages")).SelectMany(p => new[] { T(Path.Combine(p, @"AC\Temp")), T(Path.Combine(p, @"AC\INetCache")) }) },
            new() { Id = "dev_cache", Category = c3, Name = "كاش أدوات المبرمجين (npm / pip / NuGet / yarn)", DefaultOn = false,
                Description = "يُعاد تحميله عند الحاجة.",
                Targets = () => new[] { T(Path.Combine(L, "npm-cache")), T(Path.Combine(R, "npm-cache")), T(Path.Combine(L, @"pip\cache")), T(Path.Combine(L, @"NuGet\v3-cache")),
                                        T(Path.Combine(L, @"NuGet\plugins-cache")), T(Path.Combine(L, @"Yarn\Cache")), T(Path.Combine(L, @"pnpm-cache")) } },

            // ======== Privacy ========
            new() { Id = "recent", Category = c4, Name = "الملفات الأخيرة (Recent / Jump Lists)", DefaultOn = false,
                Description = "قائمة الملفات التي فتحتها مؤخراً في Explorer وقوائم شريط المهام.",
                Targets = () => new[] { T(Path.Combine(R, @"Microsoft\Windows\Recent"), "*.lnk", false), T(Path.Combine(R, @"Microsoft\Windows\Recent\AutomaticDestinations")),
                                        T(Path.Combine(R, @"Microsoft\Windows\Recent\CustomDestinations")) } },
            new() { Id = "eventlogs", Category = c4, Name = "سجل أحداث Windows (Event Logs)", DefaultOn = false, NoSizeInfo = true, Risk = Risk.Caution,
                Description = "يمسح كل سجلات الأحداث في Event Viewer. مفيد للخصوصية، لكن يمسح معلومات تفيد بتشخيص المشاكل.",
                CustomScan = () => 0,
                CustomClean = _ =>
                {
                    var (_, outp) = Util.Run("wevtutil.exe", "el", 60000);
                    int n = 0;
                    foreach (var name in outp.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        if (Util.Run("wevtutil.exe", $"cl \"{name}\"", 15000).code == 0) n++;
                    Log.Info($"تم مسح {n} سجل أحداث");
                    return 0;
                } },
            new() { Id = "defender_history", Category = c4, Name = "سجل فحص Windows Defender", DefaultOn = false,
                Description = "سجل نتائج الفحص القديمة (لا يؤثر على الحماية).",
                Targets = () => new[] { T(Path.Combine(PD, @"Microsoft\Windows Defender\Scans\History\Service"), "*", true, 24) } },

            // ======== Advanced ========
            new() { Id = "upgrade_leftovers", Category = c5, Name = "بقايا ترقية Windows ($WINDOWS.~BT ...)", DefaultOn = false, Risk = Risk.Caution,
                Description = "ملفات تبقى بعد ترقية Windows أو أداة التحديث.",
                Targets = () => new[] { T(Path.Combine(SD, "$WINDOWS.~BT")), T(Path.Combine(SD, "$Windows.~WS")), T(Path.Combine(SD, "$GetCurrent")), T(Path.Combine(SD, "Windows10Upgrade")), T(Path.Combine(SD, "ESD")) } },
            new() { Id = "windows_old", Category = c5, Name = "مجلد Windows.old", DefaultOn = false, Risk = Risk.Caution,
                Description = "نسخة Windows القديمة بعد التحديث (عادة 10-30 جيجا). بعد حذفه لن تقدر ترجع للنسخة السابقة.",
                Targets = () => new[] { T(Path.Combine(SD, "Windows.old")) },
                CustomClean = _ => TakeOwnAndDelete(Path.Combine(SD, "Windows.old")) },
            new() { Id = "component_store", Category = c5, Name = "تنظيف مخزن مكونات Windows (WinSxS)", DefaultOn = false, NoSizeInfo = true,
                Description = "يستخدم أداة DISM الرسمية لحذف نسخ التحديثات القديمة. يأخذ من 5 إلى 20 دقيقة.",
                CustomScan = () => 0,
                CustomClean = _ => { var r = Util.Run("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /NoRestart", 3600000); Log.Info("DISM: exit " + r.code); return 0; } },
        };
        return list;
    }

    static CleanItem Browser(string id, string name, string userData, string proc) => new()
    {
        Id = id, Category = Categories[2], Name = $"{name} – الكاش", Processes = new[] { proc },
        Description = $"كاش الصفحات والصور والشيدر في {name}. لا يحذف كلمات السر أو الكوكيز أو السجل.",
        Targets = () => ChromiumCache(userData),
    };

    /// <summary>Find the clean items that belong to a running process name (for the Processes page).</summary>
    public static CleanItem ForProcess(string processName)
    {
        return Items.FirstOrDefault(i => i.Category != Categories[0] && i.Processes.Any(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase)));
    }
}
