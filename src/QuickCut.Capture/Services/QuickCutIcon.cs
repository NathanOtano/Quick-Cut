using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace QuickCut.Capture.Services;

public enum QuickCutIcon
{
    Pen,
    Pencil,
    Highlighter,
    Eraser,
    Select,
    Hand,
    ZoomIn,
    ZoomOut,
    ActualSize,
    Fit,
    RotateLeft,
    RotateRight,
    Palette,
    StrokeWidth,
    BrushVariation,
    Pressure,
    Speed,
    AnnotationTarget,
    ImageTarget,
    BothTargets,
    Delete,
    ClearAnnotations,
    Undo,
    Redo,
    Copy,
    Capture,
    Save,
    SaveAs,
    RevealInFolder,
    OpenWith,
}

public static class QuickCutIconGlyphs
{
    public static string PathData(QuickCutIcon icon) => icon switch
    {
        QuickCutIcon.Pen => "M4,20 L8,19 L20,7 L17,4 L5,16 Z M14,7 L17,10 M5,16 L8,19",
        QuickCutIcon.Pencil => "M4,20 L8,19 L19,8 L16,5 L5,16 Z M13,8 L16,11 M5,16 L8,19 M18,4 L20,6",
        QuickCutIcon.Highlighter => "M4,20 L9,19 L20,8 L16,4 L5,15 Z M14,6 L18,10 M3,21 L11,21",
        QuickCutIcon.Eraser => "M7,21 L21,21 M3,17 L14,6 L18,10 L8,20 L4,20 Z M11,9 L15,13",
        QuickCutIcon.Select => "M4,4 L10,4 M4,4 L4,10 M20,4 L14,4 M20,4 L20,10 M20,20 L14,20 M20,20 L20,14 M4,20 L10,20 M4,20 L4,14",
        QuickCutIcon.Hand => "M8,12 L8,7 M11,12 L11,6 M14,12 L14,8 M17,13 L17,10 M8,7 C8,5.8 9.8,5.8 9.8,7 L9.8,12 M11,6 C11,4.8 13,4.8 13,6 L13,12 M14,8 C14,6.8 16,6.8 16,8 L16,13 M17,10 C17,8.8 19,8.8 19,10 L19,15 C19,19 16,22 12,22 L10.5,22 C8,22 6,20.5 5,18 L3.5,14.5 C3,13.4 4.6,12.6 5.4,13.5 L8,16 Z",
        QuickCutIcon.ZoomIn => "M10,4 A6,6 0 1 0 10,16 A6,6 0 1 0 10,4 M14.5,14.5 L21,21 M10,7 L10,13 M7,10 L13,10",
        QuickCutIcon.ZoomOut => "M10,4 A6,6 0 1 0 10,16 A6,6 0 1 0 10,4 M14.5,14.5 L21,21 M7,10 L13,10",
        QuickCutIcon.ActualSize => "M4,4 L20,4 L20,20 L4,20 Z M8,8 L8,16 M7,9 L8,8 L8,16 M12,8 L12,16 M16,8 L16,16 M14,8 L18,8 M14,16 L18,16",
        QuickCutIcon.Fit => "M4,9 L4,4 L9,4 M15,4 L20,4 L20,9 M20,15 L20,20 L15,20 M9,20 L4,20 L4,15 M8,8 L16,16 M16,8 L8,16",
        QuickCutIcon.RotateLeft => "M4,4 L4,10 L10,10 M5,10 C6,6.5 9,4 13,4 C17.4,4 21,7.6 21,12 C21,16.4 17.4,20 13,20 C10.2,20 7.8,18.6 6.4,16.5",
        QuickCutIcon.RotateRight => "M20,4 L20,10 L14,10 M19,10 C18,6.5 15,4 11,4 C6.6,4 3,7.6 3,12 C3,16.4 6.6,20 11,20 C13.8,20 16.2,18.6 17.6,16.5",
        QuickCutIcon.Palette => "M12,3 C7,3 3,6.8 3,11.5 C3,15.5 6,19 10,19 L11.5,19 C12.5,19 13,19.8 13,20.7 C13,21.4 13.6,22 14.4,22 C18.6,22 22,18.4 22,14 C22,8 17.5,3 12,3 M7.5,11.5 L7.6,11.5 M10,7.5 L10.1,7.5 M14,7.5 L14.1,7.5 M17,11 L17.1,11",
        QuickCutIcon.StrokeWidth => "M4,7 L20,7 M4,12 L20,12 M4,17 L20,17",
        QuickCutIcon.BrushVariation => "M4,17 C7,9 17,9 20,17 M5,18 L5.1,18 M9,15 L9.1,15 M13,12 L13.1,12 M17,15 L17.1,15 M20,18 L20.1,18",
        QuickCutIcon.Pressure => "M12,3 L19,21 L12,17 L5,21 Z M12,3 L12,17",
        QuickCutIcon.Speed => "M4,16 A8,8 0 1 1 20,16 M12,16 L17,10 M7,17 L17,17 M8,11 L8.1,11 M12,8 L12.1,8 M16,11 L16.1,11",
        QuickCutIcon.AnnotationTarget => "M4,20 L8,19 L18,9 L15,6 L5,16 Z M13,8 L16,11 M5,4 L19,4",
        QuickCutIcon.ImageTarget => "M4,5 L20,5 L20,19 L4,19 Z M8,13 L10.5,10 L14,14 L16,12 L20,17 M8,9 L8.1,9",
        QuickCutIcon.BothTargets => "M7,7 L19,7 L19,19 L7,19 Z M4,4 L16,4 L16,16 M7,13 L10,10 L13,13 L15,11 L19,16",
        QuickCutIcon.Delete => "M4,7 L20,7 M10,11 L10,17 M14,11 L14,17 M6,7 L7,21 L17,21 L18,7 M9,7 L9,4 L15,4 L15,7",
        QuickCutIcon.ClearAnnotations => "M4,7 L20,7 M10,11 L10,16 M14,11 L14,16 M6,7 L7,18 L17,18 L18,7 M9,7 L9,4 L15,4 L15,7 M4,21 L20,21 M7,21 L17,21",
        QuickCutIcon.Undo => "M9,14 L4,9 L9,4 M4,9 L15,9 C18,9 20,11 20,14 C20,17 18,19 15,19 L8,19",
        QuickCutIcon.Redo => "M15,14 L20,9 L15,4 M20,9 L9,9 C6,9 4,11 4,14 C4,17 6,19 9,19 L16,19",
        QuickCutIcon.Copy => "M8,8 L20,8 L20,20 L8,20 Z M4,16 L4,4 L16,4",
        QuickCutIcon.Capture => "M4,7 L8,7 L9.5,5 L14.5,5 L16,7 L20,7 L20,19 L4,19 Z M12,10 A3.5,3.5 0 1 0 12,17 A3.5,3.5 0 1 0 12,10 M18,10 L18.1,10",
        QuickCutIcon.Save => "M5,4 L17,4 L20,7 L20,20 L4,20 L4,4 Z M8,4 L8,10 L16,10 L16,4 M8,20 L8,14 L16,14 L16,20",
        QuickCutIcon.SaveAs => "M5,4 L17,4 L20,7 L20,20 L4,20 L4,4 Z M8,4 L8,10 L16,10 L16,4 M8,20 L8,15 L13,15 M14,17 L21,10 M17,10 L21,10 L21,14",
        QuickCutIcon.RevealInFolder => "M3,6 L9,6 L11,8 L21,8 L21,19 L3,19 Z M3,10 L21,10 M12,14 L17,14 M14.5,11.5 L14.5,16.5",
        QuickCutIcon.OpenWith => "M5,5 L13,5 M5,5 L5,19 L19,19 L19,11 M12,12 L20,4 M15,4 L20,4 L20,9",
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null),
    };

    public static Path Create(QuickCutIcon icon)
    {
        var path = new Path
        {
            Data = Geometry.Parse(PathData(icon)),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false,
        };
        path.SetResourceReference(Shape.StrokeProperty, QuickCutTheme.TextBrushKey);
        return path;
    }

    public static Viewbox CreateViewbox(QuickCutIcon icon, double size = 18)
    {
        var canvas = new Canvas
        {
            Width = 24,
            Height = 24,
            IsHitTestVisible = false,
        };
        canvas.Children.Add(Create(icon));

        return new Viewbox
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Child = canvas,
            IsHitTestVisible = false,
        };
    }
}
