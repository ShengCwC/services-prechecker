using System;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UndefinedSS.ServicesPrechecker
{
    internal static class WindowIconLoader
    {
        public static ImageSource LoadLargestFrame(Assembly assembly, string resourceName)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }

            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new ArgumentException("A resource name is required.", "resourceName");
            }

            using (System.IO.Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                IconBitmapDecoder decoder = new IconBitmapDecoder(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                BitmapFrame frame = decoder.Frames
                    .OrderByDescending(candidate =>
                        (long)candidate.PixelWidth * candidate.PixelHeight)
                    .ThenByDescending(candidate => candidate.Format.BitsPerPixel)
                    .FirstOrDefault();

                if (frame == null)
                {
                    return null;
                }

                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }

                return frame;
            }
        }
    }
}
