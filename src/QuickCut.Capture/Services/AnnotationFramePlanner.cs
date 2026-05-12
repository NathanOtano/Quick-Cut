using System.Windows;

namespace QuickCut.Capture.Services;

internal readonly record struct AnnotationFrameExpansion(
    double Width,
    double Height,
    double ShiftX,
    double ShiftY,
    bool Changed);

internal static class AnnotationFramePlanner
{
    public const double ExpansionPadding = 24;

    public static AnnotationFrameExpansion PlanExpansion(
        double currentWidth,
        double currentHeight,
        Rect contentBounds,
        bool allowOriginShift)
    {
        var safeWidth = Math.Max(1, currentWidth);
        var safeHeight = Math.Max(1, currentHeight);
        if (contentBounds.IsEmpty)
        {
            return new AnnotationFrameExpansion(safeWidth, safeHeight, 0, 0, false);
        }

        var shiftX = allowOriginShift && contentBounds.Left < 0
            ? Math.Ceiling(-contentBounds.Left + ExpansionPadding)
            : 0;
        var shiftY = allowOriginShift && contentBounds.Top < 0
            ? Math.Ceiling(-contentBounds.Top + ExpansionPadding)
            : 0;

        var width = safeWidth + shiftX;
        var height = safeHeight + shiftY;

        var requiredRight = contentBounds.Right + shiftX;
        var requiredBottom = contentBounds.Bottom + shiftY;
        if (requiredRight > width)
        {
            width = Math.Ceiling(requiredRight + ExpansionPadding);
        }

        if (requiredBottom > height)
        {
            height = Math.Ceiling(requiredBottom + ExpansionPadding);
        }

        return new AnnotationFrameExpansion(
            width,
            height,
            shiftX,
            shiftY,
            width != safeWidth || height != safeHeight || shiftX != 0 || shiftY != 0);
    }
}
