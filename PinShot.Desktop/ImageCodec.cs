using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace PinShot.Desktop;

internal static class ImageCodec
{
    public static byte[] EncodePng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    public static byte[] CopyRgbaPixels(Bitmap bitmap)
    {
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;
        int stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];

        using var target = new WriteableBitmap(
            bitmap.PixelSize,
            bitmap.Dpi,
            PixelFormats.Rgba8888,
            AlphaFormat.Unpremul);
        using ILockedFramebuffer framebuffer = target.Lock();
        bitmap.CopyPixels(framebuffer);

        for (int y = 0; y < height; y++)
            Marshal.Copy(IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes), pixels, y * stride, stride);

        return pixels;
    }

    public static Bitmap Decode(byte[] data, int? decodeWidth = null)
    {
        using var stream = new MemoryStream(data, writable: false);
        return decodeWidth is int width
            ? Bitmap.DecodeToWidth(stream, width)
            : new Bitmap(stream);
    }
}
