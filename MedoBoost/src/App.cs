using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost;

public static class Program
{
    static Mutex mutex;

    [STAThread]
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        if (args.Length > 0 && args[0] == "--actiontest") return ActionTest(args.Length > 1 ? args[1] : "actiontest");
        bool selfTest = args.Length > 0 && args[0] == "--selftest";
        if (!selfTest)
        {
            mutex = new Mutex(true, "MedoBoost-SingleInstance", out bool first);
            if (!first) { MessageBox.Show("KHA Boost شغال بالفعل.", "KHA Boost"); return 0; }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled: " + e.ExceptionObject);
        Settings.Load();
        Log.Info("KHA Boost started");

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error("UI error: " + e.Exception);
            if (selfTest) { Console.Error.WriteLine(e.Exception); Environment.Exit(3); }
            e.Handled = true;
            try { Dialogs.Toast("حدث خطأ: " + e.Exception.Message, "err"); } catch { }
        };
        Ui.LoadTheme(app);

        HardwareMonitor.Instance.Start();
        Task.Run(() => FpsMonitor.Instance.Start());

        MainWindow.SkipUpdateCheck = selfTest;
        MainWindow win;
        try { win = new MainWindow(); }
        catch (Exception ex)
        {
            Log.Error("Startup: " + ex);
            MessageBox.Show("تعذر تشغيل الواجهة:\n" + ex.Message + "\n\nالسجل: " + Log.FilePath, "KHA Boost", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        GameBoost.StartAutoPriority();
        if (selfTest) win.Loaded += (_, _) => _ = SelfTest(win, args.Length > 1 ? args[1] : "selftest");
        int code = app.Run(win);

        FpsMonitor.Instance.Dispose();
        HardwareMonitor.Instance.Dispose();
        Settings.Save();
        return code;
    }

    /// <summary>CI check: opens every page, renders a screenshot of each, then exits. Exit code != 0 means a page crashed.</summary>
    static async Task SelfTest(MainWindow win, string outDir)
    {
        Directory.CreateDirectory(outDir);
        try
        {
            await Task.Delay(2500);
            foreach (var id in win.PageIds.ToList())
            {
                win.Navigate(id);
                await Task.Delay(id == "clean" ? 6000 : id == "fresh" ? 12000 : id is "uninstall" or "security" ? 6000 : 2200);
                Snap(win, Path.Combine(outDir, $"{id}.png"));
                Console.WriteLine("page ok: " + id);
            }
            win.SetOverlay(true);
            await Task.Delay(1500);
            if (win.Overlay != null) Snap(win.Overlay, Path.Combine(outDir, "overlay.png"));
            win.SetOverlay(false);
            File.WriteAllText(Path.Combine(outDir, "log.txt"), string.Join("\n", Log.All.Select(e => $"[{e.Level}] {e.Text}")));
            Console.WriteLine("SELFTEST OK");
            Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SELFTEST FAILED: " + ex);
            File.WriteAllText(Path.Combine(outDir, "error.txt"), ex.ToString());
            Application.Current.Shutdown(2);
        }
    }

    /// <summary>CI only (throwaway VM): really applies, verifies and reverts every tweak, removes apps, cleans. Writes a report.</summary>
    static int ActionTest(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var r = new System.Text.StringBuilder();
        void W(string l) { r.AppendLine(l); File.WriteAllText(Path.Combine(outDir, "actions.txt"), r.ToString()); }
        W("=== SYSTEM SPECS ===");
        try { W(SystemInfo.Read().ToString()); } catch (Exception ex) { W("ERR " + ex); }
        W("");
                W("=== TWEAKS (applied → verified → reverted) ===");
        int ok = 0, bad = 0;
        foreach (var t in Tweaks.All)
        {
            string before = "?", after = "?", reverted = "?", err = "";
            try { before = t.IsApplied().ToString(); } catch (Exception ex) { err += " isApplied:" + ex.Message; }
            try { t.Apply(); } catch (Exception ex) { err += " apply:" + ex.Message; }
            try { after = t.IsApplied().ToString(); } catch (Exception ex) { err += " check:" + ex.Message; }
            try { t.Revert(); } catch (Exception ex) { err += " revert:" + ex.Message; }
            try { reverted = t.IsApplied().ToString(); } catch { }
            bool pass = after == "True" && err.Length == 0 && reverted == before;
            if (pass) ok++; else bad++;
            W($"{(pass ? "PASS" : "FAIL")} {t.Id,-18} before={before} after={after} reverted={reverted}{(reverted != before ? " (revert mismatch)" : "")}{err}");
        }
        W($"tweaks: {ok} pass, {bad} fail");
        W("");
        W("=== RESTORE POINT ===");
        try { var (rp, msg) = Tweaks.CreateRestorePoint(); W($"{rp}: {msg}"); } catch (Exception ex) { W("ERR " + ex.Message); }
        W("");
        W("=== DEBLOAT ===");
        try
        {
            Debloat.RefreshInstalled();
            foreach (var a in Debloat.Apps.Where(a => a.Installed))
                W($"{a.Id}: installed → remove={(a.DefaultOn ? Debloat.Remove(a).ToString() : "skipped (optional)")}");
            W("OneDrive installed: " + Debloat.OneDriveInstalled());
        }
        catch (Exception ex) { W("ERR " + ex); }
        W("");
        W("=== CLEANER ===");
        using (var w = new StreamWriter(Path.Combine(outDir, "deleted.txt")))
            foreach (var i in Cleaner.Items.Where(i => i.DefaultOn))
            {
                try { Cleaner.Scan(i); long size = i.Size; Cleaner.Clean(i, w); W($"{i.Id,-18} found={Util.Size(size)} freed={Util.Size(i.Freed)} skipped={i.Skipped}"); }
                catch (Exception ex) { W($"{i.Id} ERR {ex.Message}"); }
            }
        W("");
        W("=== SECURITY / UPDATES / UNINSTALL (read-only) ===");
        try { var d = Security.Status(); W($"Defender available={d.Available} realtime={d.RealtimeOn} tamper={d.TamperOn} sig={d.Signature}"); } catch (Exception ex) { W("Defender ERR " + ex.Message); }
        try { W("GPU driver source: " + SystemActions.GpuDriverSource().url); } catch (Exception ex) { W("Driver ERR " + ex.Message); }
        try { W("Active network adapters: " + string.Join(", ", SystemActions.ActiveAdapters())); } catch (Exception ex) { W("Net ERR " + ex.Message); }
        try { W("WARP installed: " + SystemActions.WarpInstalled); } catch (Exception ex) { W("WARP ERR " + ex.Message); }
        try { var prog = Uninstaller.List(); W($"Installed programs: {prog.Count}, sample: {string.Join(", ", prog.Take(5).Select(x => x.Name))}"); } catch (Exception ex) { W("Uninstaller ERR " + ex.Message); }
        W("");
                W("=== MEMORY / STARTUP / PROCESSES ===");
        try { W("RAM freed: " + Util.Size(Memory.CleanAll())); } catch (Exception ex) { W("RAM ERR " + ex.Message); }
        try { W("Startup entries: " + StartupManager.List().Count); } catch (Exception ex) { W("Startup ERR " + ex.Message); }
        try { var g = ProcessManager.Instance.Snapshot(false); Thread.Sleep(1000); g = ProcessManager.Instance.Snapshot(false); W($"Process groups: {g.Count}, top: {string.Join(", ", g.OrderByDescending(x => x.Ram).Take(3).Select(x => x.Name + " " + Util.Size(x.Ram)))}"); }
        catch (Exception ex) { W("Proc ERR " + ex.Message); }
        W("DONE");
        return 0;
    }

    static void Snap(Window w, string file)
    {
        var el = (FrameworkElement)w.Content;
        int width = Math.Max(1, (int)el.ActualWidth), height = Math.Max(1, (int)el.ActualHeight);
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(w.Background is SolidColorBrush b && b.Color.A > 0 ? b : new SolidColorBrush(Color.FromRgb(10, 10, 19)), null, new Rect(0, 0, width, height));
            // VisualBrush ignores the window's right-to-left mirroring, so flip it back for a true picture
            bool rtl = w.FlowDirection == FlowDirection.RightToLeft;
            if (rtl) dc.PushTransform(new MatrixTransform(-1, 0, 0, 1, width, 0));
            dc.DrawRectangle(new VisualBrush(el), null, new Rect(0, 0, width, height));
            if (rtl) dc.Pop();
        }
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(file);
        enc.Save(fs);
    }
}
