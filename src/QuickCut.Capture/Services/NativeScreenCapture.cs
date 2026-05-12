using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickCut.Capture.Services;

public static class NativeScreenCapture
{
    private const int VirtualScreenX = 76;
    private const int VirtualScreenY = 77;
    private const int VirtualScreenWidth = 78;
    private const int VirtualScreenHeight = 79;

    public static Int32Rect GetVirtualScreenBounds()
    {
        var bounds = new Int32Rect(
            GetSystemMetrics(VirtualScreenX),
            GetSystemMetrics(VirtualScreenY),
            GetSystemMetrics(VirtualScreenWidth),
            GetSystemMetrics(VirtualScreenHeight));

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("Impossible de lire les dimensions de l'écran.");
        }

        return bounds;
    }

    public static ScreenCaptureResult CaptureVirtualScreen()
    {
        var bounds = GetVirtualScreenBounds();

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        var source = CreateBitmapSource(bitmap);
        source.Freeze();
        return new ScreenCaptureResult(source, bounds);
    }

    public static BitmapSource CaptureVirtualScreenRegion(Int32Rect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "La zone de capture doit avoir une largeur et une hauteur positives.");
        }

        var virtualBounds = GetVirtualScreenBounds();
        var clippedRegion = ClipRegion(region, virtualBounds.Width, virtualBounds.Height);
        if (clippedRegion.Width <= 0 || clippedRegion.Height <= 0)
        {
            throw new InvalidOperationException("La zone de capture est hors de l'écran virtuel.");
        }

        using var bitmap = new Bitmap(clippedRegion.Width, clippedRegion.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                virtualBounds.X + clippedRegion.X,
                virtualBounds.Y + clippedRegion.Y,
                0,
                0,
                bitmap.Size,
                CopyPixelOperation.SourceCopy);
        }

        var source = CreateBitmapSource(bitmap);
        source.Freeze();
        return source;
    }

    public static BitmapSource Crop(BitmapSource source, Int32Rect region)
    {
        var cropped = new CroppedBitmap(source, region);
        cropped.Freeze();
        return cropped;
    }

    private static Int32Rect ClipRegion(Int32Rect region, int maxWidth, int maxHeight)
    {
        var left = Math.Clamp(region.X, 0, maxWidth);
        var top = Math.Clamp(region.Y, 0, maxHeight);
        var right = Math.Clamp(region.X + region.Width, 0, maxWidth);
        var bottom = Math.Clamp(region.Y + region.Height, 0, maxHeight);
        return new Int32Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static BitmapSource CreateBitmapSource(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            return BitmapSource.Create(
                bitmap.Width,
                bitmap.Height,
                96,
                96,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                data.Scan0,
                Math.Abs(data.Stride) * data.Height,
                data.Stride);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
