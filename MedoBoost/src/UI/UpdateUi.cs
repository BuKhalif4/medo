using System.Windows;
using System.Windows.Controls;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>
/// Update flow without pop-ups: a quiet banner at the top of the page ("حدّث الآن / لاحقاً / ما الجديد")
/// with a progress bar while downloading. A bell notification is added when a new version is found.
/// </summary>
public static class UpdateUi
{
    public static readonly Border Banner = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(34, 0, 34, 14) };
    static bool busy;

    public static async Task CheckAndPrompt(bool silent)
    {
        if (busy) return;
        busy = true;
        try
        {
            if (!silent) Notify.Push("info", "التحديثات", "جاري التحقق من التحديثات...", record: false);
            var r = await Updater.CheckAsync();
            if (r == null)
            {
                if (!silent) Notify.Push("ok", "التحديثات", $"عندك آخر نسخة ({Updater.CurrentText}) 👌");
                return;
            }
            if (silent && Settings.Current.DismissedUpdateTag == r.Tag) return;
            Notify.Push("info", $"نسخة جديدة {r.Tag}", "لما تكون جاهز اضغط «حدّث الآن» من الشريط بأعلى الصفحة.", balloon: true);
            Show(r);
        }
        catch (Exception ex)
        {
            Log.Error("Update: " + ex.Message);
            if (!silent) Notify.Push("err", "التحديثات", "تعذر التحقق: " + ex.Message);
        }
        finally { busy = false; }
    }

    /// <summary>Used by the Windows self-test to take a picture of the banner without touching the network.</summary>
    public static void Preview() => Show(new Updater.Release(new Version(9, 9, 9), "v9.9.9", "", "", 69_000_000));

    static void Show(Updater.Release r)
    {
        var title = Ui.HStack(8, Ui.Txt("نسخة جديدة متوفرة", 14, "Text", FontWeights.Bold),
            Ui.Txt(r.Tag, 13, "Accent2", FontWeights.Bold).Ltr(), Ui.Txt(Util.Size(r.Size), 12, "TextMute").Ltr());
        var sub = Ui.Txt("البرنامج يتسكر ويرجع يفتح لحاله بعد التثبيت.", 12, "TextDim");
        var texts = Ui.VStack(title, sub);

        var bar = Ui.Progress(); bar.Width = 190; bar.Visibility = Visibility.Collapsed;
        var pct = Ui.Txt("", 12, "Accent2", FontWeights.SemiBold).Ltr(); pct.Visibility = Visibility.Collapsed;
        var now = Ui.Btn("حدّث الآن", "", null);
        var later = Ui.Btn("لاحقاً", null, null, "BtnGhost");
        var notes = Ui.Btn("ما الجديد", null, () => Util.OpenUrl($"https://github.com/BuKhalif4/medo/releases/tag/{r.Tag}"), "BtnGhost");
        foreach (var b in new[] { now, later, notes }) b.Padding = new Thickness(14, 7, 14, 7);
        var buttons = Ui.HStack(8, now, later, notes);
        var progress = Ui.HStack(10, bar, pct);

        var host = new Grid();
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        host.ColumnDefinitions.Add(new ColumnDefinition());
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.B("AccentGrad"), Child = Ui.Icon("", 18), Margin = new Thickness(0, 0, 0, 0) };
        Grid.SetColumn(texts, 1); texts.Margin = new Thickness(12, 0, 12, 0);
        Grid.SetColumn(buttons, 2); Grid.SetColumn(progress, 2);
        host.Children.Add(icon); host.Children.Add(texts); host.Children.Add(buttons); host.Children.Add(progress);
        progress.Visibility = Visibility.Collapsed;

        later.Click += (_, _) =>
        {
            Settings.Current.DismissedUpdateTag = r.Tag; Settings.Save();
            Banner.Visibility = Visibility.Collapsed;
        };
        now.Click += async (_, _) =>
        {
            buttons.Visibility = Visibility.Collapsed; progress.Visibility = Visibility.Visible; bar.Visibility = pct.Visibility = Visibility.Visible;
            sub.Text = "جاري تنزيل التحديث...";
            try
            {
                await Updater.InstallAsync(r, new Progress<double>(p => { bar.Value = p; pct.Text = $"{p:0}%"; }));
                Settings.Save();
                MainWindow.Quitting = true; Tray.Remove();
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error("Update: " + ex.Message);
                sub.Text = "فشل التحديث: " + ex.Message;
                progress.Visibility = Visibility.Collapsed; buttons.Visibility = Visibility.Visible;
            }
        };

        Banner.Child = host;
        Banner.Background = Ui.Solid("#2A7C3AED");
        Banner.BorderBrush = Ui.B("Accent"); Banner.BorderThickness = new Thickness(1);
        Banner.CornerRadius = new CornerRadius(14); Banner.Padding = new Thickness(14, 12, 14, 12);
        Banner.Visibility = Visibility.Visible;
        Ui.FadeIn(Banner, 0, -10, 320);
    }
}
