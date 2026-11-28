using System.Globalization;
using System.Windows.Threading;

namespace MedoBoost.Core;

public record PrayerCity(string Key, string Ar, string En, double Lat, double Lon);

public record PrayerTime(string Key, string Name, DateTime Time);

/// <summary>
/// Prayer times calculated on the PC from the sun's position (the standard astronomical method used by PrayTimes.org).
/// No internet and no location permission: you pick your city once. Alerts show even while a game is running.
/// </summary>
public static class Prayer
{
    public static readonly PrayerCity[] Cities =
    {
        new("mecca", "مكة المكرمة", "Mecca", 21.4225, 39.8262), new("medina", "المدينة المنورة", "Medina", 24.4686, 39.6142),
        new("riyadh", "الرياض", "Riyadh", 24.7136, 46.6753), new("jeddah", "جدة", "Jeddah", 21.4858, 39.1925),
        new("dammam", "الدمام", "Dammam", 26.4207, 50.0888), new("dubai", "دبي", "Dubai", 25.2048, 55.2708),
        new("abudhabi", "أبوظبي", "Abu Dhabi", 24.4539, 54.3773), new("doha", "الدوحة", "Doha", 25.2854, 51.5310),
        new("kuwait", "الكويت", "Kuwait City", 29.3759, 47.9774), new("manama", "المنامة", "Manama", 26.2285, 50.5860),
        new("muscat", "مسقط", "Muscat", 23.5880, 58.3829), new("sanaa", "صنعاء", "Sanaa", 15.3694, 44.1910),
        new("baghdad", "بغداد", "Baghdad", 33.3152, 44.3661), new("amman", "عمّان", "Amman", 31.9454, 35.9284),
        new("damascus", "دمشق", "Damascus", 33.5138, 36.2765), new("beirut", "بيروت", "Beirut", 33.8938, 35.5018),
        new("jerusalem", "القدس", "Jerusalem", 31.7683, 35.2137), new("cairo", "القاهرة", "Cairo", 30.0444, 31.2357),
        new("alex", "الإسكندرية", "Alexandria", 31.2001, 29.9187), new("khartoum", "الخرطوم", "Khartoum", 15.5007, 32.5599),
        new("tripoli", "طرابلس", "Tripoli", 32.8872, 13.1913), new("benghazi", "بنغازي", "Benghazi", 32.1167, 20.0667),
        new("tunis", "تونس", "Tunis", 36.8065, 10.1815), new("algiers", "الجزائر", "Algiers", 36.7538, 3.0588),
        new("rabat", "الرباط", "Rabat", 34.0209, -6.8416), new("casablanca", "الدار البيضاء", "Casablanca", 33.5731, -7.5898),
        new("nouakchott", "نواكشوط", "Nouakchott", 18.0735, -15.9582), new("istanbul", "إسطنبول", "Istanbul", 41.0082, 28.9784),
    };

    public static readonly (string Key, string Ar, string En, double Fajr, double Isha)[] Methods =
    {
        ("MWL", "رابطة العالم الإسلامي", "Muslim World League", 18, 17),
        ("Makkah", "أم القرى", "Umm al-Qura", 18.5, -90),     // negative Isha = minutes after Maghrib
        ("Egypt", "الهيئة المصرية", "Egyptian Authority", 19.5, 17.5),
        ("Karachi", "جامعة كراتشي", "Karachi", 18, 18),
        ("ISNA", "أمريكا الشمالية", "ISNA", 15, 15),
    };

    public static string Name(string key) => key switch
    {
        "fajr" => "الفجر", "sunrise" => "الشروق", "dhuhr" => "الظهر", "asr" => "العصر", "maghrib" => "المغرب", "isha" => "العشاء", _ => key,
    };

    // ---------------- calculation ----------------
    static double Rad(double d) => d * Math.PI / 180;
    static double Deg(double r) => r * 180 / Math.PI;
    static double Fix(double a, double b) { a -= b * Math.Floor(a / b); return a < 0 ? a + b : a; }
    static double Sin(double d) => Math.Sin(Rad(d));
    static double Cos(double d) => Math.Cos(Rad(d));

    static double Julian(int y, int m, int d)
    {
        if (m <= 2) { y--; m += 12; }
        double a = Math.Floor(y / 100.0), b = 2 - a + Math.Floor(a / 4);
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
    }

    // declination and equation of time for a Julian date
    static (double decl, double eqt) Sun(double jd)
    {
        double d = jd - 2451545.0;
        double g = Fix(357.529 + 0.98560028 * d, 360);
        double q = Fix(280.459 + 0.98564736 * d, 360);
        double l = Fix(q + 1.915 * Sin(g) + 0.020 * Sin(2 * g), 360);
        double e = 23.439 - 0.00000036 * d;
        double ra = Deg(Math.Atan2(Cos(e) * Sin(l), Cos(l))) / 15;
        return (Deg(Math.Asin(Sin(e) * Sin(l))), q / 15 - Fix(ra, 24));
    }

