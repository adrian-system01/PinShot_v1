using TiffLibrary;
using TiffLibrary.PixelFormats;
using TinyEXR;

namespace PinShot;

public static class ImageExportService
{
    public static async Task<byte[]> EncodeTiffAsync(
        ReadOnlyMemory<byte> rgbaPixels,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        int pixelCount = ValidatePixels(rgbaPixels.Length, width, height);
        var pixels = new TiffRgba32[pixelCount];
        ReadOnlySpan<byte> source = rgbaPixels.Span;

        for (int i = 0; i < pixels.Length; i++)
        {
            int offset = i * 4;
            pixels[i] = new TiffRgba32(
                source[offset],
                source[offset + 1],
                source[offset + 2],
                source[offset + 3]);
        }

        var encoderBuilder = new TiffImageEncoderBuilder
        {
            PhotometricInterpretation = TiffPhotometricInterpretation.RGB,
            EnableTransparencyForRgb = true,
            Compression = TiffCompression.Deflate,
            Predictor = TiffPredictor.HorizontalDifferencing
        };
        TiffImageEncoder<TiffRgba32> encoder = encoderBuilder.Build<TiffRgba32>();
        var pixelBuffer = new TiffMemoryPixelBuffer<TiffRgba32>(pixels, width, height, writable: false);
        using var output = new MemoryStream();
        using TiffFileWriter writer = await TiffFileWriter.OpenAsync(
            output,
            leaveOpen: true,
            useBigTiff: false,
            cancellationToken);

        TiffStreamOffset ifdOffset;
        using (TiffImageFileDirectoryWriter ifdWriter = writer.CreateImageFileDirectory())
        {
            await encoder.EncodeAsync(ifdWriter, pixelBuffer, cancellationToken);
            ifdOffset = await ifdWriter.FlushAsync(cancellationToken);
        }

        writer.SetFirstImageFileDirectoryOffset(ifdOffset);
        await writer.FlushAsync(cancellationToken);
        return output.ToArray();
    }

    public static byte[] EncodeExr(ReadOnlyMemory<byte> rgbaPixels, int width, int height)
    {
        int pixelCount = ValidatePixels(rgbaPixels.Length, width, height);
        var linearRgba = new float[checked(pixelCount * 4)];
        ReadOnlySpan<byte> source = rgbaPixels.Span;

        for (int i = 0; i < pixelCount; i++)
        {
            int sourceOffset = i * 4;
            int destinationOffset = sourceOffset;
            linearRgba[destinationOffset] = SrgbToLinear(source[sourceOffset] / 255f);
            linearRgba[destinationOffset + 1] = SrgbToLinear(source[sourceOffset + 1] / 255f);
            linearRgba[destinationOffset + 2] = SrgbToLinear(source[sourceOffset + 2] / 255f);
            linearRgba[destinationOffset + 3] = source[sourceOffset + 3] / 255f;
        }

        ExrResult result = Exr.SaveEXRToMemory(
            linearRgba,
            width,
            height,
            components: 4,
            asFp16: true,
            out byte[] encoded);
        if (result != ExrResult.Success)
            throw new InvalidOperationException($"No se pudo codificar la imagen OpenEXR: {result}.");

        return encoded;
    }

    private static int ValidatePixels(int dataLength, int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "El ancho debe ser mayor que cero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "El alto debe ser mayor que cero.");

        int pixelCount = checked(width * height);
        int expectedLength = checked(pixelCount * 4);
        if (dataLength != expectedLength)
            throw new ArgumentException("Los datos deben contener cuatro bytes RGBA por píxel.");

        return pixelCount;
    }

    private static float SrgbToLinear(float value) =>
        value <= 0.04045f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
}
