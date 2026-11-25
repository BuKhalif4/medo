using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using MedoBoost.Core;

namespace MedoBoost.UI;

/// <summary>Small styled menu that pops up above the tray icon (the normal Win32 menu can't be themed).</summary>
public class TrayMenu : Window
{
    static TrayMenu current;

    public static void ShowAtCursor()
    {
        try { current?.Close(); } catch { }
        current = new TrayMenu();
        current.Show();
        Tray.SetForegroundWindow(new WindowInteropHelper(current).Handle);
        current.Activate();
    }

    TrayMenu()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight;
        FlowDirection = FlowDirection.RightToLeft; FontFamily = new FontFamily("Segoe UI");
        Foreground = Ui.B("Text");

        var stack = new StackPanel { Width = 250 };
        stack.Children.Add(Row("", "فتح KHA Boost", () => Tray.ShowMain()));
        stack.Children.Add(Row(GameMode.Active ? "" : "", GameMode.Active ? "إنهاء وضع اللعب" : "جهّز للعب", () => _ = GameMode.ToggleAsync()));
        stack.Children.Add(Row("", (MainWindow.Instance.Overlay?.IsVisible ?? false) ? "إخفاء الأوفرلاي" : "إظهار الأوفرلاي", () => MainWindow.Instance.SetOverlay(!(MainWindow.Instance.Overlay?.IsVisible ?? false))));
        stack.Children.Add(Row("", "تحرير الرام", async () => { var f = await Task.Run(() => Memory.CleanAll()); Notify.Push("ok", "تحرير الرام", $"تم تحرير {Util.Size(f)}"); }));
        stack.Children.Add(Row("", "تنظيف سريع", async () =>
        {
            long freed = await Task.Run(() =>
            {
                long f = 0; using var w = ProcessManager.DeletedListWriter();
                foreach (var i in Cleaner.Items.Where(i => i.DefaultOn && i.Risk == Risk.Safe && i.Id is "user_temp" or "win_temp" or "d3d_cache")) { Cleaner.Scan(i); Cleaner.Clean(i, w); f += i.Freed; }
                return f;
            });
            Notify.Push("ok", "تنظيف سريع", $"تم حذف {Util.Size(freed)}");
        }));
        stack.Children.Add(new Border { Height = 1, Background = Ui.B("BorderB"), Margin = new Thickness(6, 4, 6, 4) });
        stack.Children.Add(Row("", "خروج", () => Tray.Exit(), true));

        Content = new Border
        {
            Child = stack, Background = Ui.B("Surface2"), BorderBrush = Ui.B("BorderB"), BorderThickness = new Thickness(1),
            CornerRadius = Ui.Cr(14), Padding = new Thickness(6), Margin = new Thickness(10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 0, Opacity = 0.6 },
        };
        Loaded += (_, _) =>
        {
            var (cx, cy) = Tray.CursorDip();
            var wa = SystemParameters.WorkArea;
            Left = Math.Clamp(cx - ActualWidth / 2, wa.Left, wa.Right - ActualWidth);
            Top = Math.Max(wa.Top, Math.Min(cy - ActualHeight - 6, wa.Bottom - ActualHeight));
        };
        Deactivated += (_, _) => { try { Close(); } catch { } };
    }

    UIElement Row(string glyph, string text, Action act, bool danger = false)
    {
        var tb = new TextBlock { Text = text, FontSize = 13.5, Foreground = Ui.B(danger ? "Bad" : "Text"), VerticalAlignment = VerticalAlignment.Center };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var ic = Ui.Icon(glyph, 15, danger ? "Bad" : "Accent2");
        Grid.SetColumn(ic, 0); Grid.SetColumn(tb, 1);
        g.Children.Add(ic); g.Children.Add(tb);
        var b = new Border { Child = g, Padding = new Thickness(10, 9, 10, 9), CornerRadius = Ui.Cr(8), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
        b.MouseEnter += (_, _) => b.Background = Ui.Solid("#33FFFFFF");
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += (_, _) => { try { Close(); } catch { } act(); };
        return b;
    }
}
