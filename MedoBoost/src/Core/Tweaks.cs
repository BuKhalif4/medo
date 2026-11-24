using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MedoBoost.Core;

public class Tweak
{
    public string Id;
    public string Category;
    public string Name;
    public string Description;
    public Risk Risk = Risk.Safe;
    public bool Recommended = true;
    public bool NeedsRestart;
    public Func<bool> IsApplied = () => false;
    public Action Apply = () => { };
    public Action Revert = () => { };
}

/// <summary>A single registry value change. Original values are backed up before every change so everything can be reverted.</summary>
public record RegOp(string Hive, string Path, string Name, object Value, RegistryValueKind Kind = RegistryValueKind.DWord, object Default = null);

/// <summary>Stores original values (before MEDO Boost touched them) in %LOCALAPPDATA%\MedoBoost\backup.json</summary>
internal static class Backup
{
    public class Entry { public bool Existed { get; set; } public string Kind { get; set; } public string Value { get; set; } }
    static readonly string FilePath = System.IO.Path.Combine(Util.DataDir, "backup.json");
    static Dictionary<string, Entry> data;

    static Dictionary<string, Entry> Data
    {
        get
        {
            if (data != null) return data;
            try { data = File.Exists(FilePath) ? JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(FilePath)) : null; } catch { }
            return data ??= new();
        }
    }

    public static bool Has(string key) => Data.ContainsKey(key);
    public static Entry Get(string key) => Data.TryGetValue(key, out var e) ? e : null;
    public static void Set(string key, Entry e) { Data[key] = e; Save(); }
    public static void Remove(string key) { if (Data.Remove(key)) Save(); }
    static void Save() { try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true })); } catch { } }
}

public static class Reg
{
    public static RegistryKey Base(string hive) => RegistryKey.OpenBaseKey(hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);

    public static object Get(string hive, string path, string name)
    {
        try { using var k = Base(hive).OpenSubKey(path); return k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames); }
        catch { return null; }
    }

    public static void Set(string hive, string path, string name, object value, RegistryValueKind kind)
    {
        using var k = Base(hive).CreateSubKey(path, true);
        if (kind == RegistryValueKind.DWord && value is long l) value = unchecked((int)l);
        if (kind == RegistryValueKind.DWord && value is uint u) value = unchecked((int)u);
        k.SetValue(name, value, kind);
    }

    public static void Delete(string hive, string path, string name)
    {
        try { using var k = Base(hive).OpenSubKey(path, true); k?.DeleteValue(name, false); } catch { }
    }

    public static bool Equal(object a, object b)
    {
        if (a == null || b == null) return a == null && b == null;
        if (a is int or uint or long && b is int or uint or long)
            return unchecked((uint)Convert.ToInt64(a)) == unchecked((uint)Convert.ToInt64(b));
        return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    static string Key(RegOp o) => $"reg|{o.Hive}\\{o.Path}|{o.Name}";

    public static void Apply(RegOp o)
    {
        var key = Key(o);
        if (!Backup.Has(key))
        {
            var cur = Get(o.Hive, o.Path, o.Name);
            RegistryValueKind kind = o.Kind;
            try { using var k = Base(o.Hive).OpenSubKey(o.Path); if (k != null && cur != null) kind = k.GetValueKind(o.Name); } catch { }
            Backup.Set(key, new Backup.Entry { Existed = cur != null, Kind = kind.ToString(), Value = cur is string[] arr ? string.Join("\0", arr) : cur?.ToString() });
        }
        Set(o.Hive, o.Path, o.Name, o.Value, o.Kind);
        Log.Info($"Registry: {o.Hive}\\{o.Path}\\{o.Name} = {o.Value}");
    }

    public static void Restore(RegOp o)
    {
        var key = Key(o);
        var b = Backup.Get(key);
        try
        {
            if (b != null)
            {
                if (!b.Existed) Delete(o.Hive, o.Path, o.Name);
                else
                {
                    var kind = Enum.Parse<RegistryValueKind>(b.Kind);
                    object v = kind switch
                    {
                        RegistryValueKind.DWord => unchecked((int)long.Parse(b.Value)),
                        RegistryValueKind.QWord => long.Parse(b.Value),
                        RegistryValueKind.MultiString => b.Value.Split('\0'),
                        _ => b.Value
                    };
                    Set(o.Hive, o.Path, o.Name, v, kind);
                }
                Backup.Remove(key);
            }
            else if (o.Default == null) Delete(o.Hive, o.Path, o.Name);
            else Set(o.Hive, o.Path, o.Name, o.Default, o.Kind);
            Log.Info($"Registry restored: {o.Hive}\\{o.Path}\\{o.Name}");
        }
        catch (Exception ex) { Log.Error($"Restore {o.Path}\\{o.Name}: {ex.Message}"); }
    }

    public static bool IsSet(RegOp o) => Equal(Get(o.Hive, o.Path, o.Name), o.Value);
}

