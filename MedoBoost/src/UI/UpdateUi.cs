using System.Windows;
using MedoBoost.Core;

namespace MedoBoost.UI;

public static class UpdateUi
{
    static bool busy;

    public static async Task CheckAndPrompt(bool silent)
    {
        if (busy) return;
        busy = true;
        try
        {
            if (!silent) Dialogs.Toast("جاري التحقق من التحديثات...", "info");
            var r = await Updater.CheckAsync();
            if (r == null)
            {
                if (!silent) Dialogs.Toast($"عندك آخر نسخة ({Updater.CurrentText}) 👌");
                return;
            }
            var notes = string.IsNullOrWhiteSpace(r.Notes) ? "" : "\n\nالجديد:\n" + (r.Notes.Length > 600 ? r.Notes[..600] + "…" : r.Notes);
            if (!await Dialogs.Confirm("نسخة جديدة متوفرة 🎉",
                    $"{Updater.CurrentText} ← {r.Tag}  ({Util.Size(r.Size)}){notes}\n\nالبرنامج رح يتسكر، يتحدث، ويرجع يفتح لحاله.", "حدّث الآن", false, "بعدين"))
                return;
            Dialogs.Toast("جاري تنزيل التحديث...", "info");
            int last = -1;
            var progress = new Progress<double>(p =>
            {
                int step = (int)(p / 25);
                if (step != last && step > 0 && step < 4) { last = step; Dialogs.Toast($"التنزيل {step * 25}%", "info"); }
            });
            await Updater.InstallAsync(r, progress);
            Settings.Save();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error("Update: " + ex.Message);
            if (!silent) Dialogs.Toast("فشل التحديث: " + ex.Message, "err");
        }
        finally { busy = false; }
    }
}
