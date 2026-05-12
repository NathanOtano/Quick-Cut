using System.Windows.Media;
using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class AnnotationDrawingAttributesFactoryTests
{
    [Fact]
    public void CreateKeepsPressureEnabledForTabletInk()
    {
        var attributes = AnnotationDrawingAttributesFactory.Create(Colors.Blue, 7, isHighlighter: false, pressureEnabled: true);

        Assert.Equal(Colors.Blue, attributes.Color);
        Assert.Equal(7, attributes.Width);
        Assert.Equal(7, attributes.Height);
        Assert.False(attributes.IgnorePressure);
        Assert.False(attributes.IsHighlighter);
    }

    [Fact]
    public void CreateSupportsHighlighterAndPressureOptOut()
    {
        var attributes = AnnotationDrawingAttributesFactory.Create(Colors.Yellow, 18, isHighlighter: true, pressureEnabled: false);

        Assert.True(attributes.IsHighlighter);
        Assert.True(attributes.IgnorePressure);
    }

    [Fact]
    public void CreateKeepsPressurePipelineEnabledForSpeedSize()
    {
        var attributes = AnnotationDrawingAttributesFactory.Create(
            Colors.Red,
            4,
            isHighlighter: false,
            pressureEnabled: false,
            speedSizeEnabled: true);

        Assert.False(attributes.IgnorePressure);
    }
}
