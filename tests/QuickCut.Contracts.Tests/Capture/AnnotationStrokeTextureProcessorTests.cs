using System.Windows.Ink;
using System.Windows.Input;
using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class AnnotationStrokeTextureProcessorTests
{
    [Fact]
    public void ApplyPencilTextureAddsDeterministicWidthVariation()
    {
        var stroke = CreateStroke();
        var originalPoints = stroke.StylusPoints
            .Select(point => (point.X, point.Y))
            .ToArray();

        AnnotationStrokeTextureProcessor.ApplyPencilTexture(
            stroke,
            variation: 0.8,
            combineHardwarePressure: false);

        Assert.False(stroke.DrawingAttributes.IgnorePressure);
        Assert.False(stroke.DrawingAttributes.FitToCurve);
        Assert.True(stroke.DrawingAttributes.Width > 6);
        Assert.True(stroke.StylusPoints.Select(point => point.PressureFactor).Distinct().Count() > 1);
        Assert.InRange(stroke.StylusPoints.Min(point => point.PressureFactor), 0.25, 1);
        Assert.Contains(
            Enumerable.Range(1, stroke.StylusPoints.Count - 2),
            index => stroke.StylusPoints[index].X != originalPoints[index].X
                || stroke.StylusPoints[index].Y != originalPoints[index].Y);
    }

    [Fact]
    public void ApplyPencilTextureLeavesStrokeUnchangedWhenVariationIsZero()
    {
        var stroke = CreateStroke();

        AnnotationStrokeTextureProcessor.ApplyPencilTexture(
            stroke,
            variation: 0,
            combineHardwarePressure: false);

        Assert.True(stroke.DrawingAttributes.IgnorePressure);
        Assert.Equal(6, stroke.DrawingAttributes.Width);
        Assert.All(stroke.StylusPoints, point => Assert.Equal(1, point.PressureFactor));
    }

    [Fact]
    public void CreatePencilTextureStrokesRendersGrainAsExplicitSegmentWidths()
    {
        var stroke = CreateStroke();

        var fragments = AnnotationStrokeTextureProcessor.CreatePencilTextureStrokes(
            stroke,
            variation: 0.8,
            combineHardwarePressure: false);

        Assert.Equal(stroke.StylusPoints.Count - 1, fragments.Count);
        Assert.True(fragments.Select(fragment => fragment.DrawingAttributes.Width).Distinct().Count() > 1);
        Assert.All(fragments, fragment =>
        {
            Assert.True(fragment.DrawingAttributes.IgnorePressure);
            Assert.False(fragment.DrawingAttributes.FitToCurve);
        });
    }

    private static Stroke CreateStroke()
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(0, 0) { PressureFactor = 1 },
            new StylusPoint(4, 1) { PressureFactor = 1 },
            new StylusPoint(8, 3) { PressureFactor = 1 },
            new StylusPoint(12, 4) { PressureFactor = 1 },
        };

        return new Stroke(points, new DrawingAttributes
        {
            Width = 6,
            Height = 6,
            IgnorePressure = true,
        });
    }
}