public static class Tweaks
{
    public static readonly string[] Categories = { "الألعاب", "الأداء والنظام", "الشبكة والبنق", "الخصوصية وبرامج الخلفية", "الخدمات", "البرمجة" };
    public static List<Tweak> All { get; } = Build();

    const string MM = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    const string MMG = MM + @"\Tasks\Games";

    static Tweak RegTweak(string id, int cat, string name, string desc, RegOp[] ops, bool rec = true, Risk risk = Risk.Safe, bool restart = false, Action after = null) => new()
    {
        Id = id, Category = Categories[cat], Name = name, Description = desc, Recommended = rec, Risk = risk, NeedsRestart = restart,
        IsApplied = () => ops.All(Reg.IsSet),
        Apply = () => { foreach (var o in ops) Reg.Apply(o); after?.Invoke(); },
        Revert = () => { foreach (var o in ops) Reg.Restore(o); after?.Invoke(); },
    };

    static RegOp CU(string path, string name, object v, object def = null) => new("HKCU", path, name, v, RegistryValueKind.DWord, def);
    static RegOp LM(string path, string name, object v, object def = null) => new("HKLM", path, name, v, RegistryValueKind.DWord, def);
    static RegOp CUs(string path, string name, string v, string def = null) => new("HKCU", path, name, v, RegistryValueKind.String, def);
    static RegOp LMs(string path, string name, string v, string def = null) => new("HKLM", path, name, v, RegistryValueKind.String, def);

