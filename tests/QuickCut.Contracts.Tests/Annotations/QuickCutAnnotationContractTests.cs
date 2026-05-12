using QuickCut.Contracts.Annotations;

namespace QuickCut.Contracts.Tests.Annotations;

public sealed class QuickCutAnnotationContractTests
{
    [Fact]
    public void ToolOrderMatchesPdfEguiAndQuickCutSurface()
    {
        Assert.Equal(
            [
                "Pointer",
                "Pen",
                "Pencil",
                "Highlighter",
                "Eraser",
                "Select",
                "Pan",
                "Text",
            ],
            QuickCutAnnotationDefaults.ToolOrder);
    }

    [Fact]
    public void DefaultsSeparateCaptureAndPdfEguiEntryModes()
    {
        Assert.Equal("Pen", QuickCutAnnotationDefaults.CaptureDefaultTool);
        Assert.Equal("Pointer", QuickCutAnnotationDefaults.PdfEguiDefaultTool);
        Assert.Equal("Annotations", QuickCutAnnotationDefaults.DefaultSelectionTarget);
        Assert.Equal("#FFFF0000", QuickCutAnnotationDefaults.DefaultColorHex);
    }
}
