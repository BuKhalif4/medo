namespace MedoBoost.Core;

/// <summary>Arabic → English for the play-sessions and prayer-times pages. {} are filled in order.</summary>
internal static class LangSessions
{
    public static readonly (string ar, string en)[] Pairs =
    {
        // navigation
        ("جلسات اللعب", "Play sessions"), ("مواقيت الصلاة", "Prayer times"),
        // sessions page
        ("البرنامج يلاحظ لما تلعب، يسجّل الـ FPS وحرارة وحمل الجهاز كل ثانية، وبعد ما تسكّر اللعبة يقول لك بالضبط ليش تقطّعت. كل شيء يبقى على جهازك.",
         "The app notices when you play, records FPS, temperatures and PC load every second, and after you close the game tells you exactly why it stuttered. Everything stays on your PC."),
        ("سجّل جلسات اللعب تلقائياً", "Record play sessions automatically"),
        ("الجلسات الأقصر من دقيقة ما تُحفظ. يُحتفظ بآخر 200 جلسة.", "Sessions shorter than a minute are not saved. The last 200 sessions are kept."),
        ("🔴 جاري تسجيل: ", "🔴 Recording: "),
        ("جاهز: ابدأ لعبة وأنا أسجّل (يحتاج 5 ثواني فريمات ثابتة).", "Ready: start a game and I will record it (needs 5 seconds of steady frames)."),
        ("التسجيل التلقائي مطفي.", "Automatic recording is off."),
        ("ما في جلسات بعد. العب لعبة أكثر من دقيقة وبتظهر هنا.", "No sessions yet. Play a game for more than a minute and it will show up here."),
        ("فيها مشاكل", "Has problems"), ("فيها ملاحظات", "Has notes"), ("سلسة", "Smooth"),
        ("عن آخر جلسة", "vs last session"), ("دقيقة", "min"), ("متوسط FPS", "Avg FPS"), ("توقفات", "Stutters"),
        ("الـ FPS خلال الجلسة", "FPS during the session"), ("حمل الجهاز: المعالج / الكرت / الرام (%)", "PC load: CPU / GPU / RAM (%)"),
        ("أطول إطار", "longest frame"), ("التشخيص", "Diagnosis"), ("نسخ التقرير", "Copy report"), ("تم نسخ التقرير ✔", "Report copied ✔"),
        ("تعذر النسخ", "Could not copy"), ("فتح مجلد الجلسات", "Open sessions folder"), ("حذف", "Delete"), ("حذف الجلسة", "Delete session"),
        ("تحذف جلسة {} بتاريخ {}؟", "Delete the {} session from {}?"), ("تعذر حذف الملف", "Could not delete the file"),
        // diagnosis sentences
        ("الجلسة سلسة: ما فيها توقفات ملحوظة والحركة متساوية.", "A smooth session: no noticeable stutters and the motion was even."),
        ("حصل {} توقف (إطار أطول من 50 ms)، وأطولها {} ms.", "{} stutters happened (frames longer than 50 ms); the longest was {} ms."),
        ("الـ 1% Low ({} FPS) أقل بكثير من المتوسط ({} FPS): الحركة غير متساوية رغم الـ FPS العالي.",
         "The 1% Low ({} FPS) is far below the average ({} FPS): motion was uneven despite the high FPS."),
        ("المعالج كان على {}% وقت التقطيع: المعالج هو الحد. أغلق البرامج الخلفية وخفّض إعدادات الفيزياء والزحام في اللعبة.",
         "The CPU was at {}% while it stuttered: the CPU is the limit. Close background apps and lower physics and crowd settings in the game."),
        ("كرت الشاشة كان على {}%: هو الحد الأعلى للأداء. خفّض الدقة أو الظلال، أو فعّل DLSS / FSR.",
         "The graphics card was at {}%: it is the performance limit. Lower the resolution or shadows, or turn on DLSS / FSR."),
        ("الرام وصل {}%: لما تمتلئ يبدأ Windows يستخدم القرص وتصير توقفات. أغلق المتصفح والبرامج الثقيلة.",
         "RAM reached {}%: when it fills up Windows starts using the disk and you get stutters. Close the browser and heavy apps."),
        ("الحرارة وصلت {}°C: احتمال أن الجهاز قلّل سرعته لحماية نفسه. افحص التهوية والمراوح.",
         "Temperature reached {}°C: the PC may have slowed itself down to protect itself. Check ventilation and fans."),
        ("الـ FPS نزل {}% بين أول الجلسة وآخرها. الأسباب المعتادة: حرارة، أو تسرّب رام في اللعبة.",
         "FPS dropped {}% between the start and the end of the session. Usual causes: heat, or a memory leak in the game."),
        ("حصل تقطيع والمعالج والكرت والرام مرتاحين: السبب غالباً تحميل ملفات أو شيدرز من القرص، أو برنامج خلفي. جرّب نقل اللعبة لـ SSD وفعّل «جهّز للعب».",
         "It stuttered while the CPU, GPU and RAM were relaxed: usually files or shaders loading from disk, or a background app. Try moving the game to an SSD and turn on “Game Ready”."),
        ("وضع «جهّز للعب» كان مطفياً في هذي الجلسة. فعّله في الجلسة القادمة وقارن النتيجة.",
         "“Game Ready” was off during this session. Turn it on next time and compare."),
        // notifications
        ("انتهت جلسة اللعب – {}", "Play session finished – {}"),
        ("متوسط {} FPS • 1% Low {} • {} ملاحظات في «جلسات اللعب»", "Avg {} FPS • 1% Low {} • {} notes in “Play sessions”"),
        ("متوسط {} FPS • 1% Low {} • سلسة", "Avg {} FPS • 1% Low {} • smooth"),
        // prayer page
        ("تُحسب على جهازك من موقع الشمس، بدون إنترنت. اختر مدينتك مرة وحدة، والتنبيه يطلع لك حتى وأنت داخل اللعبة.",
         "Calculated on your PC from the sun's position, no internet. Pick your city once and the alert shows even while you are in a game."),
        ("الصلاة القادمة", "Next prayer"), ("مواقيت اليوم", "Today's times"), ("المدينة", "City"), ("طريقة الحساب", "Calculation method"),
        ("لم تختر مدينة بعد: المواقيت الحالية لمكة المكرمة. اختر مدينتك.", "No city chosen yet: the times shown are for Mecca. Pick your city."),
        ("اختلاف دقيقة أو دقيقتين عن التقويم المحلي طبيعي. لو بلدك تعتمد طريقة ثانية جرّب غيرها.",
         "A minute or two of difference from your local calendar is normal. If your country uses another method, try it."),
        ("التنبيهات", "Alerts"), ("نبّهني بوقت الصلاة (حتى أثناء اللعب)", "Alert me at prayer time (even during games)"),
        ("شغّل صوت مع التنبيه", "Play a sound with the alert"), ("نبّهني أيضاً قبل الصلاة بـ", "Also alert me before the prayer by"),
        ("وقت الأذان فقط", "At prayer time only"), ("دقيقة قبلها", "min before"), ("جرّب التنبيه", "Test the alert"),
        ("حان وقت الصلاة 🕌", "It's prayer time 🕌"), ("هذا تنبيه تجريبي", "This is a test alert"),
        ("الفجر", "Fajr"), ("الشروق", "Sunrise"), ("الظهر", "Dhuhr"), ("العصر", "Asr"), ("المغرب", "Maghrib"), ("العشاء", "Isha"),
        ("حان وقت الصلاة", "It's prayer time"), ("الصلاة قريبة", "Prayer is near"),
        ("{} بعد {} دقيقة ({})", "{} in {} minutes ({})"), ("أذان {} ({})", "Adhan for {} ({})"),
    };
}
