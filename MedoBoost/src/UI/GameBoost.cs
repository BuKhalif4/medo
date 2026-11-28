using System.Diagnostics;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>Raises the CPU priority of the game you are playing (the "جهّز للعب" page itself is in Core/GameMode.cs).</summary>
public static class GameBoost
{
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
