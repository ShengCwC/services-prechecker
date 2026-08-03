using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using UndefinedSS.ServicesPrechecker;

internal static class ForensicScrollBarStyleTests
{
    [STAThread]
    private static int Main()
    {
        MethodInfo factory = typeof(MainWindow).GetMethod(
            "BuildForensicScrollBarStyle",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (factory == null)
        {
            return Fail("Scroll bar style factory was not found.");
        }

        Style style = factory.Invoke(null, null) as Style;
        if (style == null || style.TargetType != typeof(ScrollBar))
        {
            return Fail("Scroll bar style did not parse correctly.");
        }

        ScrollBar scrollBar = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Minimum = 0,
            Maximum = 100,
            ViewportSize = 25,
            Value = 20,
            Height = 300,
            Style = style
        };
        scrollBar.Measure(new Size(12, 300));
        scrollBar.Arrange(new Rect(0, 0, 12, 300));
        scrollBar.ApplyTemplate();
        scrollBar.UpdateLayout();

        if (Math.Abs(scrollBar.ActualWidth - 12) > 0.01)
        {
            return Fail("Scroll bar must keep the compact 12-pixel lane.");
        }

        Track track = scrollBar.Template.FindName(
            "PART_Track",
            scrollBar) as Track;
        Border rail = scrollBar.Template.FindName("Rail", scrollBar) as Border;
        if (track == null || rail == null || track.Thumb == null)
        {
            return Fail("The native Track, rail, or thumb is missing.");
        }

        if (track.DecreaseRepeatButton == null ||
            track.IncreaseRepeatButton == null ||
            track.DecreaseRepeatButton.Command != ScrollBar.PageUpCommand ||
            track.IncreaseRepeatButton.Command != ScrollBar.PageDownCommand)
        {
            return Fail("Track paging commands are not preserved.");
        }

        if (track.DecreaseRepeatButton.Content != null ||
            track.IncreaseRepeatButton.Content != null)
        {
            return Fail("Legacy arrow glyphs must not be rendered.");
        }

        track.Thumb.ApplyTemplate();
        Border thumbSurface = track.Thumb.Template.FindName(
            "ThumbSurface",
            track.Thumb) as Border;
        if (thumbSurface == null ||
            Math.Abs(thumbSurface.Width - 5) > 0.01 ||
            track.Thumb.MinHeight < 38)
        {
            return Fail("The resting thumb geometry is incorrect.");
        }

        SolidColorBrush railBrush = rail.Background as SolidColorBrush;
        if (railBrush == null || railBrush.Color != Color.FromRgb(11, 12, 12))
        {
            return Fail("The rail does not use the forensic dark-surface color.");
        }

        Console.WriteLine("All forensic scroll bar style tests passed.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
