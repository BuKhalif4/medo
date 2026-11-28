using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class SessionsPage : PageBase
{
    readonly StackPanel list = new();
    readonly TextBlock status = Ui.Txt("", 13, "TextDim");
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    List<SessionRecord> all = new();
    bool loading;

    public SessionsPage()
    {
        var root = new StackPanel();
        root.Children.Add(Ui.Title("جلسات اللعب"));
        root.Children.Add(Ui.Txt("البرنامج يلاحظ لما تلعب، يسجّل الـ FPS وحرارة وحمل الجهاز كل ثانية، وبعد ما تسكّر اللعبة يقول لك بالضبط ليش تقطّعت. كل شيء يبقى على جهازك.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 16));

        var rec = Ui.Check(Ui.Txt("سجّل جلسات اللعب تلقائياً", 13, "Text"), Settings.Current.RecordSessions,
            v => { Settings.Current.RecordSessions = v; Settings.Save(); });
        root.Children.Add(Ui.Card(Ui.VStack(status.M(0, 0, 0, 10), rec, Ui.Txt("الجلسات الأقصر من دقيقة ما تُحفظ. يُحتفظ بآخر 200 جلسة.", 11.5, "TextMute").M(0, 8, 0, 0)), 16).M(0, 0, 0, 14));
        root.Children.Add(list);

        Children.Add(Ui.Scroll(root));
        tick.Tick += (_, _) => UpdateStatus();
        SessionRecorder.Changed += () => Dispatcher.BeginInvoke(() => Reload());
    }

    public override void OnShow() { UpdateStatus(); tick.Start(); Reload(); }
    public override void OnHide() => tick.Stop();

    void UpdateStatus()
    {
        if (SessionRecorder.Recording)
        {
            int s = SessionRecorder.CurrentSeconds;
            status.Text = L.T("🔴 جاري تسجيل: ") + SessionRecorder.CurrentGame + $"  ({s / 60}:{s % 60:00})";
            status.Foreground = Ui.B("Bad");
        }
        else
        {
            status.Text = L.T(Settings.Current.RecordSessions ? "جاهز: ابدأ لعبة وأنا أسجّل (يحتاج 5 ثواني فريمات ثابتة)." : "التسجيل التلقائي مطفي.");
            status.Foreground = Ui.B("TextDim");
        }
    }

    async void Reload()
    {
        if (loading) return;
        loading = true;
        try { all = await Task.Run(() => SessionRecorder.LoadAll()); }
        catch (Exception ex) { Log.Warn("Sessions page: " + ex.Message); all = new(); }
        finally { loading = false; }
        Render();
    }

    void Render()
    {
        list.Children.Clear();
        if (all.Count == 0)
        {
            list.Children.Add(Ui.Card(Ui.Txt("ما في جلسات بعد. العب لعبة أكثر من دقيقة وبتظهر هنا.", 13, "TextDim", null, TextWrapping.Wrap), 18));
            return;
        }
        for (int i = 0; i < all.Count; i++)
        {
            var r = all[i];
            // previous session of the same game, for the "vs last time" line
            var prev = all.Skip(i + 1).FirstOrDefault(x => string.Equals(x.Exe, r.Exe, StringComparison.OrdinalIgnoreCase));
            list.Children.Add(Row(r, prev).M(0, 0, 0, 10));
        }
    }

    FrameworkElement Row(SessionRecord r, SessionRecord prev)
    {
        int worst = r.Findings.Any(f => f.Level == "bad") ? 2 : r.Findings.Any(f => f.Level == "warn") ? 1 : 0;
        string brush = worst == 2 ? "Bad" : worst == 1 ? "Warn" : "Good";
        string verdict = worst == 2 ? "فيها مشاكل" : worst == 1 ? "فيها ملاحظات" : "سلسة";

        var detail = new StackPanel { Visibility = Visibility.Collapsed };
        bool built = false;

        string cmp = "";
        if (prev != null && prev.AvgFps > 0)
        {
            double d = (r.AvgFps / prev.AvgFps - 1) * 100;
            cmp = $"   •   {(d >= 0 ? "+" : "")}{d:0}% " + L.T("عن آخر جلسة");
        }

        var head = Ui.Cols(new[] { "*", "Auto", "Auto", "Auto", "Auto" },
            Ui.VStack(
                Ui.HStack(0, Ui.Txt(r.Game, 15, "Text", FontWeights.Bold), Ui.Badge(verdict, brush, worst == 2 ? "#33EF4444" : worst == 1 ? "#33F59E0B" : "#3322C55E")),
                Ui.Txt($"{r.Start:yyyy-MM-dd HH:mm}   •   {r.Seconds / 60} " + L.T("دقيقة") + cmp, 11.5, "TextMute").M(0, 3, 0, 0)),
            Stat($"{r.AvgFps:0}", "متوسط FPS"), Stat($"{r.Low1:0}", "1% Low"), Stat(r.Stutters.ToString(), "توقفات"),
            Ui.Txt("▾", 16, "TextDim").M(14, 0, 0, 0));
        head.Cursor = Cursors.Hand;
        head.Background = System.Windows.Media.Brushes.Transparent;   // whole row is clickable
        head.MouseLeftButtonUp += (_, _) =>
        {
            if (!built) { Build(detail, r, prev); built = true; }
            detail.Visibility = detail.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        };
        return Ui.Card(Ui.VStack(head, detail), 16);
    }

    static FrameworkElement Stat(string value, string label)
        => Ui.VStack(Ui.Txt(value, 17, "Accent2", FontWeights.Bold).Ltr(), Ui.Txt(label, 10.5, "TextMute")).M(14, 0, 0, 0);

    void Build(StackPanel host, SessionRecord r, SessionRecord prev)
    {
        host.Children.Add(Ui.Sep());

        // ---- charts (≤ 240 points each)
        var pts = Downsample(r.Samples, 240);
        var fps = new Sparkline("#FF22D3EE") { FixedMax = 0, Height = 110, Capacity = 300 };
        var load = new Sparkline("#FF8B5CF6", "#FF22C55E", "#FFF59E0B") { FixedMax = 100, Height = 90, Capacity = 300 };
        foreach (var p in pts) { fps.Push(p.Fps); load.Push(p.Cpu, p.Gpu, p.Ram); }
        host.Children.Add(Ui.Txt("الـ FPS خلال الجلسة", 12, "TextDim").M(0, 0, 0, 4));
        host.Children.Add(fps);
        host.Children.Add(Ui.Txt("حمل الجهاز: المعالج / الكرت / الرام (%)", 12, "TextDim").M(0, 10, 0, 4));
        host.Children.Add(load);

        // ---- numbers
        string F(double v, string unit) => v > 0 ? $"{v:0}{unit}" : "—";
        host.Children.Add(Ui.Txt(
            $"FPS {r.AvgFps:0}  •  1% Low {r.Low1:0}  •  0.1% Low {r.Low01:0}  •  " + L.T("أطول إطار") + $" {r.MaxFrameMs:0} ms\n" +
            $"CPU {r.AvgCpu:0}% ({F(r.MaxCpuT, "°C")})  •  GPU {r.AvgGpu:0}% ({F(r.MaxGpuT, "°C")})  •  RAM {r.AvgRam:0}%",
            12, "TextDim", null, TextWrapping.Wrap).Ltr().M(0, 10, 0, 0));

        // ---- diagnosis
        host.Children.Add(Ui.Sep());
        host.Children.Add(Ui.Txt("التشخيص", 14, "Text", FontWeights.Bold).M(0, 0, 0, 8));
        foreach (var f in r.Findings)
        {
            string icon = f.Level switch { "ok" => "✔", "bad" => "✖", "warn" => "▲", _ => "ℹ" };
            string br = f.Level switch { "ok" => "Good", "bad" => "Bad", "warn" => "Warn", _ => "TextDim" };
            host.Children.Add(Ui.Cols(new[] { "22", "*" }, Ui.Txt(icon, 13, br), Ui.Mixed(f.Text, 12.5, "Text")).M(0, 0, 0, 6));
        }

        // ---- actions
        host.Children.Add(Ui.HStack(10,
            Ui.Btn("نسخ التقرير", "", () =>
            {
                try { Clipboard.SetText(SessionRecorder.Report(r)); Dialogs.Toast("تم نسخ التقرير ✔"); }
                catch (Exception ex) { Log.Warn("نسخ التقرير: " + ex.Message); Dialogs.Toast("تعذر النسخ", "err"); }
            }, "BtnGhost"),
            Ui.Btn("فتح مجلد الجلسات", "", () => Util.OpenFolder(SessionRecorder.Dir), "BtnGhost"),
            Ui.Btn("حذف", "", async () =>
            {
                if (!await Dialogs.Confirm("حذف الجلسة", $"تحذف جلسة {r.Game} بتاريخ {r.Start:yyyy-MM-dd HH:mm}؟", "حذف", true)) return;
                if (SessionRecorder.Delete(r)) { all.Remove(r); Render(); }
                else Dialogs.Toast("تعذر حذف الملف", "err");
            }, "BtnGhost")).M(0, 12, 0, 0));
    }

    static List<SessionSample> Downsample(List<SessionSample> s, int max)
    {
        if (s.Count <= max) return s;
        double step = (double)s.Count / max;
        var res = new List<SessionSample>(max);
        for (int i = 0; i < max; i++)
        {
            int a = (int)(i * step), b = Math.Min(s.Count, (int)((i + 1) * step));
            if (b <= a) b = a + 1;
            var seg = s.Skip(a).Take(b - a).ToList();
            res.Add(new SessionSample { Fps = seg.Average(x => x.Fps), Cpu = seg.Average(x => x.Cpu), Gpu = seg.Average(x => x.Gpu), Ram = seg.Average(x => x.Ram) });
        }
        return res;
    }
}
