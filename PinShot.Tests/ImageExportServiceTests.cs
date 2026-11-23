using PinShot;
using TiffLibrary;
using TiffLibrary.PixelFormats;
using TinyEXR;

namespace PinShot.Tests;

[TestClass]
public sealed class ImageExportServiceTests
{
    [TestMethod]
    public async Task TiffExportPreservesRgbaPixelsAndDimensions()
    {
        byte[] source =
        {
            255, 0, 0, 255,
            0, 128, 255, 64
        };

        byte[] encoded = await ImageExportService.EncodeTiffAsync(source, 2, 1);

        using var encodedStream = new MemoryStream(encoded, writable: false);
        using TiffFileReader reader = await TiffFileReader.OpenAsync(encodedStream, leaveOpen: false);
        TiffImageFileDirectory ifd = await reader.ReadImageFileDirectoryAsync();
        TiffImageDecoder decoder = await reader.CreateImageDecoderAsync(ifd);
        Assert.AreEqual(2, decoder.Width);
        Assert.AreEqual(1, decoder.Height);
        var decoded = new TiffRgba32[2];
        var pixelBuffer = new TiffMemoryPixelBuffer<TiffRgba32>(decoded, 2, 1, writable: true);
        await decoder.DecodeAsync(pixelBuffer);

        Assert.AreEqual(new TiffRgba32(255, 0, 0, 255), decoded[0]);
        Assert.AreEqual(new TiffRgba32(0, 128, 255, 64), decoded[1]);
    }

    [TestMethod]
    public void ExrExportProducesLinearHalfFloatPixels()
    {
        byte[] source = { 255, 128, 0, 64 };

        byte[] encoded = ImageExportService.EncodeExr(source, 1, 1);

        Assert.AreEqual(ExrResult.Success,
            Exr.LoadEXRFromMemory(encoded, out float[] rgba, out int width, out int height));
        Assert.AreEqual(1, width);
        Assert.AreEqual(1, height);
        Assert.AreEqual(1f, rgba[0], 0.001f);
        Assert.AreEqual(0.216f, rgba[1], 0.001f);
        Assert.AreEqual(0f, rgba[2], 0.001f);
        Assert.AreEqual(64f / 255f, rgba[3], 0.001f);
    }

    [TestMethod]
    public void ExportRejectsPixelBuffersWithIncorrectLength()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            ImageExportService.EncodeExr(new byte[3], 1, 1));
    }
}
