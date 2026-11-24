using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class TweaksPage : PageBase
{
    readonly Dictionary<Tweak, ToggleButton> switches = new();
    readonly TextBlock summary = Ui.Txt("", 13, "TextDim");
    bool updating;

    public TweaksPage()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var restore = Ui.Btn("إنشاء نقطة استعادة", "", async () =>
        {
            Dialogs.Toast("جاري إنشاء نقطة الاستعادة... (ممكن تاخذ دقيقة)", "info");
            var (ok, msg) = await Task.Run(Tweaks.CreateRestorePoint);
            Dialogs.Toast(ok ? "تم إنشاء نقطة الاستعادة ✔" : "فشل: " + msg, ok ? "ok" : "err");
        }, "BtnGhost");
        var applyRec = Ui.Btn("تطبيق المنصوح به", "", async () => await ApplyRecommended());
        var enableAll = Ui.Btn("تفعيل الكل", "\uE73A", async () => await EnableAll(Tweaks.All), "BtnGhost");
        enableAll.ToolTip = "يفعّل كل التويكات – بعدها طفّي اللي ما تبيه";
        var revertAll = Ui.Btn("إرجاع كل شي للأصل", "", async () => await RevertAll(), "BtnGhost");

        var info = Ui.Card(Ui.Cols(new[] { "Auto", "*" }, Ui.Icon("", 22, "Good").M(0, 0, 12, 0),
            Ui.Txt("كل تويك يحفظ القيمة الأصلية قبل التعديل، وتقدر ترجعه بإطفاء المفتاح. ننصح بإنشاء نقطة استعادة قبل أول مرة. التويكات المعلّمة «منصوح» آمنة ومجربة.", 12.5, "TextDim", null, TextWrapping.Wrap)), 14);
        info.Background = Ui.Solid("#1422C55E");

        var head = Ui.VStack(
            Ui.Title("تسريع وتويكات"),
            Ui.Txt("تحسينات Windows للألعاب والأداء والشبكة والخصوصية – كل وحدة مشروحة وتقدر ترجعها.", 13, "TextDim").M(0, 4, 0, 14),
            Ui.Cols(new[] { "Auto", "Auto", "Auto", "Auto", "*", "Auto" }, applyRec, enableAll.M(10, 0, 0, 0), restore.M(10, 0, 0, 0), revertAll.M(10, 0, 0, 0), null, summary),
            info.M(0, 14, 0, 14));
        Children.Add(head);

        var list = new StackPanel();
        string[] glyphs = { "", "", "", "", "", "\uE943" };
        for (int ci = 0; ci < Tweaks.Categories.Length; ci++)
        {
            var cat = Tweaks.Categories[ci];
            var body = new StackPanel();
            foreach (var t in Tweaks.All.Where(t => t.Category == cat)) body.Children.Add(Row(t));
            var catTweaks = Tweaks.All.Where(t => t.Category == cat).ToList();
            var catAll = Ui.Btn("تفعيل الكل", null, async () => await EnableAll(catTweaks), "BtnGhost");
            catAll.Padding = new Thickness(10, 4, 10, 4); catAll.FontSize = 11.5;
            var catHead = Ui.Cols(new[] { "*", "Auto" }, Ui.HStack(10, Ui.Icon(glyphs[ci], 18, "Accent2"), Ui.Txt(cat, 16, "Text", FontWeights.Bold)), catAll);
            var card = Ui.Card(Ui.VStack(catHead, Ui.Sep(), body), 18);
            card.Margin = new Thickness(0, 0, 0, 14);
            list.Children.Add(card);
        }
        var sv = Ui.Scroll(list);
        Grid.SetRow(sv, 1);
        Children.Add(sv);
        Ui.Stagger(list, 70, 100);
    }

    UIElement Row(Tweak t)
    {
        var sw = Ui.Switch(false);
        switches[t] = sw;
        sw.Checked += async (_, _) => await Toggle(t, true);
        sw.Unchecked += async (_, _) => await Toggle(t, false);
        var title = Ui.HStack(0, Ui.Txt(t.Name, 13.5, "Text", System.Windows.FontWeights.SemiBold));
        if (t.Recommended) title.Children.Add(Ui.Badge("منصوح", "Good", "#2222C55E"));
        if (t.Risk == Risk.Caution) title.Children.Add(Ui.Badge("انتبه", "Warn", "#22F59E0B"));
        if (t.NeedsRestart) title.Children.Add(Ui.Badge("يحتاج ريستارت", "Accent2", "#2206B6D4"));
        var g = Ui.Cols(new[] { "*", "Auto" }, Ui.VStack(title, Ui.Txt(t.Description, 12, "TextDim", null, TextWrapping.Wrap).M(0, 3, 0, 0)).M(0, 0, 20, 0), sw);
        g.Margin = new Thickness(0, 8, 0, 8);
        return g;
    }

    async Task Toggle(Tweak t, bool on)
    {
        if (updating) return;
        var sw = switches[t];
        sw.IsEnabled = false;
        try
        {
            if (on && t.Risk == Risk.Caution && !await Dialogs.Confirm(t.Name, t.Description + "\n\nمتأكد تبي تفعله؟", "فعّل"))
            {
                Set(sw, false);
                return;
            }
            await Task.Run(() => { if (on) t.Apply(); else t.Revert(); });
            bool now = await Task.Run(() => SafeApplied(t));
            Set(sw, now);
            if (now == on) Dialogs.Toast((on ? "تم تفعيل: " : "تم إرجاع: ") + t.Name + (t.NeedsRestart ? " (يحتاج ريستارت)" : ""));
            else if (!on) Dialogs.Toast(t.Name + ": هذا الإعداد مفعّل افتراضياً في Windows (رجع للقيمة الأصلية)", "info");
            else Dialogs.Toast("ما قدرت أطبق: " + t.Name, "warn");
        }
        catch (Exception ex)
        {
            Log.Error(t.Name + ": " + ex.Message);
            Dialogs.Toast(t.Name + ": " + ex.Message, "err");
            Set(sw, await Task.Run(() => SafeApplied(t)));
        }
        finally { sw.IsEnabled = true; UpdateSummary(); }
    }

    static bool SafeApplied(Tweak t) { try { return t.IsApplied(); } catch { return false; } }

    void Set(ToggleButton sw, bool v) { updating = true; sw.IsChecked = v; updating = false; }

    void UpdateSummary()
    {
        int on = switches.Values.Count(s => s.IsChecked == true);
        summary.Text = $"مفعل {on} من {switches.Count}";
    }

    public override void OnShow() => _ = RefreshStates();

    async Task RefreshStates()
    {
        var states = await Task.Run(() => Tweaks.All.ToDictionary(t => t, SafeApplied));
        foreach (var (t, v) in states) Set(switches[t], v);
        UpdateSummary();
    }

    async Task ApplyRecommended()
    {
        var todo = Tweaks.All.Where(t => t.Recommended && t.Risk == Risk.Safe && switches[t].IsChecked != true).ToList();
        if (todo.Count == 0) { Dialogs.Toast("كل التويكات المنصوح بها مفعلة بالفعل 👌", "info"); return; }
        if (!await Dialogs.Confirm("تطبيق المنصوح به", $"سيتم تطبيق {todo.Count} تويك آمن:\n" + string.Join("\n", todo.Select(t => "• " + t.Name)) + "\n\nيفضل تعمل نقطة استعادة قبل.", "طبّق")) return;
        int ok = 0;
        foreach (var t in todo)
        {
            try { await Task.Run(t.Apply); ok++; } catch (Exception ex) { Log.Error(t.Name + ": " + ex.Message); }
        }
        await RefreshStates();
        Dialogs.Toast($"تم تطبيق {ok} تويك ✔ – أعد تشغيل الجهاز لأفضل نتيجة");
    }

    async Task EnableAll(List<Tweak> tweaks)
    {
        var todo = tweaks.Where(t => switches[t].IsChecked != true).ToList();
        if (todo.Count == 0) { Dialogs.Toast("كلها مفعلة بالفعل 👌", "info"); return; }
        var caution = todo.Where(t => t.Risk == Risk.Caution).Select(t => "• " + t.Name).ToList();
        string msg = $"سيتم تفعيل {todo.Count} تويك. بعدها تقدر تطفي أي واحد ما تبيه وبيرجع لأصله.";
        if (caution.Count > 0) msg += "\n\nانتبه، هذول فيهم شي لازم تعرفه:\n" + string.Join("\n", caution);
        if (!await Dialogs.Confirm("تفعيل الكل", msg, "فعّل الكل")) return;
        int ok = 0;
        foreach (var t in todo)
        {
            try { await Task.Run(t.Apply); ok++; } catch (Exception ex) { Log.Error(t.Name + ": " + ex.Message); }
        }
        await RefreshStates();
        Dialogs.Toast($"تم تفعيل {ok} تويك ✔ – أعد تشغيل الجهاز لأفضل نتيجة");
    }

    async Task RevertAll()
    {
        var on = Tweaks.All.Where(t => switches[t].IsChecked == true).ToList();
        if (on.Count == 0) { Dialogs.Toast("ما في تويكات مفعلة", "info"); return; }
        if (!await Dialogs.Confirm("إرجاع كل شي", $"سيتم إرجاع {on.Count} تويك لقيمها الأصلية.", "إرجاع", true)) return;
        foreach (var t in on) { try { await Task.Run(t.Revert); } catch (Exception ex) { Log.Error(t.Name + ": " + ex.Message); } }
        await RefreshStates();
        Dialogs.Toast("تم إرجاع كل التويكات ✔");
    }
}
