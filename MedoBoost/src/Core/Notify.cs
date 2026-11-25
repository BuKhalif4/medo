using System.Windows;

namespace MedoBoost.Core;

public class NotifyItem
{
    public DateTime Time = DateTime.Now;
    public string Kind = "info";   // ok | warn | err | info
    public string Title = "";
    public string Text = "";
    public bool Read;
}

/// <summary>
/// One place for every message the app shows: a small toast, an entry in the bell's history,
/// and (for important ones while the window is hidden) a Windows tray balloon.
/// </summary>
public static class Notify
{
    public static readonly List<NotifyItem> Items = new();
    public static event Action<NotifyItem> Added;
    public static event Action Changed;
    public static int Unread => Items.Count(i => !i.Read);

    public static void Push(string kind, string title, string text, bool record = true, bool balloon = false)
    {
        var d = Application.Current?.Dispatcher;
        if (d != null && !d.CheckAccess()) { d.BeginInvoke(() => Push(kind, title, text, record, balloon)); return; }
        var it = new NotifyItem { Kind = kind, Title = title, Text = text };
        if (record)
        {
            Items.Insert(0, it);
            if (Items.Count > 60) Items.RemoveRange(60, Items.Count - 60);
        }
        try { Added?.Invoke(it); Changed?.Invoke(); } catch { }
        if (balloon)
        {
            var w = UI.MainWindow.Instance;
            if (w == null || !w.IsVisible || !w.IsActive) Tray.Balloon(title, text);
        }
    }

    public static void MarkAllRead() { foreach (var i in Items) i.Read = true; Changed?.Invoke(); }
    public static void Clear() { Items.Clear(); Changed?.Invoke(); }
}
