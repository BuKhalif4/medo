using System.Management;
using Microsoft.Win32;

namespace MedoBoost.Core;

/// <summary>Full PC specs (like Windows Settings → System → About), read locally via WMI / registry.</summary>
public class SystemSpecs
{
    public string Cpu = "—", CpuSub = "";
    public string Ram = "—", RamSub = "";
    public string Gpu = "—", GpuSub = "";
    public string Storage = "—", StorageSub = "", StorageDetail = "";
    public string Board = "—", BoardSub = "";
    public string Display = "—", DisplaySub = "";
    public string Os = "";

    public override string ToString() =>
        $"المعالج: {Cpu} ({CpuSub})\nالرام: {Ram} ({RamSub})\nكرت الشاشة: {Gpu} ({GpuSub})\nالتخزين: {Storage} ({StorageSub})\n{StorageDetail}\nاللوحة الأم: {Board} {BoardSub}\nالشاشة: {Display} {DisplaySub}\nالنظام: {Os}";
}

public static class SystemInfo
{
    static IEnumerable<ManagementObject> Query(string wql, string scope = null)
    {
        var list = new List<ManagementObject>();
        try
        {
            using var s = scope == null ? new ManagementObjectSearcher(wql) : new ManagementObjectSearcher(scope, wql);
            foreach (ManagementObject o in s.Get()) list.Add(o);
        }
        catch (Exception ex) { Log.Warn($"WMI {wql}: {ex.Message}"); }
        return list;
    }

    static string S(ManagementObject o, string p) { try { return o[p]?.ToString()?.Trim() ?? ""; } catch { return ""; } }
    static long L(ManagementObject o, string p) { try { return Convert.ToInt64(o[p] ?? 0); } catch { return 0; } }

