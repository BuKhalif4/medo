using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MedoBoost.Core;

public class ReqText
{
    public string Os { get; set; } = "";
    public string Cpu { get; set; } = "";
    public string Gpu { get; set; } = "";
    public double RamGb { get; set; }          // 0 = not stated
}

public class ReqResult
{
    public string Verdict { get; set; } = "unknown";   // ok | min | bad | unknown
    public List<Finding> Lines { get; set; } = new();
    public string Error { get; set; } = "";
}

/// <summary>
/// "Can my PC run it?" Reads the game's published minimum / recommended requirements from Steam's public store page
/// (only when you press the button) and compares them with this PC's RAM and graphics card.
/// The graphics comparison is an estimate: cards are ranked on a rough built-in scale, so treat the verdict as a guide.
/// </summary>
public static class GameRequirements
{
    static readonly HttpClient Http = Make();
    static HttpClient Make()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("KHABoost");
        return h;
    }

    public static async Task<ReqResult> CheckAsync(GameInfo g)
    {
        var res = new ReqResult();
        try
        {
            string id = g.Source == "Steam" ? g.AppId : await FindSteamIdAsync(g.Name);
            if (string.IsNullOrEmpty(id)) { res.Error = "ما لقيت اللعبة في متجر Steam، فما عندي متطلباتها."; return res; }

            var url = $"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(id)}&filters=pc_requirements&l=english";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            if (!doc.RootElement.TryGetProperty(id, out var node) || !node.TryGetProperty("success", out var ok) || !ok.GetBoolean()
                || !node.TryGetProperty("data", out var data) || !data.TryGetProperty("pc_requirements", out var pr) || pr.ValueKind != JsonValueKind.Object)
            { res.Error = "Steam ما ينشر متطلبات هذي اللعبة."; return res; }

            var min = pr.TryGetProperty("minimum", out var mn) ? Parse(mn.GetString() ?? "") : new ReqText();
            var rec = pr.TryGetProperty("recommended", out var rc) ? Parse(rc.GetString() ?? "") : new ReqText();
            Compare(res, min, rec);
        }
        catch (HttpRequestException ex) { res.Error = "تعذر الاتصال بـ Steam: " + ex.Message; }
        catch (TaskCanceledException) { res.Error = "انتهت مهلة الاتصال بـ Steam."; }
        catch (JsonException ex) { res.Error = "رد Steam غير مفهوم: " + ex.Message; }
        catch (InvalidOperationException ex) { res.Error = "رد Steam غير مفهوم: " + ex.Message; }
        if (res.Error != "") Log.Warn($"متطلبات {g.Name}: {res.Error}");
        return res;
    }

    static async Task<string> FindSteamIdAsync(string name)
    {
        var url = "https://store.steampowered.com/api/storesearch/?cc=us&l=english&term=" + Uri.EscapeDataString(name);
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        if (!doc.RootElement.TryGetProperty("items", out var items)) return "";
        string Norm(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]", "");
        foreach (var it in items.EnumerateArray())
            if (it.TryGetProperty("name", out var n) && Norm(n.GetString() ?? "") == Norm(name) && it.TryGetProperty("id", out var id)) return id.GetInt64().ToString();
        return "";   // only an exact name match is trusted, a wrong game would give a wrong verdict
    }

    // ---------------- parsing ----------------
    static ReqText Parse(string html)
    {
        var t = new ReqText();
        var text = Regex.Replace(html, @"<br\s*/?>|</li>", "\n", RegexOptions.IgnoreCase);
        text = System.Net.WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", ""));
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            int c = line.IndexOf(':');
            if (c <= 0) continue;
            string key = line[..c].Trim().ToLowerInvariant().TrimEnd('*', ' '), val = line[(c + 1)..].Trim();
            if (key.StartsWith("os")) t.Os = val;
            else if (key.StartsWith("processor")) t.Cpu = val;
            else if (key.StartsWith("graphics") || key.StartsWith("video")) t.Gpu = val;
            else if (key.StartsWith("memory") || key.StartsWith("ram"))
            {
                var m = Regex.Match(val, @"(\d+(?:\.\d+)?)\s*(GB|MB)", RegexOptions.IgnoreCase);
                if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    t.RamGb = m.Groups[2].Value.Equals("MB", StringComparison.OrdinalIgnoreCase) ? n / 1024 : n;
            }
        }
        return t;
    }

    // ---------------- comparison ----------------
    static void Compare(ReqResult res, ReqText min, ReqText rec)
    {
        var hw = HardwareMonitor.Instance;
        double pcRam = Math.Round(hw.Last.RamTotal / 1073741824.0);
        int pcGpu = GpuScore(hw.GpuName);
        int worst = 0;          // 0 ok, 1 meets minimum only, 2 below minimum
        bool known = false;
        void Line(string lvl, string text) => res.Lines.Add(new Finding { Level = lvl, Text = text });

        // RAM
        if (min.RamGb > 0)
        {
            known = true;
            if (pcRam + 0.5 < min.RamGb) { worst = 2; Line("bad", $"الرام: عندك {pcRam:0} GB والحد الأدنى {min.RamGb:0} GB."); }
            else if (rec.RamGb > 0 && pcRam + 0.5 < rec.RamGb) { worst = Math.Max(worst, 1); Line("warn", $"الرام: عندك {pcRam:0} GB، يكفي الأدنى ({min.RamGb:0} GB) بس الموصى به {rec.RamGb:0} GB."); }
            else Line("ok", $"الرام: عندك {pcRam:0} GB ويكفي ({min.RamGb:0} GB مطلوب).");
        }

        // GPU
        int minG = ReqGpuScore(min.Gpu), recG = ReqGpuScore(rec.Gpu);
        if (pcGpu >= 0 && minG >= 0)
        {
            known = true;
            if (pcGpu + 3 < minG) { worst = 2; Line("bad", $"كرت الشاشة ({hw.GpuName}) أضعف من الحد الأدنى ({min.Gpu})."); }
            else if (recG >= 0 && pcGpu + 3 < recG) { worst = Math.Max(worst, 1); Line("warn", $"كرت الشاشة يغطي الحد الأدنى ({min.Gpu}) بس أضعف من الموصى به ({rec.Gpu})."); }
            else Line("ok", $"كرت الشاشة ({hw.GpuName}) كافي.");
        }
        else if (!string.IsNullOrEmpty(min.Gpu)) Line("info", $"ما قدرت أقارن كرت الشاشة تلقائياً. المطلوب: {min.Gpu}");

        if (!string.IsNullOrEmpty(min.Cpu)) Line("info", $"المعالج المطلوب: {min.Cpu} (عندك: {hw.CpuName}). المعالجات ما أقارنها تلقائياً، قارنها بنفسك.");

        res.Verdict = !known ? "unknown" : worst == 0 ? "ok" : worst == 1 ? "min" : "bad";
        res.Lines.Insert(0, new Finding
        {
            Level = res.Verdict switch { "ok" => "ok", "min" => "warn", "bad" => "bad", _ => "info" },
            Text = res.Verdict switch
            {
                "ok" => "جهازك يشغّلها بإذن الله، حتى على الإعدادات الموصى بها.",
                "min" => "جهازك يشغّلها على الحد الأدنى. خفّض الإعدادات للأداء الأفضل.",
                "bad" => "جهازك أقل من الحد الأدنى. غالباً بتتقطع أو ما تشتغل.",
                _ => "ما قدرت أحكم: Steam ما يذكر رام أو كرت شاشة واضحين لهذي اللعبة.",
            },
        });
    }

    // ---------------- rough graphics-card scale (0-100) ----------------
    static readonly Dictionary<int, int> Nv = new()
    {
        [5090] = 100, [5080] = 94, [5070] = 86, [5060] = 78, [4090] = 98, [4080] = 92, [4070] = 82, [4060] = 70,
        [3090] = 90, [3080] = 85, [3070] = 76, [3060] = 66, [3050] = 52, [2080] = 72, [2070] = 66, [2060] = 58,
        [1660] = 50, [1650] = 42, [1080] = 62, [1070] = 55, [1060] = 46, [1050] = 35, [1030] = 20,
        [980] = 48, [970] = 44, [960] = 36, [950] = 30, [780] = 36, [770] = 30, [760] = 28, [750] = 24, [740] = 18, [730] = 14, [720] = 12, [710] = 10,
    };
    static readonly Dictionary<int, int> Amd = new()
    {
        [7900] = 95, [7800] = 88, [7700] = 80, [7600] = 70, [6900] = 90, [6800] = 86, [6700] = 78, [6600] = 66, [6500] = 45, [6400] = 38,
        [5700] = 68, [5600] = 58, [5500] = 48, [590] = 52, [580] = 48, [570] = 44, [560] = 34, [550] = 26, [480] = 46, [470] = 42, [460] = 32,
    };
    static readonly Dictionary<int, int> Arc = new() { [770] = 62, [750] = 58, [580] = 50, [380] = 35, [310] = 25 };

    static int Nearest(Dictionary<int, int> t, int n)
    {
        int best = -1;
        foreach (var k in t.Keys) if (k <= n && (best < 0 || k > best)) best = k;
        return best < 0 ? -1 : t[best];
    }

    /// <summary>Score of one card name, -1 if unknown.</summary>
    public static int GpuScore(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;
        var u = name.ToUpperInvariant();
        int bonus = (u.Contains(" TI") || u.Contains("SUPER") || u.Contains(" XT") ? 4 : 0) - (u.Contains("LAPTOP") || u.Contains("MOBILE") || u.Contains("MAX-Q") ? 8 : 0);
        Match m;
        if ((m = Regex.Match(u, @"\b(?:RTX|GTX|GT)\s*(\d{3,4})")).Success) { int s = Nearest(Nv, int.Parse(m.Groups[1].Value)); return s < 0 ? -1 : Math.Clamp(s + bonus, 0, 100); }
        if ((m = Regex.Match(u, @"\bRX\s*(\d{3,4})")).Success) { int s = Nearest(Amd, int.Parse(m.Groups[1].Value)); return s < 0 ? -1 : Math.Clamp(s + bonus, 0, 100); }
        if ((m = Regex.Match(u, @"\bARC\b.*?\bA(\d{3})")).Success) { int s = Nearest(Arc, int.Parse(m.Groups[1].Value)); return s < 0 ? -1 : s; }
        if ((m = Regex.Match(u, @"\bVEGA\s*(\d+)")).Success) return int.Parse(m.Groups[1].Value) >= 56 ? 58 : 18;
        if (u.Contains("IRIS XE")) return 24;
        if (u.Contains("UHD")) return 14;
        if (u.Contains("HD GRAPHICS")) return 8;
        if (Regex.IsMatch(u, @"\bR9\b")) return 40;
        if (Regex.IsMatch(u, @"\bR7\b")) return 24;
        return -1;
    }

    /// <summary>"NVIDIA GTX 1050 or AMD RX 560": any one card is enough, so the weakest listed one is the requirement.</summary>
    static int ReqGpuScore(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return -1;
        var scores = Regex.Split(text, @"\s+or\s+|/|;|\||,\s*(?=NVIDIA|AMD|Radeon|GeForce|Intel)", RegexOptions.IgnoreCase)
            .Select(GpuScore).Where(x => x >= 0).ToList();
        return scores.Count == 0 ? -1 : scores.Min();
    }
}
