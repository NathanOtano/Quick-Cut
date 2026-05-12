using System.Windows;

namespace QuickCut.Capture.Services;

public static class CaptureCoordinateMapper
{
    private const double EdgeSnapToleranceDip = 2;

    public static Int32Rect CreatePixelRegion(
        Point start,
        Point end,
        double pixelScaleX,
        double pixelScaleY,
        int maxPixelWidth,
        int maxPixelHeight)
    {
        if (pixelScaleX <= 0 || pixelScaleY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelScaleX), "Les facteurs de conversion DPI doivent etre positifs.");
        }

        var maxDipWidth = maxPixelWidth / pixelScaleX;
        var maxDipHeight = maxPixelHeight / pixelScaleY;
        var leftDip = SnapToScreenEdge(Math.Min(start.X, end.X), maxDipWidth);
        var topDip = SnapToScreenEdge(Math.Min(start.Y, end.Y), maxDipHeight);
        var rightDip = SnapToScreenEdge(Math.Max(start.X, end.X), maxDipWidth);
        var bottomDip = SnapToScreenEdge(Math.Max(start.Y, end.Y), maxDipHeight);

        var left = ToClampedPixelFloor(leftDip, pixelScaleX, maxPixelWidth);
        var top = ToClampedPixelFloor(topDip, pixelScaleY, maxPixelHeight);
        var right = ToClampedPixelCeiling(rightDip, pixelScaleX, maxPixelWidth);
        var bottom = ToClampedPixelCeiling(bottomDip, pixelScaleY, maxPixelHeight);

        return new Int32Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static double SnapToScreenEdge(double value, double maxDip)
    {
        if (value <= EdgeSnapToleranceDip)
        {
            return 0;
        }

        if (maxDip - value <= EdgeSnapToleranceDip)
        {
            return maxDip;
        }

        return value;
    }

    private static int ToClampedPixelFloor(double value, double scale, int max)
    {
        var pixel = (int)Math.Floor(value * scale);
        return Math.Clamp(pixel, 0, max);
    }

    private static int ToClampedPixelCeiling(double value, double scale, int max)
    {
        var pixel = (int)Math.Ceiling(value * scale);
        return Math.Clamp(pixel, 0, max);
    }
}
