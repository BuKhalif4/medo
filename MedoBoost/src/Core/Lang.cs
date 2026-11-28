using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace MedoBoost.Core;

/// <summary>
/// Arabic ⇄ English. The UI is written in Arabic; in English mode every TextBlock (buttons, tooltips, dialogs, toasts…)
/// is translated at the moment its text is set, so dynamic text ("تم تحرير 1.2 GB") is covered too.
/// Order of lookup: exact text → text with numbers/names filled in ("{}" templates) → known phrases inside the text.
/// </summary>
public static class L
{
    public static bool En => Settings.Current.Language == "en";
    public static FlowDirection Flow => En ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

    static readonly Dictionary<string, string> exact = new();
    static readonly List<(Regex rx, string tpl)> templates = new();
    static readonly List<(string ar, string en)> frags = new();
    static readonly Regex HasArabic = new(@"[\u0600-\u06FF]", RegexOptions.Compiled);
    static readonly Dictionary<string, string> cache = new();
    static bool ready;

    static readonly (string ar, string en)[] Extra =
    {
        ("من الصفر", "Fresh"), ("تنظيف", "Clean"), ("البرامج", "Apps"), ("تسريع", "Boost"), ("بدء", "Startup"), ("الحماية", "Security"),
        ("إلغاء تثبيت", "Uninstall"), ("أدوات", "Tools"), ("حول", "About"),
        ("تخطي", "Skip"), ("أهلاً بك", "Welcome"), ("دخول", "Enter"), ("اللغة", "Language"), ("التصميم", "Design"), ("اللغة / Language", "Language"),
        ("إنشاء نقطة استعادة للنظام الآن (منصوح)", "Create a system restore point now (recommended)"),
        ("عرض شاشة الترحيب", "Replay the intro"),
        ("تغيير اللغة أو التصميم يعيد تشغيل البرنامج تلقائياً.", "Changing the language or design restarts the app automatically."),
        ("جاري إعادة تشغيل البرنامج...", "Restarting the app..."),
        ("جهّز للعب (Ctrl+Shift+G)", "Game Ready (Ctrl+Shift+G)"),
        ("نقطة الاستعادة جاهزة", "Restore point is ready"),
        ("تطبيق المحدد", "Apply selected"), ("مسح التحديد", "Clear selection"), ("محدد {}", "Selected {}"), ("حدد هذا التويك", "Select this tweak"),
        ("اختر تويكات أول (المربعات بجانب كل تويك)", "Pick some tweaks first (the boxes next to each tweak)"),
        ("كل التويكات المحددة مفعلة بالفعل 👌", "All selected tweaks are already on 👌"),
        ("اختيار مخصص: ضع علامة على التويكات اللي تبيها (المربعات بجانب كل تويك) ثم اضغط «تطبيق المحدد» – غير المحدد ما ينلمس.", "Custom pick: tick the tweaks you want (the boxes next to each one) then press “Apply selected” – anything not ticked is left alone."),
        ("سيتم تطبيق {} تويك محدد:\n", "{} selected tweaks will be applied:\n"),
        ("\n\nغير المحدد ما ينلمس.", "\n\nAnything not selected is left alone."),
        ("تطبيق", "Apply"), ("سيتم تطبيق {} تويك: {} مفعّل (يعاد تطبيقه) و {} جديد.", "{} tweaks will be applied: {} already on (re-applied) and {} new."),
        ("\n\nالجديد:\n", "\n\nNew:\n"), ("\n\nالمطفي وغير المحدد ما ينلمس.", "\n\nSwitched-off and unticked tweaks are left alone."),
        ("تم تطبيق {} من {}. ما انطبق: ", "Applied {} of {}. Not applied: "),
        ("أنوية", "cores"), ("مسار", "threads"), ("منافذ", "slots"), ("من", "of"), ("تعريف", "driver"), ("مستخدم", "used"), ("متاح", "free"), ("ناشر غير معروف", "Unknown publisher"),
    };

