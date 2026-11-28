namespace MedoBoost.Core;

/// <summary>Every action the app takes is written here (and to a file) so you always know what was changed or deleted.</summary>
internal static class Log
{
    public record Entry(DateTime Time, string Level, string Text);
    static readonly List<Entry> entries = new();
    static readonly object gate = new();
    public static event Action<Entry> Added;
    public static string FilePath => Path.Combine(Util.DataDir, "logs", $"log-{DateTime.Now:yyyy-MM-dd}.txt");

    public static IReadOnlyList<Entry> All { get { lock (gate) return entries.ToList(); } }

    static void Write(string level, string text)
    {
        var e = new Entry(DateTime.Now, level, text);
        lock (gate)
        {
            entries.Add(e);
            if (entries.Count > 3000) entries.RemoveRange(0, 500);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.AppendAllText(FilePath, $"[{e.Time:HH:mm:ss}] [{level}] {text}{Environment.NewLine}");
            }
            catch { }
        }
        try { Added?.Invoke(e); } catch { }
    }

    public static void Info(string t) => Write("INFO", t);
    public static void Ok(string t) => Write("OK", t);
    public static void Warn(string t) => Write("WARN", t);
    public static void Error(string t) => Write("ERR", t);
}
