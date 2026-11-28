using System.Diagnostics;
using System.Net.NetworkInformation;
using LibreHardwareMonitor.Hardware;

namespace MedoBoost.Core;

public class HwSnapshot
{
    public double CpuLoad;          // %
    public double CpuTemp = double.NaN;
    public double CpuClock = double.NaN; // MHz
    public double CpuPower = double.NaN; // W
    public double GpuLoad = double.NaN;
    public double GpuTemp = double.NaN;
    public double GpuHotSpot = double.NaN;
    public double GpuClock = double.NaN;
    public double GpuPower = double.NaN;
    public double GpuFan = double.NaN;
    public double VramUsedMb = double.NaN;
    public double VramTotalMb = double.NaN;
    public double RamLoad;
    public ulong RamTotal, RamAvail;
    public double NetDownBps, NetUpBps;
    public Dictionary<int, double> GpuByPid = new();
}

/// <summary>
/// Reads CPU / GPU / RAM / temperatures once per second on a background thread.
/// - CPU load: GetSystemTimes (same source as Task Manager)
/// - GPU load per process: Windows "GPU Engine" performance counters (same source as Task Manager)
/// - Temperatures / clocks / power: LibreHardwareMonitor (open source). CPU temps need the signed PawnIO driver.
/// </summary>
public sealed class HardwareMonitor : IDisposable
{
    public static HardwareMonitor Instance { get; } = new();
    public event Action<HwSnapshot> Updated;
    public HwSnapshot Last { get; private set; } = new();
    public string CpuName { get; private set; } = "";
    public string GpuName { get; private set; } = "";
    public bool CpuTempAvailable { get; private set; }
    public bool PawnIoInstalled { get; private set; }

    Thread thread;
    volatile bool running;
    Computer computer;
    IHardware cpuHw, gpuHw;
    ulong prevIdle, prevKernel, prevUser;
    readonly Dictionary<string, CounterSample> gpuPrev = new();
    long prevRx = -1, prevTx = -1; DateTime prevNetTime;