    static void Build()
    {
        if (ready) return;
        ready = true;
        foreach (var (ar, en) in LangData.Pairs.Concat(Extra).Concat(LangSessions.Pairs))
        {
            if (!ar.Contains("{}")) { exact[ar] = en; if (ar.Trim().Length >= 8) frags.Add((ar, en)); continue; }
            var parts = ar.Split("{}");
            var rx = "^" + string.Join("(.+?)", parts.Select(Regex.Escape)) + "$";
            templates.Add((new Regex(rx, RegexOptions.Singleline | RegexOptions.Compiled), en));
        }
        frags.Sort((a, b) => b.ar.Length.CompareTo(a.ar.Length));
    }

    /// <summary>Translate when the app language is English. Returns the input unchanged otherwise.</summary>
    public static string T(string s)
    {
        if (!En || string.IsNullOrEmpty(s) || !HasArabic.IsMatch(s)) return s;
        Build();
        if (cache.TryGetValue(s, out var hit)) return hit;
        var r = Translate(s);
        if (cache.Count < 4000) cache[s] = r;
        return r;
    }

    static string Translate(string s)
    {
        if (exact.TryGetValue(s, out var e)) return e;
        foreach (var (rx, tpl) in templates)
        {
            var m = rx.Match(s);
            if (!m.Success) continue;
            int n = 0;
            return Regex.Replace(tpl, @"\{(\d*)\}", x => { int idx = x.Groups[1].Length > 0 ? int.Parse(x.Groups[1].Value) : n++; return idx < m.Groups.Count - 1 ? Translate(m.Groups[idx + 1].Value) : ""; });
        }
        if (s.Contains('\n'))
        {
            var lines = s.Split('\n');
            if (lines.Length > 1 && lines.Any(l => HasArabic.IsMatch(l))) return string.Join("\n", lines.Select(l => HasArabic.IsMatch(l) ? Translate(l) : l));
        }
        var r = s;
        foreach (var (ar, en) in frags) if (r.Contains(ar)) r = r.Replace(ar, en);
        r = Regex.Replace(r, @"^إيقاف ", "Disable ");
        r = Regex.Replace(r, @"^القرص ", "Drive ");
        r = Regex.Replace(r, @"\bمن\b", "of");
        r = Regex.Replace(r, @"(\d) أنوية", "$1 cores");
        r = Regex.Replace(r, @"(\d) مسار", "$1 threads");
        r = Regex.Replace(r, @"تعريف\s*(?=[\u202A\d])", "Driver ");
        return r;
    }

    /// <summary>
    /// Translates everything currently on screen: TextBlocks (including the ones WPF creates inside buttons), tooltips and
    /// context-menu items. Called a few times per second in English mode, so text set later by code is covered too.
    /// </summary>
    public static void Sweep(DependencyObject root)
    {
        if (!En || root == null) return;
        void Walk(DependencyObject d)
        {
            if (d is TextBlock tb) { var s = tb.Text; if (!string.IsNullOrEmpty(s) && HasArabic.IsMatch(s)) { var t = T(s); if (t != s) tb.Text = t; } }
            if (d is FrameworkElement fe)
            {
                if (fe.ToolTip is string tip && HasArabic.IsMatch(tip)) fe.ToolTip = T(tip);
                if (fe.ContextMenu != null) foreach (var it in fe.ContextMenu.Items) if (it is MenuItem mi && mi.Header is string h && HasArabic.IsMatch(h)) mi.Header = T(h);
            }
            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++) Walk(System.Windows.Media.VisualTreeHelper.GetChild(d, i));
        }
        Walk(root);
    }

    public static void InstallHook() { }   // (kept so callers stay valid; the sweep is driven by the main window)

    /// <summary>Always English (used by the boot-up intro, whose hardware lines are technical readouts in both languages).</summary>
    public static string TEn(string s)
    {
        if (string.IsNullOrEmpty(s) || !HasArabic.IsMatch(s)) return s;
        Build();
        return Translate(s);
    }

    /// <summary>Arabic words left in a text (used by the self-test to find untranslated strings).</summary>
    public static bool HasArabicText(string s) => !string.IsNullOrEmpty(s) && HasArabic.IsMatch(s);
}
