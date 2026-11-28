using System.Diagnostics;
using System.Text.Json;
using System.Windows.Threading;

namespace MedoBoost.Core;

public class GameModeState
{
    public bool Active { get; set; }
    public DateTime Started { get; set; }
    public int GamePid { get; set; }                 // 0 = started by hand
    public string GameName { get; set; } = "";
    public string PrevPlan { get; set; }
    public string CreatedPlan { get; set; }
    public List<string> StoppedServices { get; set; } = new();
    public List<string> Closed { get; set; } = new();
    public bool PausedUpdates { get; set; }
    public bool MutedToasts { get; set; }
    public long RamFreed { get; set; }
}

/// <summary>
/// "جهّز للعب": a temporary gaming mode. Everything it changes is recorded in gamemode.json
/// so "end game mode" (or the next launch of the app) puts every setting back exactly as it was.
/// </summary>
public static class GameMode
{
    static string StatePath => Path.Combine(Util.DataDir, "gamemode.json");
    public static GameModeState State { get; private set; } = Load();
    public static bool Active => State.Active;
    public static event Action Changed;
    static bool busy;

    static readonly string[] Svcs = { "SysMain", "WSearch", "DiagTrack" };
    static readonly RegOp MuteToasts = new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0);

    static GameModeState Load()
    {
        try { if (File.Exists(StatePath)) return JsonSerializer.Deserialize<GameModeState>(File.ReadAllText(StatePath)) ?? new(); } catch { }
        return new();
    }
    static void Save() { try { File.WriteAllText(StatePath, JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true })); } catch { } }
    static void Raise() { var d = System.Windows.Application.Current?.Dispatcher; if (d != null) d.BeginInvoke(() => Changed?.Invoke()); else Changed?.Invoke(); }

    static bool ServiceRunning(string name)
    {
        var (_, o) = Util.Run("sc.exe", $"query {name}", 15000);
        return o.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Runs on a background thread. Returns the new state.</summary>
    public static GameModeState Activate(int gamePid = 0, string gameName = "")
    {
        if (State.Active) return State;
        var st = Settings.Current;
        var s = new GameModeState { Active = true, Started = DateTime.Now, GamePid = gamePid, GameName = gameName };
        State = s; Save();
        Log.Info("=== وضع اللعب: تفعيل ===");

        if (st.GameReadyClose)
            foreach (var name in st.BoostKillList.ToList())
            {
                if (string.Equals(name, Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase)) continue;
                bool any = false;
                foreach (var p in Process.GetProcessesByName(name)) { try { p.Kill(true); any = true; } catch { } finally { p.Dispose(); } }
                if (any) s.Closed.Add(name);
            }
        if (s.Closed.Count > 0) Thread.Sleep(700);

        if (st.GameReadyServices)
            foreach (var svc in Svcs)
                try
                {
                    if (!ServiceRunning(svc)) continue;
                    Util.Run("net.exe", $"stop {svc} /y", 60000);
                    s.StoppedServices.Add(svc);
                }
                catch { }

        if (st.GameReadyPower)
        {
            try
            {
                var prev = Tweaks.ActiveScheme();
                if (prev != null && Util.Run("powercfg.exe", "/setactive SCHEME_MIN", 15000).code == 0) s.PrevPlan = prev;
                else
                {
                    var dup = Util.Run("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", 15000);
                    var m = System.Text.RegularExpressions.Regex.Match(dup.output, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                    if (m.Success && Util.Run("powercfg.exe", $"/setactive {m.Value}", 15000).code == 0) { s.PrevPlan = prev; s.CreatedPlan = m.Value; }
                }
            }
            catch (Exception ex) { Log.Warn("وضع اللعب – خطة الطاقة: " + ex.Message); }
        }

        if (st.GameReadyDns) { try { Native.DnsFlushResolverCache(); } catch { } }

        if (st.GameReadyPauseUpdates)
        {
            try
            {
                if (Reg.Get("HKLM", @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", "PauseUpdatesExpiryTime") == null)
                { SystemActions.PauseUpdates(1); s.PausedUpdates = true; }
            }
            catch (Exception ex) { Log.Warn("وضع اللعب – التحديثات: " + ex.Message); }
        }

        if (st.GameReadyMute)
        {
            try { Reg.Apply(MuteToasts); s.MutedToasts = true; } catch (Exception ex) { Log.Warn("وضع اللعب – الإشعارات: " + ex.Message); }
        }

        if (st.GameReadyRam) { try { s.RamFreed = Memory.CleanAll(); } catch { } }

        Save(); Raise();
        Log.Ok($"وضع اللعب شغّال: أُغلق {s.Closed.Count} برنامج، أُوقفت {s.StoppedServices.Count} خدمة، رام محرر {Util.Size(s.RamFreed)}");
        return s;
    }

    /// <summary>Puts everything back. Safe to call even if nothing is active.</summary>
    public static void Deactivate()
    {
        var s = State;
        if (!s.Active) return;
        Log.Info("=== وضع اللعب: إنهاء ===");
        try
        {
            if (s.CreatedPlan != null)
            {
                Util.Run("powercfg.exe", $"/setactive {s.PrevPlan ?? "SCHEME_BALANCED"}", 15000);
                Util.Run("powercfg.exe", $"/delete {s.CreatedPlan}", 15000);
            }
            else if (s.PrevPlan != null) Util.Run("powercfg.exe", $"/setactive {s.PrevPlan}", 15000);
        }
        catch { }
        foreach (var svc in s.StoppedServices) { try { Util.Run("net.exe", $"start {svc}", 60000); } catch { } }
        if (s.PausedUpdates) { try { SystemActions.ResumeUpdates(); } catch { } }
        if (s.MutedToasts) { try { Reg.Restore(MuteToasts); } catch { } }
        State = new GameModeState();
        Save(); Raise();
        Log.Ok("انتهى وضع اللعب – رجعت الإعدادات كلها");
    }

    /// <summary>Game mode was started by hand/launcher: bind it to the game's process so it ends when the game closes.</summary>
    public static void AttachGame(int pid, string name)
    {
        if (!State.Active || State.GamePid != 0) return;
        State.GamePid = pid; State.GameName = name;
        Save();
    }

    public static async Task ToggleAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            if (Active)
            {
                await Task.Run(Deactivate);
                Notify.Push("ok", "انتهى وضع اللعب", "رجعنا كل الإعدادات لحالها.");
            }
            else
            {
                var s = await Task.Run(() => Activate());
                Notify.Push("ok", "وضع اللعب شغّال 🚀", Summary(s));
            }
        }
        catch (Exception ex) { Notify.Push("err", "وضع اللعب", ex.Message); }
        finally { busy = false; }
    }

    public static string Summary(GameModeState s)
    {
        var parts = new List<string>();
        if (s.RamFreed > 0) parts.Add($"تحرر {Util.Size(s.RamFreed)} رام");
        if (s.Closed.Count > 0) parts.Add($"أُغلق {s.Closed.Count} برنامج");
        if (s.StoppedServices.Count > 0) parts.Add($"أُوقفت {s.StoppedServices.Count} خدمة");
        if (s.PrevPlan != null) parts.Add("طاقة قصوى");
        return parts.Count > 0 ? string.Join(" • ", parts) : "جاهز";
    }

    // ---------------- automatic mode ----------------
    static DispatcherTimer timer;
    static int streak, ignorePid;
    static bool firstTick = true;
    internal static readonly HashSet<string> NotGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "iexplore", "vlc", "wmplayer", "mpc-hc64", "potplayermini64",
        "obs64", "obs32", "discord", "spotify", "explorer", "code", "devenv", "KHABoost", "MedoBoost", "WindowsTerminal", "Teams", "ms-teams",
    };

    public static void StartAuto()
    {
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    static void Tick()
    {
        try
        {
            if (firstTick)
            {
                firstTick = false;
                // the app was closed while game mode was on → restore now
                if (State.Active && State.GamePid != 0 && !IsRunning(State.GamePid)) { Task.Run(Deactivate); return; }
            }
            if (State.Active)
            {
                if (State.GamePid != 0 && !busy && !IsRunning(State.GamePid))
                {
                    busy = true;
                    Task.Run(() => { Deactivate(); }).ContinueWith(_ => { busy = false; Notify.Push("ok", "سكّرت اللعبة", "انتهى وضع اللعب ورجعت الإعدادات."); });
                }
                return;
            }
            if (!Settings.Current.GameReadyAuto || busy) return;
            var (pid, fps, _) = FpsMonitor.Instance.Foreground();
            if (pid <= 4 || pid == Environment.ProcessId || pid == ignorePid || fps < 30) { streak = 0; return; }
            string name; string path;
            try { using var p = Process.GetProcessById(pid); name = p.ProcessName; path = Native.ProcessPath(pid) ?? ""; } catch { streak = 0; return; }
            if (NotGames.Contains(name) || path.StartsWith(Util.WinDir, StringComparison.OrdinalIgnoreCase)) { streak = 0; return; }
            if (++streak < 3) return;       // ~9 seconds of steady frames
            streak = 0; busy = true;
            Task.Run(() => Activate(pid, name)).ContinueWith(t =>
            {
                busy = false;
                if (t.IsCompletedSuccessfully) Notify.Push("ok", $"وضع اللعب شغّال – {name}", Summary(t.Result), balloon: true);
            });
        }
        catch (Exception ex) { Log.Warn("GameMode tick: " + ex.Message); }
    }

    static bool IsRunning(int pid) { try { using var p = Process.GetProcessById(pid); return !p.HasExited; } catch { return false; } }
    public static void IgnoreCurrentGame() { ignorePid = State.GamePid; }
}
