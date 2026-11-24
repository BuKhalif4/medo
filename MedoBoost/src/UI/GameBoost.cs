using System.Diagnostics;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>One click (or Ctrl+Shift+G) before playing: frees RAM, closes the apps you chose, flushes DNS.</summary>
public static class GameBoost
{
    static bool busy;

    public static async Task Run(bool silent = false)
    {
        if (busy) return;
        busy = true;
        try
        {
            var (freed, killed) = await Task.Run(() =>
            {
                int k = 0;
                foreach (var name in Settings.Current.BoostKillList.ToList())
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try { p.Kill(true); k++; } catch { } finally { p.Dispose(); }
                    }
                if (k > 0) Thread.Sleep(800);
                Native.DnsFlushResolverCache();
                long f = Memory.CleanAll();
                return (f, k);
            });
            string msg = $"🚀 Game Boost: تم تحرير {Util.Size(freed)} رام" + (killed > 0 ? $" وإغلاق {killed} برنامج" : "");
            Log.Ok(msg);
            Dialogs.Toast(msg);
        }
        catch (Exception ex) { Dialogs.Toast("Game Boost: " + ex.Message, "err"); }
        finally { busy = false; }
    }

    static readonly HashSet<int> boosted = new();
    static System.Windows.Threading.DispatcherTimer autoTimer;

    /// <summary>When enabled: the game in front (rendering frames) gets High CPU priority automatically.</summary>
    public static void StartAutoPriority()
    {
        autoTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        autoTimer.Tick += (_, _) =>
        {
            if (!Settings.Current.AutoGamePriority) return;
            var (pid, fps, _) = FpsMonitor.Instance.Foreground();
            if (pid <= 4 || pid == Environment.ProcessId || fps < 20 || boosted.Contains(pid)) return;
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.PriorityClass == ProcessPriorityClass.Normal)
                {
                    p.PriorityClass = ProcessPriorityClass.High;
                    boosted.Add(pid);
                    Log.Info($"أولوية عالية تلقائياً للعبة: {p.ProcessName}");
                }
            }
            catch { boosted.Add(pid); }
        };
        autoTimer.Start();
    }
}
