using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UndefinedSS.ServicesPrechecker;

internal static class WindowIconLoaderTests
{
    private const string ResourceName =
        "UndefinedSS.ServicesPrechecker.Tests.Assets.app.ico";

    [STAThread]
    private static int Main()
    {
        ImageSource icon = WindowIconLoader.LoadLargestFrame(
            Assembly.GetExecutingAssembly(),
            ResourceName);

        if (icon == null)
        {
            Console.Error.WriteLine("Window icon was not loaded.");
            return 1;
        }

        BitmapSource bitmap = icon as BitmapSource;
        if (bitmap == null || bitmap.PixelWidth != 256 || bitmap.PixelHeight != 256)
        {
            Console.Error.WriteLine(
                "Expected the 256x256 frame, but selected {0}x{1}.",
                bitmap == null ? 0 : bitmap.PixelWidth,
                bitmap == null ? 0 : bitmap.PixelHeight);
            return 1;
        }

        if (!icon.IsFrozen)
        {
            Console.Error.WriteLine("Window icon must be frozen for cross-thread use.");
            return 1;
        }

        ImageSource missing = WindowIconLoader.LoadLargestFrame(
            Assembly.GetExecutingAssembly(),
            "UndefinedSS.ServicesPrechecker.Tests.Assets.missing.ico");
        if (missing != null)
        {
            Console.Error.WriteLine("A missing resource unexpectedly returned an icon.");
            return 1;
        }

        Console.WriteLine("All window icon loader tests passed.");
        return 0;
    }
}
