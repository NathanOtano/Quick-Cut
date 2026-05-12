using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickCut.Capture.Services;
using QuickCut.Contracts;
using QuickCut.Contracts.Jobs;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class CaptureJobWriterTests
{
    [Fact]
    public async Task WriteStubJobAsyncWritesManifestAtomicallyWithSnakeCaseJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "QuickCut.Tests", Guid.NewGuid().ToString("N"));
        var writer = new CaptureJobWriter(root);

        try
        {
            var result = await writer.WriteStubJobAsync();

            var json = await File.ReadAllTextAsync(result.ManifestPath);
            var manifest = JsonSerializer.Deserialize<CaptureJobManifest>(json, QuickCutJson.DefaultOptions);

            Assert.NotNull(manifest);
            Assert.True(File.Exists(result.ManifestPath));
            Assert.True(File.Exists(result.ImagePath));
            Assert.False(File.Exists(result.ManifestPath + ".tmp"));
            Assert.Contains("\"image_path\"", json, StringComparison.Ordinal);
            Assert.Contains("\"routing_profile\": \"offline-only\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ImagePath", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteCaptureJobAsyncWritesSelectedImageBounds()
    {
        var root = Path.Combine(Path.GetTempPath(), "QuickCut.Tests", Guid.NewGuid().ToString("N"));
        var writer = new CaptureJobWriter(root);

        try
        {
            var image = BitmapSource.Create(
                2,
                2,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                new byte[]
                {
                    0, 0, 255, 255,
                    0, 255, 0, 255,
                    255, 0, 0, 255,
                    255, 255, 255, 255,
                },
                8);
            var bounds = new CaptureBounds { X = 10, Y = 20, Width = 2, Height = 2 };

            var result = await writer.WriteCaptureJobAsync(image, bounds);

            var manifest = JsonSerializer.Deserialize<CaptureJobManifest>(
                await File.ReadAllTextAsync(result.ManifestPath),
                QuickCutJson.DefaultOptions);

            Assert.NotNull(manifest);
            Assert.Equal("screen-capture", manifest.Source);
            Assert.Equal(10, manifest.Bounds.X);
            Assert.Equal(20, manifest.Bounds.Y);
            Assert.Equal(2, manifest.Bounds.Width);
            Assert.Equal(2, manifest.Bounds.Height);
            Assert.True(File.Exists(result.ImagePath));
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
