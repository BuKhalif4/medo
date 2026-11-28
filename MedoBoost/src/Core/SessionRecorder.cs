using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace MedoBoost.Core;

public class SessionSample
{
    public int T { get; set; }          // seconds since the session started
    public double Fps { get; set; }
    public double Cpu { get; set; }
    public double CpuT { get; set; }
    public double Gpu { get; set; }
    public double GpuT { get; set; }
    public double Ram { get; set; }
    public int St { get; set; }         // stutters (frames >= 50 ms) in this second
    public double Max { get; set; }     // longest frame (ms) in this second
}

public class Finding
{
    public string Level { get; set; } = "info";   // ok | info | warn | bad
    public string Text { get; set; } = "";
}

public class SessionRecord
{
    public string Id { get; set; } = "";
    public string Game { get; set; } = "";
    public string Exe { get; set; } = "";
    public DateTime Start { get; set; }
    public int Seconds { get; set; }
    public bool GameModeOn { get; set; }
    public double AvgFps { get; set; }
    public double Low1 { get; set; }
    public double Low01 { get; set; }
    public double MaxFrameMs { get; set; }
    public int Stutters { get; set; }
    public double AvgCpu { get; set; }
    public double AvgGpu { get; set; }
    public double AvgRam { get; set; }
    public double MaxCpuT { get; set; }
    public double MaxGpuT { get; set; }
    public List<Finding> Findings { get; set; } = new();
    public List<SessionSample> Samples { get; set; } = new();
}

/// <summary>
/// Records every play session on its own: it notices a game (steady frames from the front window), samples FPS and
/// the PC's load/temperatures once a second, and when the game closes it saves the session with a plain-language diagnosis.
/// Nothing is injected into the game and nothing leaves the PC.
/// </summary>
public static class SessionRecorder
{
    public const int MinSeconds = 60;          // shorter runs are not worth keeping
    public const int KeepMax = 200;            // oldest sessions are deleted past this

    public static event Action Changed;
    public static bool Recording => cur != null;
    public static string CurrentGame => cur?.Game ?? "";
    public static int CurrentSeconds => cur == null ? 0 : (int)(DateTime.Now - t0).TotalSeconds;
    public static string Dir => Path.Combine(Util.DataDir, "sessions");

