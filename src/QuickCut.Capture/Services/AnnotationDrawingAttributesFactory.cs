using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickCut.Capture.Services;

public static class AnnotationDrawingAttributesFactory
{
    public static DrawingAttributes Create(Color color, double width, bool isHighlighter, bool pressureEnabled, bool speedSizeEnabled = false)
    {
        var safeWidth = Math.Clamp(width, 1, 80);
        return new DrawingAttributes
        {
            Color = color,
            Width = safeWidth,
            Height = safeWidth,
            FitToCurve = true,
            IgnorePressure = !pressureEnabled && !speedSizeEnabled,
            IsHighlighter = isHighlighter,
            StylusTip = StylusTip.Ellipse,
        };
    }
}
