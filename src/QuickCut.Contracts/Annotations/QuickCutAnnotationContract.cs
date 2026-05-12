namespace QuickCut.Contracts.Annotations;

public enum QuickCutAnnotationTool
{
    Pointer,
    Pen,
    Pencil,
    Highlighter,
    Eraser,
    Select,
    Pan,
    Text,
}

public enum QuickCutAnnotationSelectionTarget
{
    Annotations,
    Image,
    Both,
}

public static class QuickCutAnnotationDefaults
{
    public const string CaptureDefaultTool = nameof(QuickCutAnnotationTool.Pen);
    public const string PdfEguiDefaultTool = nameof(QuickCutAnnotationTool.Pointer);
    public const string DefaultSelectionTarget = nameof(QuickCutAnnotationSelectionTarget.Annotations);
    public const string DefaultColorHex = "#FFFF0000";

    public static readonly string[] ToolOrder =
    [
        nameof(QuickCutAnnotationTool.Pointer),
        nameof(QuickCutAnnotationTool.Pen),
        nameof(QuickCutAnnotationTool.Pencil),
        nameof(QuickCutAnnotationTool.Highlighter),
        nameof(QuickCutAnnotationTool.Eraser),
        nameof(QuickCutAnnotationTool.Select),
        nameof(QuickCutAnnotationTool.Pan),
        nameof(QuickCutAnnotationTool.Text),
    ];
}