    static string Clean(string s) => System.Text.RegularExpressions.Regex.Replace(
        s.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "").Replace(" CPU", "").Replace("  ", " "), @"\s*@.*$", "").Trim();

    static string GB(long bytes) => bytes >= 1_000_000_000_000 ? $"{bytes / 1e12:0.##} TB" : $"{bytes / 1e9:0} GB";

    public static SystemSpecs Read()
    {
        var r = new SystemSpecs();

        // ---- CPU
        var cpu = Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor").FirstOrDefault();
        if (cpu != null)
        {
            r.Cpu = Clean(S(cpu, "Name"));
            r.CpuSub = $"{L(cpu, "NumberOfCores")} أنوية • {L(cpu, "NumberOfLogicalProcessors")} مسار • {L(cpu, "MaxClockSpeed") / 1000.0:0.0#} GHz";
        }

        // ---- RAM
        var sticks = Query("SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, Manufacturer FROM Win32_PhysicalMemory").ToList();
        if (sticks.Count > 0)
        {
            long total = sticks.Sum(s => L(s, "Capacity"));
            long speed = sticks.Max(s => Math.Max(L(s, "ConfiguredClockSpeed"), 0));
            if (speed == 0) speed = sticks.Max(s => L(s, "Speed"));
            string type = L(sticks[0], "SMBIOSMemoryType") switch { 20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", 34 => "DDR5", 35 => "LPDDR5", 30 => "LPDDR4", _ => "" };
            int slots = (int)Query("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray").Sum(a => L(a, "MemoryDevices"));
            r.Ram = $"{total / 1073741824.0:0.#} GB";
            r.RamSub = string.Join(" • ", new[]
            {
                type, speed > 0 ? $"{speed} MT/s" : "",
                slots > 0 ? $"{sticks.Count} من {slots} منافذ" : $"{sticks.Count} شريحة",
            }.Where(x => x.Length > 0));
        }

        // ---- GPU (prefer the discrete one)
        var gpus = Query("SELECT Name, DriverVersion, AdapterRAM, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController").ToList();
        var gpu = gpus.OrderByDescending(g => { var n = S(g, "Name"); return n.Contains("NVIDIA") || n.Contains("Radeon") || n.Contains("Arc") ? 2 : n.Contains("Basic") ? 0 : 1; }).FirstOrDefault();
        if (gpu != null)
        {
            r.Gpu = S(gpu, "Name").Replace("(R)", "").Replace("(TM)", "");
            long vram = VramBytes(r.Gpu);
            if (vram <= 0) vram = L(gpu, "AdapterRAM");
            string drv = S(gpu, "DriverVersion");
            if (r.Gpu.Contains("NVIDIA") && drv.Count(c => c == '.') == 3)
            {
                var digits = string.Concat(drv.Split('.').Skip(2)).Replace(".", "");
                if (digits.Length >= 5) { var d5 = digits[^5..]; drv = $"{d5[..3]}.{d5[3..]}"; }
            }
            r.GpuSub = string.Join(" • ", new[] { vram > 0 ? $"{vram / 1073741824.0:0.#} GB VRAM" : "", drv.Length > 0 ? "تعريف " + drv : "" }.Where(x => x.Length > 0));
            // ---- display
            long w = L(gpu, "CurrentHorizontalResolution"), h = L(gpu, "CurrentVerticalResolution"), hz = L(gpu, "CurrentRefreshRate");
            if (w > 0) { r.Display = $"{w}×{h}"; r.DisplaySub = hz > 0 ? $"{hz} Hz" : ""; }
        }
        var mon = Query("SELECT UserFriendlyName FROM WmiMonitorID", @"root\wmi").FirstOrDefault();
        if (mon != null)
        {
            try
            {
                if (mon["UserFriendlyName"] is ushort[] chars)
                {
                    var name = new string(chars.TakeWhile(c => c != 0).Select(c => (char)c).ToArray()).Trim();
                    if (name.Length > 0) r.DisplaySub = string.Join(" • ", new[] { r.DisplaySub, name }.Where(x => x.Length > 0));
                }
            }
            catch { }
        }

        // ---- storage (like Windows: all fixed drives)
        long tot = 0, used = 0;
        foreach (var d in DriveInfo.GetDrives())
        {
            try { if (d.DriveType == DriveType.Fixed && d.IsReady) { tot += d.TotalSize; used += d.TotalSize - d.TotalFreeSpace; } } catch { }
        }
        if (tot > 0) { r.Storage = GB(tot); r.StorageSub = $"{GB(used)} مستخدم من {GB(tot)}"; }
        var disks = Query("SELECT FriendlyName, MediaType, BusType, Size FROM MSFT_PhysicalDisk", @"root\Microsoft\Windows\Storage")
            .Select(d =>
            {
                string media = L(d, "MediaType") switch { 4 => "SSD", 3 => "HDD", _ => "" };
                if (L(d, "BusType") == 17) media = "NVMe SSD";
                return $"{S(d, "FriendlyName")} – {GB(L(d, "Size"))} {media}".Trim();
            }).ToList();
        r.StorageDetail = string.Join("\n", disks);

        // ---- motherboard
        var bb = Query("SELECT Manufacturer, Product FROM Win32_BaseBoard").FirstOrDefault();
        if (bb != null)
        {
            r.Board = S(bb, "Product");
            r.BoardSub = S(bb, "Manufacturer").Replace("ASUSTeK COMPUTER INC.", "ASUS").Replace("Micro-Star International Co., Ltd.", "MSI").Replace("Gigabyte Technology Co., Ltd.", "Gigabyte");
            if (r.Board.Length == 0) r.Board = "—";
        }

        // ---- OS
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var name = k?.GetValue("ProductName") as string ?? "Windows";
            var build = k?.GetValue("CurrentBuild") as string ?? "";
            if (int.TryParse(build, out var b) && b >= 22000) name = name.Replace("Windows 10", "Windows 11");
            r.Os = $"{name} {k?.GetValue("DisplayVersion")} (Build {build})";
        }
        catch { }
        return r;
    }

    /// <summary>Real VRAM size from the driver's registry entry (WMI AdapterRAM caps at 4 GB).</summary>
    static long VramBytes(string gpuName)
    {
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (cls == null) return 0;
            foreach (var sub in cls.GetSubKeyNames())
            {
                if (!sub.All(char.IsDigit)) continue;
                using var k = cls.OpenSubKey(sub);
                var desc = k?.GetValue("DriverDesc") as string ?? "";
                if (!gpuName.Contains(desc.Replace("(R)", "").Replace("(TM)", ""), StringComparison.OrdinalIgnoreCase) && !desc.Contains(gpuName, StringComparison.OrdinalIgnoreCase)) continue;
                var v = k.GetValue("HardwareInformation.qwMemorySize");
                if (v is long l) return l;
                if (v is byte[] bytes && bytes.Length >= 8) return BitConverter.ToInt64(bytes, 0);
                if (v is int i) return (uint)i;
            }
        }
        catch { }
        return 0;
    }
}
