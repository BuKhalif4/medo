using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace MedoBoost.Core;

/// <summary>
/// FPS counter based on Windows ETW events - the same technique PresentMon / CapFrameX use.
/// Nothing is injected into games (no anti-cheat problems): we only listen to the
/// "frame presented" events Windows already emits for every DirectX / Vulkan / OpenGL app.
/// </summary>
public sealed class FpsMonitor : IDisposable
{
    public static FpsMonitor Instance { get; } = new();

    static readonly Guid DXGI = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");    // Present_Start = 42
    static readonly Guid D3D9 = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");    // Present_Start = 1
    static readonly Guid DxgKrnl = new("802EC45A-1E99-4B83-9920-87C98277BA9D"); // Present_Info = 184 (Vulkan/OpenGL fallback)
    const string SessionName = "MedoBoost-FPS";

    class PidStats
    {
        public readonly Queue<double> Dx = new();   // timestamps (ms) of DXGI/D3D9 presents in last 1s
        public readonly Queue<double> Kr = new();   // DxgKrnl presents in last 1s
        public double LastDx = -1, LastKr = -1;
        public double LastFrameMs;
        public long LastSeen;
    }

    readonly object gate = new();
    readonly Dictionary<int, PidStats> stats = new();
    TraceEventSession session;
    Thread thread;
    public bool Running { get; private set; }
    public string Error { get; private set; }

    // Benchmark capture
    List<double> benchFrames;
    int benchPid;
    public bool Benchmarking => benchFrames != null;

    public void Start()
    {
        if (Running) return;
        try
        {
            if (!(TraceEventSession.IsElevated() ?? false)) { Error = "يلزم تشغيل البرنامج كمسؤول"; return; }
            try { using var old = new TraceEventSession(SessionName, TraceEventSessionOptions.Attach); old.Stop(); } catch { }
            session = new TraceEventSession(SessionName) { StopOnDispose = true, BufferSizeMB = 16 };
            session.EnableProvider(DXGI, TraceEventLevel.Informational, ulong.MaxValue);
            session.EnableProvider(D3D9, TraceEventLevel.Informational, ulong.MaxValue);
            session.EnableProvider(DxgKrnl, TraceEventLevel.Informational, 0x8000000); // "Present" keyword only
            session.Source.AllEvents += OnEvent;
            thread = new Thread(() => { try { session.Source.Process(); } catch (Exception ex) { Error = ex.Message; } })
            { IsBackground = true, Name = "FpsEtw" };
            thread.Start();
            Running = true;
            Log.Info("FPS monitor started (ETW).");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Log.Warn("FPS monitor failed: " + ex.Message);
        }
    }

    void OnEvent(TraceEvent e)
    {
        bool dx;
        int id = (int)e.ID;
        if (e.ProviderGuid == DXGI) { if (id != 42) return; dx = true; }
        else if (e.ProviderGuid == D3D9) { if (id != 1) return; dx = true; }
        else if (e.ProviderGuid == DxgKrnl) { if (id != 184) return; dx = false; }
        else return;

        int pid = e.ProcessID;
        double t = e.TimeStampRelativeMSec;
        lock (gate)
        {
            if (!stats.TryGetValue(pid, out var st)) stats[pid] = st = new PidStats();
            st.LastSeen = Environment.TickCount64;
            var q = dx ? st.Dx : st.Kr;
            q.Enqueue(t);
            while (q.Count > 0 && t - q.Peek() > 1000) q.Dequeue();
            double last = dx ? st.LastDx : st.LastKr;
            // Use DXGI/D3D9 frame times when available, otherwise kernel ones
            bool primary = dx || st.Dx.Count == 0;
            if (last >= 0 && primary)
            {
                double ft = t - last;
                if (ft > 0 && ft < 1000)
                {
                    st.LastFrameMs = ft;
                    if (benchFrames != null && pid == benchPid) benchFrames.Add(ft);
                }
            }
            if (dx) st.LastDx = t; else st.LastKr = t;
        }
    }

    /// <summary>FPS of a process during the last second (0 if it is not rendering).</summary>
    public (double fps, double frameMs) Get(int pid)
    {
        lock (gate)
        {
            if (!stats.TryGetValue(pid, out var st)) return (0, 0);
            if (Environment.TickCount64 - st.LastSeen > 1500) return (0, 0);
            var q = st.Dx.Count > 0 ? st.Dx : st.Kr;
            return (q.Count, st.LastFrameMs);
        }
    }

    /// <summary>The process currently in front (the game you're playing).</summary>
    public (int pid, double fps, double frameMs) Foreground()
    {
        int pid = Native.ForegroundPid();
        var (fps, ft) = Get(pid);
        return (pid, fps, ft);
    }

    public void BeginBenchmark(int pid)
    {
        lock (gate) { benchPid = pid; benchFrames = new List<double>(20000); }
    }

    public BenchResult EndBenchmark(string name)
    {
        List<double> f;
        lock (gate) { f = benchFrames; benchFrames = null; }
        if (f == null || f.Count < 10) return null;
        var sorted = f.OrderByDescending(x => x).ToList();
        double total = f.Sum();
        int n1 = Math.Max(1, f.Count / 100), n01 = Math.Max(1, f.Count / 1000);
        return new BenchResult
        {
            Name = name,
            Time = DateTime.Now,
            Frames = f.Count,
            Seconds = total / 1000,
            AvgFps = f.Count / (total / 1000),
            Low1 = 1000 / sorted.Take(n1).Average(),
            Low01 = 1000 / sorted.Take(n01).Average(),
            MaxFrameMs = sorted[0],
        };
    }

    public void Dispose()
    {
        try { session?.Dispose(); } catch { }
        Running = false;
    }
}

public class BenchResult
{
    public string Name { get; set; }
    public DateTime Time { get; set; }
    public int Frames { get; set; }
    public double Seconds { get; set; }
    public double AvgFps { get; set; }
    public double Low1 { get; set; }
    public double Low01 { get; set; }
    public double MaxFrameMs { get; set; }
}
