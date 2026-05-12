using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class CaptureImageWriterTests
{
    [Fact]
    public async Task SaveCaptureAsyncWritesPngAtomicallyInCaptureFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "QuickCut.Tests", Guid.NewGuid().ToString("N"));
        var writer = new CaptureImageWriter(root);

        try
        {
            var image = BitmapSource.Create(
                1,
                1,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                new byte[] { 0, 0, 255, 255 },
                4);

            var result = await writer.SaveCaptureAsync(image);

            Assert.True(File.Exists(result.ImagePath));
            Assert.Equal(root, Path.GetDirectoryName(result.ImagePath));
            Assert.EndsWith(".png", result.ImagePath, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(result.ImagePath + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
