using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using QuickCut.Contracts;
using QuickCut.Contracts.Jobs;
using QuickCut.Contracts.Validation;

namespace QuickCut.Capture.Services;

public sealed class CaptureJobWriter
{
    private static readonly byte[] TransparentPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    public string RootPath { get; }

    public CaptureJobWriter(string? rootPath = null)
    {
        RootPath = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickCut",
            "jobs");
    }

    public async Task<CaptureJobWriteResult> WriteStubJobAsync(CancellationToken cancellationToken = default)
    {
        return await WriteJobAsync(
            source: "capture-stub",
            bounds: new CaptureBounds { X = 0, Y = 0, Width = 1, Height = 1 },
            imageWriter: path => File.WriteAllBytesAsync(path, TransparentPng, cancellationToken),
            cancellationToken);
    }

    public async Task<CaptureJobWriteResult> WriteCaptureJobAsync(
        BitmapSource image,
        CaptureBounds bounds,
        CancellationToken cancellationToken = default)
    {
        return await WriteJobAsync(
            source: "screen-capture",
            bounds: bounds,
            imageWriter: path => WriteBitmapSourcePngAsync(image, path, cancellationToken),
            cancellationToken);
    }

    private async Task<CaptureJobWriteResult> WriteJobAsync(
        string source,
        CaptureBounds bounds,
        Func<string, Task> imageWriter,
        CancellationToken cancellationToken)
    {
        var jobId = $"qc-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..31];
        var artifactDir = Path.Combine(RootPath, jobId);
        Directory.CreateDirectory(artifactDir);

        var imagePath = Path.Combine(artifactDir, "capture.png");
        var temporaryImagePath = imagePath + ".tmp";
        await imageWriter(temporaryImagePath);
        File.Move(temporaryImagePath, imagePath, overwrite: true);

        var manifest = new CaptureJobManifest
        {
            SchemaVersion = CaptureJobManifest.CurrentSchemaVersion,
            JobId = jobId,
            CreatedAt = DateTimeOffset.UtcNow,
            Source = source,
            ImagePath = imagePath,
            Bounds = bounds,
            Monitor = new MonitorDescriptor { Id = "virtual-screen", DeviceName = "virtual-screen", IsPrimary = true },
            Dpi = new DpiDescriptor { ScaleX = 1.0, ScaleY = 1.0, DpiX = 96, DpiY = 96 },
            RoutingProfile = RoutingProfile.OfflineOnly,
            ArtifactDir = artifactDir,
        };

        var validation = CaptureJobManifestValidator.Validate(manifest);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join("; ", validation.Errors));
        }

        var manifestPath = Path.Combine(artifactDir, "job.json");
        var temporaryManifestPath = manifestPath + ".tmp";
        var payload = JsonSerializer.Serialize(manifest, QuickCutJson.DefaultOptions);

        await File.WriteAllTextAsync(temporaryManifestPath, payload, cancellationToken);
        File.Move(temporaryManifestPath, manifestPath, overwrite: true);

        return new CaptureJobWriteResult(jobId, manifestPath, imagePath, manifest);
    }

    private static async Task WriteBitmapSourcePngAsync(
        BitmapSource image,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        await using var stream = new MemoryStream();
        encoder.Save(stream);
        await File.WriteAllBytesAsync(outputPath, stream.ToArray(), cancellationToken);
    }
}
