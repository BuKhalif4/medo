namespace MedoBoost.Core;

/// <summary>Arabic → English for the games page and the overlay styles.</summary>
internal static class LangGames
{
    public static readonly (string ar, string en)[] Pairs =
    {
        ("ألعابي", "My games"),
        ("يلقى الألعاب المثبتة من Steam و Epic و GOG، ويشغّلها لك بعد ما يجهّز الجهاز، ويقول لك هل جهازك يقدر يشغّلها قبل ما تنزّلها أو تلعبها.",
         "Finds the games installed from Steam, Epic and GOG, launches them after preparing your PC, and tells you whether your PC can run them."),
        ("فحص الألعاب من جديد", "Rescan games"), ("جاري البحث...", "Searching..."), ("لقيت", "Found"), ("لعبة", "games"),
        ("ما لقيت ألعاب. الألعاب اللي من Steam و Epic و GOG بس هي اللي تظهر هنا.", "No games found. Only games from Steam, Epic and GOG show up here."),
        ("آخر جلسة", "Last session"), ("هل يشغّلها جهازي؟", "Can my PC run it?"),
        ("يتصل بمتجر Steam ويجيب المتطلبات المنشورة للعبة. ما يرسل أي شي عنك.", "Connects to the Steam store and fetches the game's published requirements. Nothing about you is sent."),
        ("جهّز وشغّل", "Prepare & play"), ("تشغيل", "Play"), ("المجلد", "Folder"), ("تعذر تشغيل اللعبة", "Could not start the game"),
        ("جهّزت الجهاز وشغّلت {} 🚀", "Prepared the PC and started {} 🚀"), ("صار خطأ: {}", "Error: {}"),
        ("جاري جلب المتطلبات من Steam...", "Fetching the requirements from Steam..."),
        ("تقدير: مقارنة الكرت على سلّم تقريبي، والمعالج ما يتقارن تلقائياً.", "Estimate: the graphics card is compared on a rough scale, the CPU is not compared automatically."),
        // requirement lines
        ("ما لقيت اللعبة في متجر Steam، فما عندي متطلباتها.", "I could not find the game on the Steam store, so I have no requirements for it."),
        ("Steam ما ينشر متطلبات هذي اللعبة.", "Steam does not publish requirements for this game."),
        ("تعذر الاتصال بـ Steam: {}", "Could not reach Steam: {}"), ("انتهت مهلة الاتصال بـ Steam.", "The connection to Steam timed out."),
        ("رد Steam غير مفهوم: {}", "Steam's reply was not understood: {}"),
        ("جهازك يشغّلها بإذن الله، حتى على الإعدادات الموصى بها.", "Your PC runs it, even on the recommended settings."),
        ("جهازك يشغّلها على الحد الأدنى. خفّض الإعدادات للأداء الأفضل.", "Your PC runs it at the minimum level. Lower the settings for better performance."),
        ("جهازك أقل من الحد الأدنى. غالباً بتتقطع أو ما تشتغل.", "Your PC is below the minimum. It will likely stutter or not run."),
        ("ما قدرت أحكم: Steam ما يذكر رام أو كرت شاشة واضحين لهذي اللعبة.", "I could not decide: Steam does not list a clear RAM or graphics card for this game."),
        ("الرام: عندك {} GB والحد الأدنى {} GB.", "RAM: you have {} GB and the minimum is {} GB."),
        ("الرام: عندك {} GB، يكفي الأدنى ({} GB) بس الموصى به {} GB.", "RAM: you have {} GB, enough for the minimum ({} GB) but {} GB is recommended."),
        ("الرام: عندك {} GB ويكفي ({} GB مطلوب).", "RAM: you have {} GB and that is enough ({} GB needed)."),
        ("كرت الشاشة ({}) أضعف من الحد الأدنى ({}).", "The graphics card ({}) is weaker than the minimum ({})."),
        ("كرت الشاشة يغطي الحد الأدنى ({}) بس أضعف من الموصى به ({}).", "The graphics card covers the minimum ({}) but is weaker than recommended ({})."),
        ("كرت الشاشة ({}) كافي.", "The graphics card ({}) is enough."),
        ("ما قدرت أقارن كرت الشاشة تلقائياً. المطلوب: {}", "I could not compare the graphics card automatically. Required: {}"),
        ("المعالج المطلوب: {} (عندك: {}). المعالجات ما أقارنها تلقائياً، قارنها بنفسك.", "Required CPU: {} (yours: {}). I do not compare CPUs automatically, compare them yourself."),
        // overlay styles
        ("نمط الأوفرلاي", "Overlay style"), ("كامل", "Full"), ("بسيط (FPS فقط)", "Minimal (FPS only)"), ("احترافي", "Pro"), ("نيون", "Neon"), ("مخصص", "Custom"),
        ("«مخصص» و«نيون»: اختر اللون وبعدين العناصر من الخيارات تحت.", "“Custom” and “Neon”: pick the colour, then the items from the options below."),
        ("لون الأوفرلاي", "Overlay colour"), ("سماوي", "Cyan"), ("بنفسجي", "Violet"), ("أخضر", "Green"), ("برتقالي", "Orange"), ("وردي", "Pink"), ("أحمر", "Red"),
        ("خلفية داكنة خلف الأوفرلاي (للنمط المخصص)", "Dark background behind the overlay (custom style)"),
    };
}
