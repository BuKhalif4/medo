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

        bool selfTest = args.Length > 0 && args[0] == "--selftest";
        if (!selfTest)
        {
            mutex = new Mutex(true, "MedoBoost-SingleInstance", out bool first);
            if (!first) { MessageBox.Show("MEDO Boost شغال بالفعل.", "MEDO Boost"); return 0; }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled: " + e.ExceptionObject);
        Settings.Load();
        Log.Info("MEDO Boost started");

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

        MainWindow win;
        try { win = new MainWindow(); }
        catch (Exception ex)
        {
            Log.Error("Startup: " + ex);
            MessageBox.Show("تعذر تشغيل الواجهة:\n" + ex.Message + "\n\nالسجل: " + Log.FilePath, "MEDO Boost", MessageBoxButton.OK, MessageBoxImage.Error);
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
                await Task.Delay(id == "clean" ? 6000 : 2200);
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
