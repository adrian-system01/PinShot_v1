using System.IO;
using System.Windows.Media.Imaging;

namespace PinShot
{
    internal static class WpfImageCodec
    {
        public static byte[] ToPng(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        public static BitmapImage? Load(string path, int decodeWidth = 0)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo cargar imagen " + path + ": " + ex.Message);
                return null;
            }
        }

        public static BitmapImage? Decode(byte[] pngData)
        {
            try
            {
                using var stream = new MemoryStream(pngData, writable: false);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo decodificar una imagen: " + ex.Message);
                return null;
            }
        }
    }
}
