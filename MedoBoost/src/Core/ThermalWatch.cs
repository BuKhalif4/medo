namespace MedoBoost.Core;

/// <summary>Warns when the CPU or GPU stays too hot for ~6 seconds. At most one warning per part every 10 minutes.</summary>
public static class ThermalWatch
{
    static int cpuHot, gpuHot;
    static DateTime lastCpu = DateTime.MinValue, lastGpu = DateTime.MinValue;

    public static void Start() => HardwareMonitor.Instance.Updated += OnUpdate;

    static void OnUpdate(HwSnapshot s)
    {
        var st = Settings.Current;
        if (!st.ThermalAlerts) return;
        Check(s.CpuTemp, st.CpuTempLimit, ref cpuHot, ref lastCpu, "المعالج");
        Check(s.GpuTemp, st.GpuTempLimit, ref gpuHot, ref lastGpu, "كرت الشاشة");
    }

    static void Check(double temp, int limit, ref int hot, ref DateTime last, string name)
    {
        if (double.IsNaN(temp) || temp < limit) { hot = 0; return; }
        if (++hot < 6 || (DateTime.Now - last).TotalMinutes < 10) return;
        hot = 0; last = DateTime.Now;
        Notify.Push("warn", $"حرارة {name} مرتفعة 🔥", $"وصلت {temp:0} درجة مئوية منذ ثواني. افحص التهوية والمراوح، وإذا بتلعب خفّض الإعدادات.", balloon: true);
        Log.Warn($"حرارة {name}: {temp:0}°C");
    }
}