    /// <summary>All six times for one calendar day, as local DateTimes.</summary>
    public static List<PrayerTime> Day(DateTime date, double lat, double lon, string method)
    {
        var m = Methods.FirstOrDefault(x => x.Key == method);
        if (m.Key == null) m = Methods[0];
        double tz = TimeZoneInfo.Local.GetUtcOffset(date.Date.AddHours(12)).TotalHours;
        double jd = Julian(date.Year, date.Month, date.Day) - lon / 360.0;

        double MidDay(double t) => Fix(12 - Sun(jd + t).eqt, 24);
        double AngleTime(double angle, double t, bool ccw)
        {
            var (decl, _) = Sun(jd + t);
            double noon = MidDay(t);
            double x = (-Sin(angle) - Sin(decl) * Sin(lat)) / (Cos(decl) * Cos(lat));
            x = Math.Max(-1, Math.Min(1, x));
            double h = Deg(Math.Acos(x)) / 15;
            return noon + (ccw ? -h : h);
        }
        double AsrTime(double t)
        {
            var (decl, _) = Sun(jd + t);
            double angle = -Deg(Math.Atan(1 / (1 + Math.Tan(Rad(Math.Abs(lat - decl))))));
            return AngleTime(angle, t, false);
        }

        double fajr = AngleTime(m.Fajr, 5.0 / 24, true);
        double sunrise = AngleTime(0.833, 6.0 / 24, true);
        double dhuhr = MidDay(12.0 / 24);
        double asr = AsrTime(13.0 / 24);
        double maghrib = AngleTime(0.833, 18.0 / 24, false);
        double isha = m.Isha < 0 ? maghrib + (-m.Isha) / 60.0 : AngleTime(m.Isha, 18.0 / 24, false);

        double adj = tz - lon / 15;
        DateTime At(double h) => date.Date.AddMinutes(Math.Round((Fix(h + adj, 24)) * 60));
        return new List<PrayerTime>
        {
            new("fajr", Name("fajr"), At(fajr)), new("sunrise", Name("sunrise"), At(sunrise)), new("dhuhr", Name("dhuhr"), At(dhuhr)),
            new("asr", Name("asr"), At(asr)), new("maghrib", Name("maghrib"), At(maghrib)), new("isha", Name("isha"), At(isha)),
        };
    }

    public static List<PrayerTime> Today() => Day(DateTime.Today, Settings.Current.PrayerLat, Settings.Current.PrayerLon, Settings.Current.PrayerMethod);

    /// <summary>The next prayer from now (tomorrow's Fajr after Isha). Sunrise is not a prayer, so it is skipped.</summary>
    public static PrayerTime Next(DateTime now)
    {
        var s = Settings.Current;
        foreach (var d in new[] { now.Date, now.Date.AddDays(1) })
            foreach (var p in Day(d, s.PrayerLat, s.PrayerLon, s.PrayerMethod))
                if (p.Key != "sunrise" && p.Time > now) return p;
        return null;
    }

    public static string HijriDate(DateTime d)
    {
        try
        {
            var c = new UmAlQuraCalendar();
            string[] months = { "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة", "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة" };
            string[] monthsEn = { "Muharram", "Safar", "Rabi' I", "Rabi' II", "Jumada I", "Jumada II", "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qi'dah", "Dhu al-Hijjah" };
            int day = c.GetDayOfMonth(d), mo = c.GetMonth(d), yr = c.GetYear(d);
            return L.En ? $"{day} {monthsEn[mo - 1]} {yr} AH" : $"{day} {months[mo - 1]} {yr} هـ";
        }
        catch (Exception ex) { Log.Warn("Hijri date: " + ex.Message); return ""; }   // date outside the calendar's range
    }

    // ---------------- alerts ----------------
    static DispatcherTimer timer;
    static string lastFired = "";

    public static void StartAlerts()
    {
        if (timer != null) return;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    static void Tick()
    {
        try
        {
            var s = Settings.Current;
            if (!s.PrayerAlerts) return;
            var now = DateTime.Now;
            foreach (var p in Day(now.Date, s.PrayerLat, s.PrayerLon, s.PrayerMethod))
            {
                if (p.Key == "sunrise") continue;
                var before = p.Time.AddMinutes(-Math.Max(0, s.PrayerMinutesBefore));
                // two alerts per prayer: "N minutes before" and "now"; each fires once, only inside a 90 s window
                if (s.PrayerMinutesBefore > 0 && Within(now, before)) Fire($"{p.Key}-pre-{now:yyyyMMdd}", "الصلاة قريبة", $"{p.Name} بعد {s.PrayerMinutesBefore} دقيقة ({p.Time:HH:mm})");
                if (Within(now, p.Time)) Fire($"{p.Key}-now-{now:yyyyMMdd}", "حان وقت الصلاة", $"أذان {p.Name} ({p.Time:HH:mm})");
            }
        }
        catch (Exception ex) { Log.Warn("Prayer alert: " + ex.Message); }
    }

    static bool Within(DateTime now, DateTime t) => now >= t && now < t.AddSeconds(90);

    static void Fire(string id, string title, string text)
    {
        if (lastFired == id) return;
        lastFired = id;
        Notify.Push("info", title + " 🕌", text, balloon: true);
        if (Settings.Current.PrayerSound)
            try { System.Media.SystemSounds.Exclamation.Play(); } catch (Exception ex) { Log.Warn("Prayer sound: " + ex.Message); }
    }
}
