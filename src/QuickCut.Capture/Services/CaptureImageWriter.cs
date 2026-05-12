using System.IO;
using System.Windows.Media.Imaging;

namespace QuickCut.Capture.Services;

public sealed record SavedCapture(string ImagePath);

public sealed class CaptureImageWriter
{
    public string RootPath { get; }

    public CaptureImageWriter(string? rootPath = null)
    {
        RootPath = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "QuickCut");
    }

    public async Task<SavedCapture> SaveCaptureAsync(
        BitmapSource image,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RootPath);

        var fileName = $"QuickCut_{DateTimeOffset.Now:yyyyMMdd_HHmmss_fff}.png";
        var imagePath = Path.Combine(RootPath, fileName);
        var temporaryPath = imagePath + ".tmp";

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        await using var stream = new MemoryStream();
        encoder.Save(stream);
        await File.WriteAllBytesAsync(temporaryPath, stream.ToArray(), cancellationToken);
        File.Move(temporaryPath, imagePath, overwrite: true);

        return new SavedCapture(imagePath);
    }
}
