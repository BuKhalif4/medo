using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class GamesPage : PageBase
{
    readonly StackPanel list = new();
    readonly TextBlock info = Ui.Txt("", 12.5, "TextDim");
    List<GameInfo> games = new();
    List<SessionRecord> sessions = new();
    bool scanning, scanned;

    public GamesPage()
    {
        var root = new StackPanel();
        root.Children.Add(Ui.Title("ألعابي"));
        root.Children.Add(Ui.Txt("يلقى الألعاب المثبتة من Steam و Epic و GOG، ويشغّلها لك بعد ما يجهّز الجهاز، ويقول لك هل جهازك يقدر يشغّلها قبل ما تنزّلها أو تلعبها.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 14));
        root.Children.Add(Ui.HStack(14, Ui.Btn("فحص الألعاب من جديد", "", () => _ = Scan(true)), info).M(0, 0, 0, 14));
        root.Children.Add(list);
        Children.Add(Ui.Scroll(root));
    }

    public override void OnShow() { if (!scanned) _ = Scan(false); }

    async Task Scan(bool force)
    {
        if (scanning) return;
        scanning = true;
        info.Text = L.T("جاري البحث...");
        try
        {
            games = await Task.Run(GameLibrary.Scan);
            sessions = await Task.Run(() => SessionRecorder.LoadAll(200));
            scanned = true;
        }
        catch (Exception ex) { Log.Error("Games scan: " + ex.Message); games = new(); }
        finally { scanning = false; }
        info.Text = L.T("لقيت") + $" {games.Count} " + L.T("لعبة");
        Render();
    }

    void Render()
    {
        list.Children.Clear();
        if (games.Count == 0)
        {
            list.Children.Add(Ui.Card(Ui.Txt("ما لقيت ألعاب. الألعاب اللي من Steam و Epic و GOG بس هي اللي تظهر هنا.", 13, "TextDim", null, TextWrapping.Wrap), 18));
            return;
        }
        foreach (var g in games) list.Children.Add(Row(g).M(0, 0, 0, 10));
    }

    SessionRecord LastSession(GameInfo g)
        => g.ProcessName == "" ? null : sessions.FirstOrDefault(s => string.Equals(s.Exe, g.ProcessName + ".exe", StringComparison.OrdinalIgnoreCase));

    FrameworkElement Row(GameInfo g)
    {
        var result = new StackPanel { Visibility = Visibility.Collapsed };
        var last = LastSession(g);
        string sub = (g.SizeBytes > 0 ? Util.Size(g.SizeBytes) : "") +
                     (last != null ? (g.SizeBytes > 0 ? "   •   " : "") + L.T("آخر جلسة") + $": {last.AvgFps:0} FPS ({last.Start:yyyy-MM-dd})" : "");

        var checkBtn = Ui.Btn("هل يشغّلها جهازي؟", "", null, "BtnGhost");
        checkBtn.ToolTip = L.T("يتصل بمتجر Steam ويجيب المتطلبات المنشورة للعبة. ما يرسل أي شي عنك.");
        checkBtn.Click += async (_, _) => await Check(g, result, checkBtn);

        var head = Ui.Cols(new[] { "*", "Auto" },
            Ui.VStack(
                Ui.HStack(0, Ui.Txt(g.Name, 15, "Text", FontWeights.Bold), Ui.Badge(g.Source, "Accent2", "#33FFFFFF")),
                Ui.Txt(sub, 11.5, "TextMute").M(0, 3, 0, 0)),
            Ui.HStack(8,
                Ui.Btn("جهّز وشغّل", "", async () => await PrepareAndLaunch(g)),
                Ui.Btn("تشغيل", "", () => { if (!GameLibrary.Launch(g)) Dialogs.Toast("تعذر تشغيل اللعبة", "err"); }, "BtnGhost"),
                checkBtn,
                Ui.Btn("المجلد", "", () => Util.OpenFolder(g.Dir), "BtnGhost")));
        return Ui.Card(Ui.VStack(head, result), 16);
    }

    static async Task PrepareAndLaunch(GameInfo g)
    {
        try
        {
            if (!GameMode.Active) await Task.Run(() => GameMode.Activate(0, g.Name));
            if (!GameLibrary.Launch(g)) { Dialogs.Toast("تعذر تشغيل اللعبة", "err"); return; }
            Dialogs.Toast("جهّزت الجهاز وشغّلت " + g.Name + " 🚀");
            if (g.ProcessName == "") return;          // exe unknown: game mode stays on until you end it from «جهّز للعب»
            // bind game mode to the game, so it switches off by itself when the game closes
            await Task.Run(() =>
            {
                for (int i = 0; i < 120; i++)
                {
                    var ps = Process.GetProcessesByName(g.ProcessName);
                    try { if (ps.Length > 0) { GameMode.AttachGame(ps[0].Id, g.Name); return; } }
                    finally { foreach (var p in ps) p.Dispose(); }
                    Thread.Sleep(1000);
                }
            });
        }
        catch (Exception ex) { Log.Error("جهّز وشغّل: " + ex.Message); Dialogs.Toast("صار خطأ: " + ex.Message, "err"); }
    }

    static async Task Check(GameInfo g, StackPanel host, Button btn)
    {
        btn.IsEnabled = false;
        host.Visibility = Visibility.Visible;
        host.Children.Clear();
        host.Children.Add(Ui.Sep());
        host.Children.Add(Ui.Txt("جاري جلب المتطلبات من Steam...", 12.5, "TextDim"));
        try
        {
            var r = await GameRequirements.CheckAsync(g);
            host.Children.Clear();
            host.Children.Add(Ui.Sep());
            if (r.Error != "") { host.Children.Add(Ui.Mixed(r.Error, 12.5, "Warn")); return; }
            foreach (var f in r.Lines)
            {
                string icon = f.Level switch { "ok" => "✔", "bad" => "✖", "warn" => "▲", _ => "ℹ" };
                string br = f.Level switch { "ok" => "Good", "bad" => "Bad", "warn" => "Warn", _ => "TextDim" };
                host.Children.Add(Ui.Cols(new[] { "22", "*" }, Ui.Txt(icon, 13, br), Ui.Mixed(f.Text, 12.5, "Text")).M(0, 0, 0, 6));
            }
            host.Children.Add(Ui.Txt("تقدير: مقارنة الكرت على سلّم تقريبي، والمعالج ما يتقارن تلقائياً.", 11, "TextMute").M(0, 4, 0, 0));
        }
        catch (Exception ex) { Log.Error("فحص المتطلبات: " + ex.Message); host.Children.Clear(); host.Children.Add(Ui.Txt("صار خطأ: " + ex.Message, 12.5, "Bad")); }
        finally { btn.IsEnabled = true; }
    }
}