    static List<Tweak> Build()
    {
        var l = new List<Tweak>
        {
            // ================= Games =================
            RegTweak("g_gamemode", 0, "تفعيل وضع الألعاب (Game Mode)",
                "يعطي اللعبة أولوية على المعالج والكرت ويمنع تحديثات Windows من التثبيت أثناء اللعب.",
                new[] { CU(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1), CU(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1) }),
            RegTweak("g_gamedvr", 0, "إيقاف تسجيل الخلفية (Game DVR / Captures)",
                "يوقف تسجيل الفيديو بالخلفية الذي يستهلك من أداء الكرت والمعالج ويسبب نقص FPS.",
                new[] { CU(@"System\GameConfigStore", "GameDVR_Enabled", 0, 1), CU(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                        LM(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0) }),
            RegTweak("g_mmcss", 0, "أولوية عالية للألعاب (MMCSS)",
                "يعطي الألعاب أولوية أعلى للمعالج والكرت عبر جدولة الوسائط في Windows (GPU Priority = 8, Priority = 6, Scheduling = High).",
                new[] { LM(MMG, "GPU Priority", 8, 8), LM(MMG, "Priority", 6, 2), LMs(MMG, "Scheduling Category", "High", "Medium"), LMs(MMG, "SFIO Priority", "High", "Normal"),
                        LM(MM, "SystemResponsiveness", 10, 20) }),
            RegTweak("g_priosep", 0, "تفضيل البرنامج الأمامي (Win32PrioritySeparation)",
                "يعطي البرنامج المفتوح أمامك (اللعبة) وقت معالج أطول من برامج الخلفية. القيمة 0x26 المعروفة للألعاب.",
                new[] { LM(@"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26, 2) }),
            RegTweak("g_hags", 0, "جدولة كرت الشاشة بالهاردوير (HAGS)",
                "يقلل التأخير (latency) ويحسن الأداء على كروت RTX / RX 6000 وأحدث. مطلوب لـ DLSS Frame Generation. يحتاج إعادة تشغيل.",
                new[] { LM(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1) }, restart: true),
            new Tweak
            {
                Id = "g_windowed", Category = Categories[0], Name = "تحسين الألعاب بوضع النافذة (Windows 11)",
                Description = "يفعّل 'Optimizations for windowed games' – يقلل التأخير ويحسن الأداء للألعاب بوضع Borderless / Windowed.",
                IsApplied = () => (Reg.Get("HKCU", @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings") as string ?? "").Contains("SwapEffectUpgradeEnable=1"),
                Apply = () => SetDxGlobal("SwapEffectUpgradeEnable", "1"),
                Revert = () => SetDxGlobal("SwapEffectUpgradeEnable", "0"),
            },
            RegTweak("g_fso", 0, "إيقاف Fullscreen Optimizations (لكل الألعاب)",
                "بعض الألعاب القديمة يتحسن فيها الأداء/التأخير بالفل سكرين الحقيقي. في الألعاب الحديثة غالباً بلا فرق – جرّب وقارن.",
                new[] { CU(@"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, 0), CU(@"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1, 0),
                        CU(@"System\GameConfigStore", "GameDVR_FSEBehavior", 2, 0), CU(@"System\GameConfigStore", "GameDVR_DXGIHonorFSEWindowsCompatible", 1, 0) },
                rec: false, risk: Risk.Caution),
            RegTweak("g_mouse", 0, "إيقاف تسريع الماوس (Enhance Pointer Precision)",
                "حركة الماوس تصير 1:1 – مهم جداً لألعاب التصويب (Valorant, CS2, COD...).",
                new[] { CUs(@"Control Panel\Mouse", "MouseSpeed", "0", "1"), CUs(@"Control Panel\Mouse", "MouseThreshold1", "0", "6"), CUs(@"Control Panel\Mouse", "MouseThreshold2", "0", "10") },
                after: ApplyMouseNow),
            RegTweak("g_sticky", 0, "إيقاف نوافذ Sticky Keys المزعجة",
                "يمنع ظهور نافذة Sticky/Filter/Toggle Keys إذا ضغطت Shift أو Ctrl كثير أثناء اللعب.",
                new[] { CUs(@"Control Panel\Accessibility\StickyKeys", "Flags", "506", "510"), CUs(@"Control Panel\Accessibility\Keyboard Response", "Flags", "122", "126"),
                        CUs(@"Control Panel\Accessibility\ToggleKeys", "Flags", "58", "62") }),
            new Tweak
            {
                Id = "g_power", Category = Categories[0], Name = "خطة طاقة Ultimate Performance",
                Description = "يفعّل خطة الطاقة المخفية 'الأداء الأقصى' – المعالج يبقى جاهز بأعلى سرعة بدون تأخير. (للابتوب: تستهلك بطارية أكثر).",
                IsApplied = PowerPlanActive, Apply = ApplyPowerPlan, Revert = RevertPowerPlan,
            },
            RegTweak("g_throttle", 0, "إيقاف Power Throttling",
                "يمنع Windows من تقليل سرعة البرامج اللي بالخلفية (مثل ديسكورد والستريم). للابتوب: استهلاك بطارية أعلى.",
                new[] { LM(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1) }, rec: false),

            // ================= Performance =================
            RegTweak("p_visual", 1, "تقليل الأنيميشن والمؤثرات البصرية",
                "يوقف أنيميشن النوافذ وشريط المهام والظلال – الجهاز يحس أسرع خصوصاً على الأجهزة الضعيفة (الخطوط تبقى ناعمة).",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3, 0),
                        CUs(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", "1"),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 0, 1),
                        CU(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, 1) }, restart: true),
            RegTweak("p_transparency", 1, "إيقاف الشفافية في Windows",
                "يقلل استهلاك كرت الشاشة من واجهة Windows.",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1) }, rec: false),
            RegTweak("p_menudelay", 1, "قوائم أسرع (Menu Delay)",
                "القوائم تفتح فوراً بدل الانتظار 400ms.",
                new[] { CUs(@"Control Panel\Desktop", "MenuShowDelay", "0", "400") }),
            RegTweak("p_startupdelay", 1, "إلغاء تأخير برامج بدء التشغيل",
                "Windows يأخر برامج بدء التشغيل عمداً، هذا يلغي التأخير.",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0) }),
            RegTweak("p_shutdown", 1, "إيقاف تشغيل أسرع",
                "يقلل وقت انتظار الخدمات والبرامج المعلقة عند الإطفاء.",
                new[] { LMs(@"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "2000", "5000"), CUs(@"Control Panel\Desktop", "HungAppTimeout", "3000", "5000") }),
            RegTweak("p_bgapps", 1, "إيقاف تطبيقات المتجر في الخلفية",
                "يمنع تطبيقات Microsoft Store من العمل بالخلفية واستهلاك الرام والمعالج.",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, 0),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, 1) }),
            RegTweak("p_edge", 1, "منع Edge من العمل بالخلفية",
                "يوقف Startup Boost و الوضع الخلفي في Edge – يوفر رامات حتى لو ما فتحت Edge.",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0), LM(@"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0) }),
            RegTweak("p_chrome", 1, "منع Chrome من العمل بعد إغلاقه",
                "يمنع Chrome من البقاء شغال بالخلفية بعد ما تسكره.",
                new[] { LM(@"SOFTWARE\Policies\Google\Chrome", "BackgroundModeEnabled", 0) }),
            new Tweak
            {
                Id = "p_hibernate", Category = Categories[1], Name = "إيقاف السبات (Hibernation)", Recommended = false, Risk = Risk.Caution,
                Description = "يحذف ملف hiberfil.sys (يوفر جيجات بقدر حجم الرام) ويوقف Fast Startup. لا تفعله على لابتوب تعتمد على السبات.",
                IsApplied = () => Reg.Equal(Reg.Get("HKLM", @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled"), 0),
                Apply = () => { Util.Run("powercfg.exe", "/hibernate off"); Log.Info("powercfg /hibernate off"); },
                Revert = () => { Util.Run("powercfg.exe", "/hibernate on"); Log.Info("powercfg /hibernate on"); },
            },

            // ================= Network =================
            new Tweak
            {
                Id = "n_nagle", Category = Categories[2], Name = "إيقاف خوارزمية Nagle (بنق أقل)",
                Description = "يرسل حزم الشبكة فوراً بدل تجميعها – يقلل التأخير في بعض الألعاب الأونلاين (TCP).",
                IsApplied = () => NagleInterfaces().Any() && NagleInterfaces().All(p => Reg.Equal(Reg.Get("HKLM", p, "TcpAckFrequency"), 1) && Reg.Equal(Reg.Get("HKLM", p, "TCPNoDelay"), 1)),
                Apply = () => { foreach (var p in NagleInterfaces()) { Reg.Apply(LM(p, "TcpAckFrequency", 1)); Reg.Apply(LM(p, "TCPNoDelay", 1)); } },
                Revert = () => { foreach (var p in NagleInterfaces()) { Reg.Restore(LM(p, "TcpAckFrequency", 1)); Reg.Restore(LM(p, "TCPNoDelay", 1)); } },
            },
            RegTweak("n_throttle", 2, "إيقاف Network Throttling",
                "Windows يحدد سرعة معالجة الشبكة أثناء تشغيل الوسائط – هذا يلغي التحديد.",
                new[] { LM(MM, "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), 10) }),
            RegTweak("n_qos", 2, "إلغاء حجز 20% من الإنترنت (QoS)",
                "يتأكد أن Windows ما يحجز جزء من سرعة النت لخدماته.",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0) }),

            // ================= Privacy / background =================
            RegTweak("x_telemetry", 3, "تقليل التتبع (Telemetry)",
                "يقلل البيانات التي يرسلها Windows لمايكروسوفت (سياسة AllowTelemetry). لإيقاف خدمة التتبع نفسها شوف قسم الخدمات.",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0), LM(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1) },
                after: null),
            RegTweak("x_adid", 3, "إيقاف معرف الإعلانات", "يمنع التطبيقات من استخدام معرف إعلاني لتتبعك.",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1) }),
            RegTweak("x_suggest", 3, "إيقاف الاقتراحات والإعلانات في Windows",
                "يوقف اقتراحات قائمة ابدأ والنصائح وتثبيت التطبيقات الإعلانية تلقائياً.",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353698Enabled", 0, 1),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0, 1) }),
            RegTweak("x_bing", 3, "إيقاف بحث الويب (Bing) في قائمة ابدأ",
                "البحث في قائمة ابدأ يصير أسرع ويبحث بجهازك فقط.",
                new[] { CU(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1), CU(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0) }),
            RegTweak("x_widgets", 3, "إيقاف الويدجت (Widgets / News)",
                "يوقف لوحة الأخبار والطقس اللي تشتغل بالخلفية وتاكل رام.",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0) }),
            RegTweak("x_copilot", 3, "إيقاف Copilot", "يوقف Windows Copilot.",
                new[] { CU(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1), LM(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1) }),
            RegTweak("x_recall", 3, "إيقاف Recall (لقطات الشاشة بالذكاء الاصطناعي)", "يمنع Windows من حفظ لقطات لشاشتك (على أجهزة Copilot+).",
                new[] { CU(@"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1), LM(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1) }),
            RegTweak("x_activity", 3, "إيقاف سجل النشاطات (Activity History)", "يمنع Windows من تسجيل البرامج والملفات التي تستخدمها.",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0), LM(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
                        LM(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0) }),
            RegTweak("x_wer", 3, "إيقاف تقارير الأخطاء (Error Reporting)", "يمنع إنشاء وإرسال تقارير الأعطال (توفير مساحة ومعالج).",
                new[] { LM(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, 0) }, rec: false),

            RegTweak("x_cortana", 3, "إيقاف Cortana", "يمنع Cortana من العمل بالخلفية.",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0) }),
            RegTweak("x_edge", 3, "تنظيف Edge من الإضافات المزعجة",
                "يوقف الشريط الجانبي وCopilot داخل Edge ومساعد التسوق والاقتراحات وشاشة الترحيب. (Edge نفسه يبقى لأن برامج كثيرة تعتمد على WebView2).",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0), LM(@"SOFTWARE\Policies\Microsoft\Edge", "EdgeShoppingAssistantEnabled", 0),
                        LM(@"SOFTWARE\Policies\Microsoft\Edge", "ShowRecommendationsEnabled", 0), LM(@"SOFTWARE\Policies\Microsoft\Edge", "HideFirstRunExperience", 1),
                        LM(@"SOFTWARE\Policies\Microsoft\Edge", "PersonalizationReportingEnabled", 0), LM(@"SOFTWARE\Policies\Microsoft\Edge", "DiagnosticData", 0) }),
            RegTweak("n_dop2p", 2, "منع رفع التحديثات لأجهزة ثانية (P2P)",
                "Windows يستخدم نتك ليرسل تحديثات لأجهزة غيرك – هذا يوقفه (يوفر سرعة رفع ويقلل البنق).",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0) }),
            RegTweak("g_gamebar_popup", 0, "إيقاف نوافذ Game Bar المنبثقة",
                "يمنع رسالة «ms-gamingoverlay» ونافذة Game Bar لما تضغط زر Xbox أو بعد حذف تطبيقات Xbox.",
                new[] { CU(@"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0), CU(@"Software\Microsoft\GameBar", "ShowStartupPanel", 0),
                        CU(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AudioCaptureEnabled", 0) }),
            RegTweak("g_wudrivers", 0, "منع Windows Update من تغيير تعريف كرت الشاشة",
                "Windows أحياناً يركّب تعريف قديم فوق تعريفك. فعّل هذا إذا تحدّث تعريف NVIDIA/AMD بنفسك.",
                new[] { LM(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1) }, rec: false),
            RegTweak("g_hvci", 0, "إيقاف Memory Integrity (عزل النواة)",
                "مايكروسوفت نفسها تقول إنه ممكن يقلل أداء الألعاب. إيقافه يرفع FPS على أجهزة كثيرة، لكنه يقلل الحماية. WSL و Docker والمحاكيات تضل تشتغل عادي. يحتاج ريستارت.",
                new[] { LM(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0) }, rec: false, risk: Risk.Caution, restart: true),
            RegTweak("p_endtask", 1, "زر «إنهاء المهمة» في شريط المهام",
                "كليك يمين على أي برنامج بشريط المهام ← End task. (Windows 11)",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", 1) }),
            new Tweak
            {
                Id = "p_classicmenu", Category = Categories[1], Name = "قائمة الكليك يمين الكلاسيكية (Windows 11)", Recommended = false, NeedsRestart = true,
                Description = "يرجع قائمة الكليك يمين الكاملة بدل «Show more options». تشتغل بعد إعادة تشغيل Explorer.",
                IsApplied = () => { try { using var k = Reg.Base("HKCU").OpenSubKey(ClassicMenuKey); return k != null; } catch { return false; } },
                Apply = () => { using var k = Reg.Base("HKCU").CreateSubKey(ClassicMenuKey, true); k.SetValue("", "", Microsoft.Win32.RegistryValueKind.String); Log.Info("Classic context menu on"); },
                Revert = () => { try { Reg.Base("HKCU").DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false); } catch { } Log.Info("Classic context menu off"); },
            },

            // ================= Programming =================
            RegTweak("d_longpaths", 5, "تفعيل المسارات الطويلة (Long Paths)",
                "يحل أخطاء node_modules و git و Python لما المسار يتعدى 260 حرف.",
                new[] { LM(@"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled", 1, 0) }),
            RegTweak("d_ext", 5, "إظهار امتدادات الملفات (.js .py .exe)",
                "ضروري للمبرمج – ويحميك من ملفات مثل photo.jpg.exe.",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, 1) }),
            RegTweak("d_hidden", 5, "إظهار الملفات المخفية (.git .env)",
                "يظهر الملفات والمجلدات المخفية في Explorer.",
                new[] { CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", 1, 2) }, rec: false),
            RegTweak("d_devmode", 5, "وضع المطور (Developer Mode)",
                "يسمح بإنشاء Symlinks بدون صلاحية مسؤول (npm/pnpm و git يحتاجوها) وتشغيل تطبيقات التطوير.",
                new[] { LM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", "AllowDevelopmentWithoutDevLicense", 1, 0) }),
            new Tweak
            {
                Id = "d_wslmem", Category = Categories[5], Name = "تحديد رام WSL و Docker",
                Description = "WSL و Docker (عملية vmmem) ممكن ياكلوا رامات كثير وما يرجعوها. هذا يحدّد حدهم لنص الرام ويخليهم يرجّعوا الرام الفاضية تلقائياً. يشتغل بعد wsl --shutdown.",
                IsApplied = () => { try { var f = WslConfigPath; return File.Exists(f) && File.ReadAllText(f).Contains("MEDO Boost"); } catch { return false; } },
                Apply = ApplyWslConfig,
                Revert = RevertWslConfig,
            },

            // ================= Services =================
            ServiceTweak("s_diag", "خدمة التتبع (DiagTrack)", "تجمع بيانات الاستخدام وترسلها. إيقافها آمن.", new[] { "DiagTrack", "dmwappushservice" }, true),
            ServiceTweak("s_sysmain", "SysMain (Superfetch)", "تحمّل البرامج مسبقاً بالرام. على SSD فائدتها قليلة وأحياناً تسبب استهلاك قرص عالي. آمن للإيقاف.", new[] { "SysMain" }, false),
            ServiceTweak("s_search", "فهرسة البحث (Windows Search)", "تقلل استخدام القرص، لكن البحث في قائمة ابدأ والملفات يصير أبطأ.", new[] { "WSearch" }, false, Risk.Caution),
            ServiceTweak("s_xbox", "خدمات Xbox Live", "إذا ما تستخدم Xbox App أو Game Pass. (لا تطفيها إذا تلعب من Game Pass).", new[] { "XblAuthManager", "XblGameSave", "XboxNetApiSvc" }, false, Risk.Caution),
            ServiceTweak("s_print", "خدمة الطباعة (Print Spooler)", "إذا ما عندك طابعة. (أطفئها فقط إذا ما تطبع أبداً).", new[] { "Spooler" }, false, Risk.Caution),
            ServiceTweak("s_fax", "خدمة الفاكس", "غير مستخدمة لأغلب الناس.", new[] { "Fax" }, true),
            ServiceTweak("s_remote", "Remote Registry", "تسمح بتعديل الريجستري عن بعد – إيقافها أكثر أماناً.", new[] { "RemoteRegistry" }, true),
            ServiceTweak("s_maps", "مدير الخرائط (Maps Broker)", "تحميل خرائط بدون نت – غير مستخدم غالباً.", new[] { "MapsBroker" }, true),
            ServiceTweak("s_retail", "Retail Demo", "وضع عرض المتاجر – غير مستخدم.", new[] { "RetailDemo" }, true),
            ServiceTweak("s_wer", "خدمة تقارير الأخطاء (WerSvc)", "ترسل تقارير الأعطال لمايكروسوفت.", new[] { "WerSvc" }, false),
        };
        return l;
    }

    const string ClassicMenuKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";

    // ---------------- WSL ----------------
    static string WslConfigPath => System.IO.Path.Combine(Util.UserProfile, ".wslconfig");
    static string WslBackupPath => System.IO.Path.Combine(Util.DataDir, "wslconfig.backup");

    static void ApplyWslConfig()
    {
        var f = WslConfigPath;
        var m = new Native.MEMORYSTATUSEX();
        Native.GlobalMemoryStatusEx(m);
        int gb = Math.Max(2, (int)(m.ullTotalPhys / 1073741824UL / 2));
        var lines = File.Exists(f) ? File.ReadAllLines(f).ToList() : new List<string>();
        if (File.Exists(f) && !File.Exists(WslBackupPath)) File.Copy(f, WslBackupPath);
        lines.RemoveAll(l => l.TrimStart().StartsWith("memory", StringComparison.OrdinalIgnoreCase) || l.TrimStart().StartsWith("autoMemoryReclaim", StringComparison.OrdinalIgnoreCase) || l.Contains("MEDO Boost"));
        int idx = lines.FindIndex(l => l.Trim().Equals("[wsl2]", StringComparison.OrdinalIgnoreCase));
        if (idx < 0) { lines.Insert(0, "[wsl2]"); idx = 0; }
        lines.Insert(idx + 1, "# MEDO Boost: limit RAM for gaming");
        lines.Insert(idx + 2, $"memory={gb}GB");
        int exp = lines.FindIndex(l => l.Trim().Equals("[experimental]", StringComparison.OrdinalIgnoreCase));
        if (exp < 0) { lines.Add(""); lines.Add("[experimental]"); exp = lines.Count - 1; }
        lines.Insert(exp + 1, "autoMemoryReclaim=gradual");
        File.WriteAllLines(f, lines);
        Log.Ok($".wslconfig → memory={gb}GB, autoMemoryReclaim=gradual");
    }

    static void RevertWslConfig()
    {
        var f = WslConfigPath;
        if (File.Exists(WslBackupPath)) { File.Copy(WslBackupPath, f, true); File.Delete(WslBackupPath); }
        else if (File.Exists(f))
        {
            var lines = File.ReadAllLines(f).Where(l => !l.Contains("MEDO Boost") && !l.TrimStart().StartsWith("autoMemoryReclaim") && !l.TrimStart().StartsWith("memory=")).ToList();
            if (lines.All(l => string.IsNullOrWhiteSpace(l) || l.Trim().StartsWith("["))) File.Delete(f); else File.WriteAllLines(f, lines);
        }
        Log.Info(".wslconfig restored");
    }

    // ---------------- Services ----------------
    static Tweak ServiceTweak(string id, string name, string desc, string[] services, bool rec, Risk risk = Risk.Safe) => new()
    {
        Id = id, Category = Categories[4], Name = "إيقاف " + name, Description = desc + "  [" + string.Join(", ", services) + "]",
        Recommended = rec, Risk = risk,
        IsApplied = () =>
        {
            var existing = services.Where(ServiceExists).ToList();
            // a service that isn't installed can't run → counts as "off"
            return existing.All(s => Reg.Equal(Reg.Get("HKLM", $@"SYSTEM\CurrentControlSet\Services\{s}", "Start"), 4));
        },
        Apply = () =>
        {
            foreach (var s in services.Where(ServiceExists))
            {
                var key = "svc|" + s;
                if (!Backup.Has(key)) Backup.Set(key, new Backup.Entry { Existed = true, Kind = "DWord", Value = Reg.Get("HKLM", $@"SYSTEM\CurrentControlSet\Services\{s}", "Start")?.ToString() ?? "3" });
                Util.Run("sc.exe", $"stop {s}", 30000);
                var r = Util.Run("sc.exe", $"config {s} start= disabled", 30000);
                if (r.code != 0) Reg.Set("HKLM", $@"SYSTEM\CurrentControlSet\Services\{s}", "Start", 4, RegistryValueKind.DWord);
                Log.Info($"Service {s} → Disabled");
            }
        },
        Revert = () =>
        {
            foreach (var s in services.Where(ServiceExists))
            {
                var key = "svc|" + s;
                var b = Backup.Get(key);
                int start = b != null && int.TryParse(b.Value, out var v) ? v : 3;   // no backup → Windows default (manual)
                string mode = start switch { 2 => "auto", 4 => "disabled", _ => "demand" };
                var r = Util.Run("sc.exe", $"config {s} start= {mode}", 30000);
                if (r.code != 0) Reg.Set("HKLM", $@"SYSTEM\CurrentControlSet\Services\{s}", "Start", start, RegistryValueKind.DWord);
                if (start == 2) Util.Run("sc.exe", $"start {s}", 30000);
                Backup.Remove(key);
                Log.Info($"Service {s} → restored ({mode})");
            }
        },
    };

    static bool ServiceExists(string name)
    {
        try { using var k = Reg.Base("HKLM").OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}"); return k != null; } catch { return false; }
    }

    // ---------------- Nagle ----------------
    static IEnumerable<string> NagleInterfaces()
    {
        const string root = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        var list = new List<string>();
        try
        {
            using var k = Reg.Base("HKLM").OpenSubKey(root);
            if (k == null) return list;
            foreach (var n in k.GetSubKeyNames())
            {
                using var s = k.OpenSubKey(n);
                var dhcp = s?.GetValue("DhcpIPAddress") as string;
                var ip = s?.GetValue("IPAddress") as string[];
                bool hasIp = (!string.IsNullOrEmpty(dhcp) && dhcp != "0.0.0.0") || (ip != null && ip.Any(x => !string.IsNullOrEmpty(x) && x != "0.0.0.0"));
                if (hasIp) list.Add($@"{root}\{n}");
            }
        }
        catch { }
        return list;
    }

    // ---------------- Windowed games (DirectX user global settings) ----------------
    static void SetDxGlobal(string name, string value)
    {
        const string path = @"Software\Microsoft\DirectX\UserGpuPreferences";
        var cur = Reg.Get("HKCU", path, "DirectXUserGlobalSettings") as string ?? "";
        var parts = cur.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(p => !p.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)).ToList();
        parts.Add($"{name}={value}");
        var nv = string.Join(";", parts) + ";";
        Reg.Set("HKCU", path, "DirectXUserGlobalSettings", nv, RegistryValueKind.String);
        Log.Info("DirectXUserGlobalSettings = " + nv);
    }

    // ---------------- Mouse ----------------
    static void ApplyMouseNow()
    {
        try
        {
            int a = int.Parse(Reg.Get("HKCU", @"Control Panel\Mouse", "MouseThreshold1") as string ?? "6");
            int b = int.Parse(Reg.Get("HKCU", @"Control Panel\Mouse", "MouseThreshold2") as string ?? "10");
            int c = int.Parse(Reg.Get("HKCU", @"Control Panel\Mouse", "MouseSpeed") as string ?? "1");
            Native.SystemParametersInfo(0x0004 /*SPI_SETMOUSE*/, 0, new[] { a, b, c }, 0x3);
        }
        catch { }
    }

    // ---------------- Power plan ----------------
    const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    const string HighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    static readonly Regex GuidRx = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    public static string ActiveScheme()
    {
        var m = GuidRx.Match(Util.Run("powercfg.exe", "/getactivescheme", 15000).output);
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    static bool PowerPlanActive()
    {
        var a = ActiveScheme();
        if (a == null) return false;
        var mine = Backup.Get("power|created")?.Value;
        return a == Ultimate || a == mine;
    }

    static void ApplyPowerPlan()
    {
        var prev = ActiveScheme() ?? Balanced;
        if (!Backup.Has("power|previous")) Backup.Set("power|previous", new Backup.Entry { Existed = true, Kind = "String", Value = prev });
        // reuse an Ultimate scheme if one already exists
        var list = Util.Run("powercfg.exe", "/list", 15000).output;
        string guid = Backup.Get("power|created")?.Value;
        if (guid == null || !list.Contains(guid, StringComparison.OrdinalIgnoreCase))
        {
            var r = Util.Run("powercfg.exe", $"-duplicatescheme {Ultimate}", 15000);
            var m = GuidRx.Match(r.output);
            if (!m.Success)
            {
                r = Util.Run("powercfg.exe", $"-duplicatescheme {HighPerf}", 15000);
                m = GuidRx.Match(r.output);
            }
            if (!m.Success) { Log.Error("Power plan: " + r.output); throw new Exception("جهازك لا يدعم خطة الأداء العالي (غالباً لابتوب بنظام Modern Standby)."); }
            guid = m.Value.ToLowerInvariant();
            Util.Run("powercfg.exe", $"-changename {guid} \"MEDO Ultimate Performance\"", 15000);
            Backup.Set("power|created", new Backup.Entry { Existed = true, Kind = "String", Value = guid });
        }
        Util.Run("powercfg.exe", $"/setactive {guid}", 15000);
        Log.Ok("Power plan → MEDO Ultimate Performance");
    }

    static void RevertPowerPlan()
    {
        var prev = Backup.Get("power|previous")?.Value ?? Balanced;
        var created = Backup.Get("power|created")?.Value;
        if (prev == created) prev = Balanced;
        Util.Run("powercfg.exe", $"/setactive {prev}", 15000);
        if (created != null) Util.Run("powercfg.exe", $"/delete {created}", 15000);
        Backup.Remove("power|previous"); Backup.Remove("power|created");
        Log.Ok("Power plan restored → " + prev);
    }

    // ---------------- Restore point ----------------
    public static (bool ok, string msg) CreateRestorePoint()
    {
        try { Reg.Set("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", "SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord); } catch { }
        var drive = Util.SystemDrive;
        var r = Util.PowerShell($"$ProgressPreference='SilentlyContinue'; try {{ Enable-ComputerRestore -Drive '{drive}' -ErrorAction SilentlyContinue; Checkpoint-Computer -Description 'MEDO Boost' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; 'RESULT:OK' }} catch {{ 'RESULT:' + $_.Exception.Message }}", 300000);
        var line = r.output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith("RESULT:")) ?? "RESULT:" + r.output.Trim();
        var msg = line["RESULT:".Length..];
        bool ok = msg == "OK";
        if (ok) Log.Ok("تم إنشاء نقطة استعادة للنظام"); else Log.Error("Restore point: " + msg);
        return (ok, ok ? "" : msg);
    }
}
