using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace MedoBoost.Core;

/// <summary>
/// Self-update from this project's own GitHub releases (BuKhalif4/medo). Nothing else is contacted.
/// Every release is built and tested on Windows by the repo's CI before it is published.
/// </summary>
public static class Updater
{
    const string Api = "https://api.github.com/repos/BuKhalif4/medo/releases/latest";

    public record Release(Version Version, string Tag, string Notes, string Url, long Size, string ShaUrl = "");

    public static Version Current
    {
        get
        {
            var v = typeof(Updater).Assembly.GetName().Version ?? new Version(1, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    public static string CurrentText => $"v{Current.Major}.{Current.Minor}.{Current.Build}";

    static HttpClient Http()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("KHABoost-Updater");
        return h;
    }

    public static string LastError { get; private set; } = "";

    static void TryDelete(string f) { try { File.Delete(f); } catch (Exception ex) { Log.Warn("Update cleanup: " + ex.Message); } }

    static Release Make(string tag, string notes, string url, long size, string shaUrl = "")
    {
        // the tag ends up inside a download URL, so only plain version tags are accepted
        if (!System.Text.RegularExpressions.Regex.IsMatch(tag, @"^[vV]?\d+\.\d+(\.\d+)?$")) return null;
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;
        v = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        return v <= Current ? null : new Release(v, tag, notes, url, size, shaUrl);
    }

    /// <summary>
    /// Returns the newer release, or null if this is the latest. If GitHub can't be reached at all, LastError says why
    /// (so "you're on the latest" is never shown for a failed check). Tries the API first, then the plain release page,
    /// which still works when the API is rate-limited or blocked.
    /// </summary>
    public static async Task<Release> CheckAsync()
    {
        LastError = "";
        try
        {
            using var h = Http();
            h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var doc = JsonDocument.Parse(await h.GetStringAsync(Api));
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            string shaUrl = "";
            foreach (var a in root.GetProperty("assets").EnumerateArray())
                if (string.Equals(a.GetProperty("name").GetString(), "KHABoost.exe.sha256", StringComparison.OrdinalIgnoreCase))
                    shaUrl = a.GetProperty("browser_download_url").GetString() ?? "";
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                var an = a.GetProperty("name").GetString();
                if (!string.Equals(an, "KHABoost.exe", StringComparison.OrdinalIgnoreCase) && !string.Equals(an, "MedoBoost.exe", StringComparison.OrdinalIgnoreCase)) continue;
                var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                return Make(tag, notes, a.GetProperty("browser_download_url").GetString(), a.GetProperty("size").GetInt64(), shaUrl);
            }
            return null;
        }
        catch (Exception ex) { Log.Warn("Update check (API): " + ex.Message); LastError = ex.Message; }

        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var h = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("KHABoost-Updater");
            using var resp = await h.GetAsync("https://github.com/BuKhalif4/medo/releases/latest");
            var loc = resp.Headers.Location?.ToString() ?? "";
            int k = loc.LastIndexOf("/tag/", StringComparison.Ordinal);
            if (k < 0) throw new Exception("no release page");
            var tag = loc[(k + 5)..];
            LastError = "";
            return Make(tag, "", $"https://github.com/BuKhalif4/medo/releases/download/{tag}/KHABoost.exe", 0, $"https://github.com/BuKhalif4/medo/releases/download/{tag}/KHABoost.exe.sha256");
        }
        catch (Exception ex) { Log.Warn("Update check (page): " + ex.Message); if (LastError.Length == 0) LastError = ex.Message; }
        return null;
    }

    /// <summary>Downloads the new exe, then swaps it in after this process exits and starts it again.</summary>
    public static async Task InstallAsync(Release r, IProgress<double> progress)
    {
        var dir = Path.Combine(Util.DataDir, "update");
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, "KHABoost.new.exe");
        using (var h = Http())
        using (var resp = await h.GetAsync(r.Url, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? r.Size;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(tmp);
            var buf = new byte[81920];
            long done = 0; int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                done += n;
                if (total > 0) progress?.Report(done * 100.0 / total);
            }
        }
        var len = new FileInfo(tmp).Length;
        if (len < 5_000_000) { TryDelete(tmp); throw new Exception("الملف اللي نزل ناقص – جرّب مرة ثانية"); }

        // integrity: the release publishes KHABoost.exe.sha256 next to the exe. No hash, or a different one → nothing is installed.
        // (This catches corrupted / cut-off downloads. Proof of who built the file needs code signing.)
        string want = "";
        try
        {
            using var h = Http();
            var shaUrl = string.IsNullOrEmpty(r.ShaUrl) ? r.Url + ".sha256" : r.ShaUrl;
            var m = System.Text.RegularExpressions.Regex.Match(await h.GetStringAsync(shaUrl), "[0-9a-fA-F]{64}");
            if (m.Success) want = m.Value.ToLowerInvariant();
        }
        catch (Exception ex) { Log.Warn("Update hash fetch: " + ex.Message); }
        if (want == "") { TryDelete(tmp); throw new Exception("هذا الإصدار ما فيه ملف تحقق (sha256)، لذلك ما ثبّتّه. حمّله يدوياً من صفحة المشروع."); }
        string got;
        await using (var fs = File.OpenRead(tmp)) got = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(fs)).ToLowerInvariant();
        if (got != want) { TryDelete(tmp); Log.Error($"تحديث: الـ hash ما يطابق ({got} بدل {want})"); throw new Exception("الملف اللي نزل ما يطابق التحقق (sha256). ما ثبّتّه."); }

        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
        var backup = Path.Combine(dir, "KHABoost.previous.exe");
        var script = Path.Combine(dir, "update.cmd");
        int pid = Environment.ProcessId;
        // wait for KHA Boost to close, keep the old exe as a backup, put the new one in place, start it
        var ulog = Path.Combine(dir, "update.log");
        File.WriteAllText(script, $"""
            @echo off
            :wait
            tasklist /FI "PID eq {pid}" 2>nul | find "{pid}" >nul && (timeout /t 1 /nobreak >nul & goto wait)
            echo [%date% %time%] swapping >> "{ulog}"
            copy /y "{exe}" "{backup}" >nul
            set /a n=0
            :retry
            copy /y "{tmp}" "{exe}" >nul 2>>"{ulog}" && goto ok
            set /a n+=1
            if %n% GEQ 20 goto fail
            timeout /t 1 /nobreak >nul
            goto retry
            :ok
            echo [%date% %time%] updated >> "{ulog}"
            del "{tmp}" >nul 2>&1
            start "" "{exe}"
            exit /b 0
            :fail
            echo [%date% %time%] FAILED to replace the exe (is the folder read-only?) >> "{ulog}"
            start "" "{exe}"
            exit /b 1
            """);
        Log.Ok($"تحديث: تم تنزيل {r.Tag} – إعادة التشغيل للتثبيت (النسخة القديمة محفوظة: {backup})");
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
    }
}
