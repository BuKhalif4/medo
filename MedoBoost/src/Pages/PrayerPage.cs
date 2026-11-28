using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MedoBoost.Core;
using MedoBoost.UI;

namespace MedoBoost.Pages;

public class PrayerPage : PageBase
{
    readonly StackPanel rows = new();
    readonly TextBlock nextName = Ui.GradText("", 30);
    readonly TextBlock countdown = Ui.Txt("", 22, "Accent2", FontWeights.Bold).Ltr();
    readonly TextBlock dateLine = Ui.Txt("", 12.5, "TextDim");
    readonly TextBlock cityHint = Ui.Txt("", 12, "Warn", null, TextWrapping.Wrap);
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    PrayerTime next;

    public PrayerPage()
    {
        var s = Settings.Current;
        var root = new StackPanel();
        root.Children.Add(Ui.Title("مواقيت الصلاة"));
        root.Children.Add(Ui.Txt("تُحسب على جهازك من موقع الشمس، بدون إنترنت. اختر مدينتك مرة وحدة، والتنبيه يطلع لك حتى وأنت داخل اللعبة.", 13, "TextDim", null, TextWrapping.Wrap).M(0, 4, 0, 16));

        // ---- next prayer
        root.Children.Add(Ui.Card(Ui.VStack(
            Ui.Txt("الصلاة القادمة", 12.5, "TextDim"), nextName, countdown.M(0, 2, 0, 6), dateLine), 20).M(0, 0, 0, 14));

        // ---- today
        root.Children.Add(Ui.Card(Ui.VStack(Ui.Txt("مواقيت اليوم", 14, "Text", FontWeights.Bold).M(0, 0, 0, 10), rows), 16).M(0, 0, 0, 14));

        // ---- city
        var cityPills = new WrapPanel();
        string g1 = Guid.NewGuid().ToString();
        foreach (var c in Prayer.Cities)
        {
            var city = c;
            var rb = new RadioButton { Style = Ui.S("Pill"), Content = new TextBlock { Text = L.En ? c.En : c.Ar }, GroupName = g1, IsChecked = s.PrayerCity == c.Key };
            rb.Checked += (_, _) => { Settings.Current.PrayerCity = city.Key; Settings.Current.PrayerLat = city.Lat; Settings.Current.PrayerLon = city.Lon; Settings.Save(); Refresh(); };
            cityPills.Children.Add(rb);
        }
        var methodPills = new WrapPanel();
        string g2 = Guid.NewGuid().ToString();
        foreach (var m in Prayer.Methods)
        {
            var key = m.Key;
            var rb = new RadioButton { Style = Ui.S("Pill"), Content = new TextBlock { Text = L.En ? m.En : m.Ar }, GroupName = g2, IsChecked = s.PrayerMethod == key };
            rb.Checked += (_, _) => { Settings.Current.PrayerMethod = key; Settings.Save(); Refresh(); };
            methodPills.Children.Add(rb);
        }
        root.Children.Add(Ui.Card(Ui.VStack(
            Ui.Txt("المدينة", 14, "Text", FontWeights.Bold).M(0, 0, 0, 8), cityPills, cityHint.M(0, 8, 0, 0),
            Ui.Txt("طريقة الحساب", 14, "Text", FontWeights.Bold).M(0, 14, 0, 8), methodPills,
            Ui.Txt("اختلاف دقيقة أو دقيقتين عن التقويم المحلي طبيعي. لو بلدك تعتمد طريقة ثانية جرّب غيرها.", 11.5, "TextMute", null, TextWrapping.Wrap).M(0, 8, 0, 0)), 16).M(0, 0, 0, 14));

        // ---- alerts
        var before = new WrapPanel();
        string g3 = Guid.NewGuid().ToString();
        foreach (var mins in new[] { 0, 5, 10, 15, 30 })
        {
            int mm = mins;
            var rb = new RadioButton { Style = Ui.S("Pill"), Content = new TextBlock { Text = mm == 0 ? L.T("وقت الأذان فقط") : $"{mm} " + L.T("دقيقة قبلها") }, GroupName = g3, IsChecked = s.PrayerMinutesBefore == mm };
            rb.Checked += (_, _) => { Settings.Current.PrayerMinutesBefore = mm; Settings.Save(); };
            before.Children.Add(rb);
        }
        root.Children.Add(Ui.Card(Ui.VStack(
            Ui.Txt("التنبيهات", 14, "Text", FontWeights.Bold).M(0, 0, 0, 10),
            Ui.Check(Ui.Txt("نبّهني بوقت الصلاة (حتى أثناء اللعب)", 13, "Text"), s.PrayerAlerts, v => { Settings.Current.PrayerAlerts = v; Settings.Save(); }).M(0, 0, 0, 8),
            Ui.Check(Ui.Txt("شغّل صوت مع التنبيه", 13, "Text"), s.PrayerSound, v => { Settings.Current.PrayerSound = v; Settings.Save(); }).M(0, 0, 0, 10),
            Ui.Txt("نبّهني أيضاً قبل الصلاة بـ", 12.5, "TextDim").M(0, 0, 0, 6), before,
            Ui.Btn("جرّب التنبيه", "", () =>
            {
                Notify.Push("info", "حان وقت الصلاة 🕌", "هذا تنبيه تجريبي", balloon: true);
                if (Settings.Current.PrayerSound) { try { System.Media.SystemSounds.Exclamation.Play(); } catch (Exception ex) { Log.Warn("Prayer sound: " + ex.Message); } }
            }, "BtnGhost").M(0, 12, 0, 0)), 16));

        Children.Add(Ui.Scroll(root));
        timer.Tick += (_, _) => { if (next != null && next.Time <= DateTime.Now) Refresh(); else Tickk(); };
    }

    public override void OnShow() { Refresh(); timer.Start(); }
    public override void OnHide() => timer.Stop();

    void Refresh()
    {
        try
        {
            var s = Settings.Current;
            cityHint.Text = s.PrayerCity == "" ? L.T("لم تختر مدينة بعد: المواقيت الحالية لمكة المكرمة. اختر مدينتك.") : "";
            rows.Children.Clear();
            var now = DateTime.Now;
            var today = Prayer.Today();
            next = Prayer.Next(now);
            foreach (var p in today)
            {
                bool isNext = next != null && p.Key == next.Key && p.Time.Date == next.Time.Date;
                string br = isNext ? "Accent2" : p.Key == "sunrise" ? "TextMute" : "Text";
                var w = isNext ? FontWeights.Bold : FontWeights.Normal;
                rows.Children.Add(Ui.Cols(new[] { "*", "Auto" }, Ui.Txt(p.Name, 14, br, w), Ui.Txt(p.Time.ToString("HH:mm"), 15, br, w).Ltr()).M(0, 4, 0, 4));
            }
            dateLine.Text = now.ToString(L.En ? "dddd, d MMMM yyyy" : "dddd d MMMM yyyy", L.En ? new System.Globalization.CultureInfo("en-US") : new System.Globalization.CultureInfo("ar")) + "   •   " + Prayer.HijriDate(now);
            Tickk();
        }
        catch (Exception ex) { Log.Warn("Prayer page: " + ex.Message); }
    }

    void Tickk()
    {
        if (next == null) { nextName.Text = ""; countdown.Text = ""; return; }
        var left = next.Time - DateTime.Now;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        nextName.Text = L.T(next.Name);
        countdown.Text = $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
    }
}