    static DispatcherTimer timer;
    static SessionRecord cur;
    static List<SessionSample> samples;
    static DateTime t0;
    static int pid, streak, idle;
    static bool saving;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static void Start()
    {
        if (timer != null) return;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    static void Tick()
    {
        try
        {
            if (cur == null)
            {
                if (!Settings.Current.RecordSessions || saving) { streak = 0; return; }
                var (p, fps, _) = FpsMonitor.Instance.Foreground();
                if (p <= 4 || p == Environment.ProcessId || fps < 30) { streak = 0; return; }
                string name, path;
                try { using var pr = Process.GetProcessById(p); name = pr.ProcessName; path = Native.ProcessPath(p) ?? ""; }
                catch { streak = 0; return; }
                if (GameMode.NotGames.Contains(name) || path.StartsWith(Util.WinDir, StringComparison.OrdinalIgnoreCase)) { streak = 0; return; }
                if (++streak < 5) return;
                streak = 0;
                Begin(p, name);
                return;
            }

            if (!IsRunning(pid)) { End(); return; }
            var (f, _) = FpsMonitor.Instance.Get(pid);
            if (f <= 0)
            {
                // menu / minimised / loading screen: no frames. Close the session after 10 s of nothing.
                if (++idle >= 10) End();
                return;
            }
            idle = 0;
            var hw = HardwareMonitor.Instance.Last;
            var (st, mx) = FpsMonitor.Instance.TakeWindow();
            samples.Add(new SessionSample
            {
                T = (int)(DateTime.Now - t0).TotalSeconds,
                Fps = Math.Round(f, 1),
                Cpu = Math.Round(hw.CpuLoad, 1), CpuT = Clean(hw.CpuTemp),
                Gpu = Clean(hw.GpuLoad), GpuT = Clean(hw.GpuTemp),
                Ram = Math.Round(hw.RamLoad, 1),
                St = st, Max = Math.Round(mx, 1),
            });
        }
        catch (Exception ex) { Log.Warn("SessionRecorder: " + ex.Message); }
    }

    static double Clean(double v) => double.IsNaN(v) || double.IsInfinity(v) ? 0 : Math.Round(v, 1);
    static bool IsRunning(int id) { try { using var p = Process.GetProcessById(id); return !p.HasExited; } catch { return false; } }

    static void Begin(int p, string name)
    {
        pid = p; idle = 0; t0 = DateTime.Now;
        samples = new List<SessionSample>(3600);
        cur = new SessionRecord { Game = name, Exe = name + ".exe", Start = t0, GameModeOn = GameMode.Active };
        FpsMonitor.Instance.BeginSession(p);
        Log.Info($"جلسة لعب: بدأ تسجيل {name}");
    }

    static void End()
    {
        var rec = cur; var smp = samples;
        cur = null; samples = null; idle = 0;
        var frames = FpsMonitor.Instance.EndSession();
        if (rec == null) return;
        rec.Seconds = (int)(DateTime.Now - rec.Start).TotalSeconds;
        if (smp.Count < MinSeconds) { Log.Info($"جلسة لعب: {rec.Game} قصيرة جداً، لم تُحفظ"); return; }
        saving = true;
        Task.Run(() =>
        {
            try
            {
                rec.Samples = smp;
                Summarize(rec, smp, frames);
                rec.Findings = SessionDiagnosis.Analyze(rec);
                Directory.CreateDirectory(Dir);
                var safe = new string(rec.Game.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
                rec.Id = $"{rec.Start:yyyyMMdd-HHmmss}-{(safe.Length == 0 ? "game" : safe)}";
                File.WriteAllText(Path.Combine(Dir, rec.Id + ".json"), JsonSerializer.Serialize(rec, Json), Encoding.UTF8);
                Prune();
                Log.Ok($"جلسة لعب: حُفظت {rec.Game} ({rec.Seconds / 60} د، متوسط {rec.AvgFps:0} FPS)");
                var bad = rec.Findings.Count(x => x.Level is "warn" or "bad");
                string msg = bad > 0
                    ? $"متوسط {rec.AvgFps:0} FPS • 1% Low {rec.Low1:0} • {bad} ملاحظات في «جلسات اللعب»"
                    : $"متوسط {rec.AvgFps:0} FPS • 1% Low {rec.Low1:0} • سلسة";
                Notify.Push(bad > 0 ? "warn" : "ok", "انتهت جلسة اللعب – " + rec.Game, msg, balloon: true);
            }
            catch (Exception ex) { Log.Error("جلسة لعب: تعذر الحفظ – " + ex.Message); }
            finally
            {
                saving = false;
                var d = System.Windows.Application.Current?.Dispatcher;
                if (d != null) d.BeginInvoke(() => Changed?.Invoke()); else Changed?.Invoke();
            }
        });
    }

    static void Summarize(SessionRecord r, List<SessionSample> s, List<double> frames)
    {
        r.AvgCpu = Math.Round(s.Average(x => x.Cpu), 1);
        r.AvgGpu = Math.Round(s.Average(x => x.Gpu), 1);
        r.AvgRam = Math.Round(s.Average(x => x.Ram), 1);
        r.MaxCpuT = s.Max(x => x.CpuT);
        r.MaxGpuT = s.Max(x => x.GpuT);
        r.Stutters = s.Sum(x => x.St);
        r.MaxFrameMs = s.Max(x => x.Max);
        if (frames != null && frames.Count >= 100)
        {
            var sorted = frames.OrderByDescending(x => x).ToList();
            double total = frames.Sum();
            int n1 = Math.Max(1, frames.Count / 100), n01 = Math.Max(1, frames.Count / 1000);
            r.AvgFps = Math.Round(frames.Count / (total / 1000), 1);
            r.Low1 = Math.Round(1000 / sorted.Take(n1).Average(), 1);
            r.Low01 = Math.Round(1000 / sorted.Take(n01).Average(), 1);
            r.MaxFrameMs = Math.Round(Math.Max(r.MaxFrameMs, sorted[0]), 1);
        }
        else
        {
            r.AvgFps = Math.Round(s.Average(x => x.Fps), 1);
            var ord = s.Select(x => x.Fps).OrderBy(x => x).ToList();
            r.Low1 = Math.Round(ord[Math.Max(0, ord.Count / 100)], 1);
            r.Low01 = Math.Round(ord[0], 1);
        }
    }

    static void Prune()
    {
        try
        {
            var files = new DirectoryInfo(Dir).GetFiles("*.json").OrderByDescending(f => f.Name).ToList();
            foreach (var f in files.Skip(KeepMax)) { try { f.Delete(); } catch (Exception ex) { Log.Warn("حذف جلسة قديمة: " + ex.Message); } }
        }
        catch (Exception ex) { Log.Warn("Prune sessions: " + ex.Message); }
    }

    // ---------------- reading ----------------
    public static List<SessionRecord> LoadAll(int max = 60)
    {
        var list = new List<SessionRecord>();
        try
        {
            if (!Directory.Exists(Dir)) return list;
            foreach (var f in new DirectoryInfo(Dir).GetFiles("*.json").OrderByDescending(x => x.Name).Take(max))
            {
                try
                {
                    var r = JsonSerializer.Deserialize<SessionRecord>(File.ReadAllText(f.FullName, Encoding.UTF8));
                    if (r != null) { r.Id = Path.GetFileNameWithoutExtension(f.Name); list.Add(r); }
                }
                catch (Exception ex) { Log.Warn($"جلسة تالفة {f.Name}: {ex.Message}"); }
            }
        }
        catch (Exception ex) { Log.Warn("LoadAll sessions: " + ex.Message); }
        return list;
    }

    public static bool Delete(SessionRecord r)
    {
        try { File.Delete(Path.Combine(Dir, r.Id + ".json")); return true; }
        catch (Exception ex) { Log.Warn("حذف الجلسة: " + ex.Message); return false; }
    }

    /// <summary>Plain-text report to paste to a friend or a support chat.</summary>
    public static string Report(SessionRecord r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"KHA Boost — {r.Game}");
        sb.AppendLine($"{r.Start:yyyy-MM-dd HH:mm}  •  {r.Seconds / 60} min {r.Seconds % 60} s");
        sb.AppendLine($"FPS avg {r.AvgFps:0} | 1% Low {r.Low1:0} | 0.1% Low {r.Low01:0} | longest frame {r.MaxFrameMs:0} ms | stutters {r.Stutters}");
        sb.AppendLine($"CPU {r.AvgCpu:0}% (max {r.MaxCpuT:0}°C) | GPU {r.AvgGpu:0}% (max {r.MaxGpuT:0}°C) | RAM {r.AvgRam:0}%");
        sb.AppendLine($"CPU: {HardwareMonitor.Instance.CpuName} | GPU: {HardwareMonitor.Instance.GpuName}");
        sb.AppendLine();
        foreach (var f in r.Findings) sb.AppendLine($"[{f.Level}] {f.Text}");
        return sb.ToString();
    }
}

/// <summary>
/// Turns the numbers of one session into plain sentences. Rule based: it compares what the PC was doing in the seconds
/// that stuttered with the whole session, so it points at the part that was really the limit.
/// Every sentence is a fixed template (numbers filled in), so the English UI can translate it.
/// </summary>
public static class SessionDiagnosis
{
    public static List<Finding> Analyze(SessionRecord r)
    {
        var res = new List<Finding>();
        void Add(string lvl, string text) => res.Add(new Finding { Level = lvl, Text = text });
        var s = r.Samples.Count > 0 ? r.Samples : null;
        var st = Settings.Current;

        var badSecs = s?.Where(x => x.St > 0).ToList() ?? new List<SessionSample>();
        // what the PC was doing while it stuttered (falls back to the whole session when there were no stutters)
        double cpuBad = badSecs.Count >= 3 ? badSecs.Average(x => x.Cpu) : r.AvgCpu;
        double gpuBad = badSecs.Count >= 3 ? badSecs.Average(x => x.Gpu) : r.AvgGpu;
        double ramBad = badSecs.Count >= 3 ? badSecs.Average(x => x.Ram) : r.AvgRam;
        bool uneven = r.AvgFps > 0 && r.Low1 < r.AvgFps * 0.5;
        bool stutter = r.Stutters >= 3;

        if (!stutter && !uneven) Add("ok", "الجلسة سلسة: ما فيها توقفات ملحوظة والحركة متساوية.");
        if (stutter)
        {
            string lvl = r.Stutters > 15 || r.MaxFrameMs >= 200 ? "bad" : "warn";
            Add(lvl, $"حصل {r.Stutters} توقف (إطار أطول من 50 ms وأطول بكثير من المعتاد)، وأطولها {r.MaxFrameMs:0} ms.");
        }
        if (uneven) Add("warn", $"الـ 1% Low ({r.Low1:0} FPS) أقل بكثير من المتوسط ({r.AvgFps:0} FPS): الحركة غير متساوية رغم الـ FPS العالي.");

        bool cpuHot = cpuBad >= 90, gpuHot = gpuBad >= 97, ramHot = ramBad >= 90 || r.AvgRam >= 90;
        if (cpuHot && (stutter || uneven || r.AvgCpu >= 90))
            Add("bad", $"المعالج كان على {cpuBad:0}% وقت التقطيع: المعالج هو الحد. أغلق البرامج الخلفية وخفّض إعدادات الفيزياء والزحام في اللعبة.");
        if (gpuHot)
            Add("info", $"كرت الشاشة كان على {gpuBad:0}%: هو الحد الأعلى للأداء. خفّض الدقة أو الظلال، أو فعّل DLSS / FSR.");
        if (ramHot)
            Add("bad", $"الرام وصل {Math.Max(ramBad, r.AvgRam):0}%: لما تمتلئ يبدأ Windows يستخدم القرص وتصير توقفات. أغلق المتصفح والبرامج الثقيلة.");
        if (r.MaxCpuT >= st.CpuTempLimit || r.MaxGpuT >= st.GpuTempLimit)
            Add("warn", $"الحرارة وصلت {Math.Max(r.MaxCpuT, r.MaxGpuT):0}°C: احتمال أن الجهاز قلّل سرعته لحماية نفسه. افحص التهوية والمراوح.");

        if (s != null && s.Count >= 90)
        {
            int third = s.Count / 3;
            double first = s.Take(third).Average(x => x.Fps), last = s.Skip(s.Count - third).Average(x => x.Fps);
            if (first > 0 && last < first * 0.8)
                Add("warn", $"الـ FPS نزل {(1 - last / first) * 100:0}% بين أول الجلسة وآخرها. الأسباب المعتادة: حرارة، أو تسرّب رام في اللعبة.");
        }

        if (stutter && !cpuHot && !gpuHot && !ramHot)
            Add("info", "حصل تقطيع والمعالج والكرت والرام مرتاحين: السبب غالباً تحميل ملفات أو شيدرز من القرص، أو برنامج خلفي. جرّب نقل اللعبة لـ SSD وفعّل «جهّز للعب».");
        if (!r.GameModeOn && (stutter || uneven))
            Add("info", "وضع «جهّز للعب» كان مطفياً في هذي الجلسة. فعّله في الجلسة القادمة وقارن النتيجة.");
        return res;
    }
}