    public void Start()
    {
        if (running) return;
        running = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "HwMonitor", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    void InitLhm()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            PawnIoInstalled = k != null;
        }
        catch { }
        try
        {
            computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            computer.Open();
            foreach (var h in computer.Hardware)
            {
                if (h.HardwareType == HardwareType.Cpu && cpuHw == null) cpuHw = h;
                if (h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                    gpuHw ??= h;
            }
            if (gpuHw == null)
                gpuHw = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel);
            CpuName = cpuHw?.Name ?? "";
            GpuName = gpuHw?.Name ?? "";
        }
        catch (Exception ex) { Log.Warn("Sensors (LibreHardwareMonitor) unavailable: " + ex.Message); computer = null; }
    }

    void Loop()
    {
        InitLhm();
        while (running)
        {
            var sw = Stopwatch.StartNew();
            var s = new HwSnapshot();
            try { ReadCpuLoad(s); } catch { }
            try { ReadRam(s); } catch { }
            try { ReadGpuCounters(s); } catch { }
            try { ReadLhm(s); } catch { }
            try { ReadNet(s); } catch { }
            Last = s;
            try { Updated?.Invoke(s); } catch { }
            int wait = 1000 - (int)sw.ElapsedMilliseconds;
            if (wait > 0) Thread.Sleep(wait);
        }
    }

    void ReadCpuLoad(HwSnapshot s)
    {
        if (!Native.GetSystemTimes(out var idle, out var kernel, out var user)) return;
        ulong i = idle.Value, k = kernel.Value, u = user.Value;
        if (prevKernel != 0)
        {
            ulong di = i - prevIdle, dk = k - prevKernel, du = u - prevUser;
            ulong total = dk + du;
            s.CpuLoad = total == 0 ? 0 : Math.Clamp((total - di) * 100.0 / total, 0, 100);
        }
        prevIdle = i; prevKernel = k; prevUser = u;
    }

    static void ReadRam(HwSnapshot s)
    {
        var m = new Native.MEMORYSTATUSEX();
        if (Native.GlobalMemoryStatusEx(m))
        {
            s.RamTotal = m.ullTotalPhys; s.RamAvail = m.ullAvailPhys;
            s.RamLoad = (m.ullTotalPhys - m.ullAvailPhys) * 100.0 / m.ullTotalPhys;
        }
    }

    void ReadGpuCounters(HwSnapshot s)
    {
        if (!PerformanceCounterCategory.Exists("GPU Engine")) return;
        var cat = new PerformanceCounterCategory("GPU Engine");
        var data = cat.ReadCategory();
        var util = data["Utilization Percentage"];
        if (util == null) return;
        var perEngine = new Dictionary<string, double>();
        var perPid = new Dictionary<int, double>();
        var seen = new HashSet<string>();
        foreach (InstanceData inst in util.Values)
        {
            string name = inst.InstanceName;
            seen.Add(name);
            var cur = inst.Sample;
            if (gpuPrev.TryGetValue(name, out var prev))
            {
                double v = CounterSample.Calculate(prev, cur);
                if (v > 0 && !double.IsNaN(v))
                {
                    // pid_1234_luid_0x..._0x..._phys_0_eng_3_engtype_3D
                    int pid = 0;
                    var parts = name.Split('_');
                    if (parts.Length > 1) int.TryParse(parts[1], out pid);
                    int engIdx = name.IndexOf("_phys_", StringComparison.Ordinal);
                    int luidIdx = name.IndexOf("luid_", StringComparison.Ordinal);
                    string engineKey = (luidIdx >= 0 && engIdx > luidIdx) ? name.Substring(luidIdx) : name;
                    perEngine[engineKey] = perEngine.GetValueOrDefault(engineKey) + v;
                    perPid[pid] = Math.Max(perPid.GetValueOrDefault(pid), v);
                }
            }
            gpuPrev[name] = cur;
        }
        foreach (var k in gpuPrev.Keys.Where(k => !seen.Contains(k)).ToList()) gpuPrev.Remove(k);
        s.GpuLoad = perEngine.Count == 0 ? 0 : Math.Min(100, perEngine.Values.Max());
        s.GpuByPid = perPid;
    }

    void ReadLhm(HwSnapshot s)
    {
        if (computer == null) return;
        if (cpuHw != null)
        {
            cpuHw.Update();
            double temp = double.NaN; int prio = 99;
            var clocks = new List<double>();
            foreach (var sn in cpuHw.Sensors)
            {
                if (sn.Value is not float v) continue;
                switch (sn.SensorType)
                {
                    case SensorType.Temperature:
                        int p = sn.Name switch
                        {
                            "Core (Tctl/Tdie)" => 0, "CPU Package" => 0, "Package" => 1, "Core (Tdie)" => 1, "Core (Tctl)" => 2,
                            "Core Average" => 3, "Core Max" => 4, _ => sn.Name.StartsWith("CCD") ? 5 : 9
                        };
                        if (p < prio && v > 0 && v < 125) { prio = p; temp = v; }
                        break;
                    case SensorType.Clock when sn.Name.StartsWith("Core #") || sn.Name.StartsWith("P-Core") || sn.Name.StartsWith("E-Core"):
                        if (v > 0) clocks.Add(v); break;
                    case SensorType.Power when sn.Name is "Package" or "CPU Package":
                        s.CpuPower = v; break;
                }
            }
            s.CpuTemp = temp;
            CpuTempAvailable = !double.IsNaN(temp);
            if (clocks.Count > 0) s.CpuClock = clocks.Average();
        }
        if (gpuHw != null)
        {
            gpuHw.Update();
            foreach (var sn in gpuHw.Sensors)
            {
                if (sn.Value is not float v) continue;
                switch (sn.SensorType)
                {
                    case SensorType.Temperature when sn.Name == "GPU Core": s.GpuTemp = v; break;
                    case SensorType.Temperature when sn.Name.Contains("Hot Spot"): s.GpuHotSpot = v; break;
                    case SensorType.Clock when sn.Name == "GPU Core": s.GpuClock = v; break;
                    case SensorType.Power when sn.Name is "GPU Package" or "GPU Power" or "GPU Core": if (double.IsNaN(s.GpuPower)) s.GpuPower = v; break;
                    case SensorType.SmallData when sn.Name is "GPU Memory Used" or "D3D Dedicated Memory Used": if (double.IsNaN(s.VramUsedMb)) s.VramUsedMb = v; break;
                    case SensorType.SmallData when sn.Name == "GPU Memory Total": s.VramTotalMb = v; break;
                    case SensorType.Fan: if (double.IsNaN(s.GpuFan)) s.GpuFan = v; break;
                    case SensorType.Load when sn.Name == "GPU Core" && (s.GpuLoad is double.NaN || s.GpuLoad == 0): s.GpuLoad = v; break;
                }
            }
            if (double.IsNaN(s.GpuTemp))
            {
                var t = gpuHw.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Temperature && x.Value is > 0);
                if (t != null) s.GpuTemp = t.Value.Value;
            }
        }
    }

    void ReadNet(HwSnapshot s)
    {
        long rx = 0, tx = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var st = ni.GetIPStatistics();
            rx += st.BytesReceived; tx += st.BytesSent;
        }
        var now = DateTime.UtcNow;
        if (prevRx >= 0)
        {
            double dt = (now - prevNetTime).TotalSeconds;
            if (dt > 0) { s.NetDownBps = Math.Max(0, (rx - prevRx) / dt); s.NetUpBps = Math.Max(0, (tx - prevTx) / dt); }
        }
        prevRx = rx; prevTx = tx; prevNetTime = now;
    }

    public void Dispose()
    {
        running = false;
        try { computer?.Close(); } catch { }
    }
}
