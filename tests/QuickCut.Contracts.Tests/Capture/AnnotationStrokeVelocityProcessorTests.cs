using System.Windows.Ink;
using System.Windows.Input;
using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class AnnotationStrokeVelocityProcessorTests
{
    [Fact]
    public void PressureForDistanceMakesFastSegmentsThickerThanSlowSegmentsByDefault()
    {
        var slow = AnnotationStrokeVelocityProcessor.PressureForDistance(1);
        var fast = AnnotationStrokeVelocityProcessor.PressureForDistance(30);

        Assert.True(fast > slow);
        Assert.InRange(slow, 0.41, 0.43);
    }

    [Fact]
    public void PressureForDistanceSupportsInverseSlowThickMode()
    {
        var slow = AnnotationStrokeVelocityProcessor.PressureForDistance(1, AnnotationSpeedSizeMode.SlowThick);
        var fast = AnnotationStrokeVelocityProcessor.PressureForDistance(30, AnnotationSpeedSizeMode.SlowThick);

        Assert.True(slow > fast);
        Assert.InRange(fast, 0.41, 0.43);
    }

    [Fact]
    public void ApplyScalesStrokeWidthAndPressureFactors()
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(0, 0) { PressureFactor = 1 },
            new StylusPoint(1, 0) { PressureFactor = 1 },
            new StylusPoint(30, 0) { PressureFactor = 1 },
        };
        var stroke = new Stroke(points, new DrawingAttributes
        {
            Width = 8,
            Height = 8,
            IgnorePressure = true,
        });

        AnnotationStrokeVelocityProcessor.Apply(stroke, combineHardwarePressure: false, AnnotationSpeedSizeMode.FastThick);

        Assert.False(stroke.DrawingAttributes.IgnorePressure);
        Assert.True(stroke.DrawingAttributes.Width > 8);
        Assert.True(stroke.StylusPoints[2].PressureFactor > stroke.StylusPoints[0].PressureFactor);
    }

    [Fact]
    public void ApplyLeavesStrokeUnchangedWhenSpeedSizeIsOff()
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(0, 0) { PressureFactor = 1 },
            new StylusPoint(30, 0) { PressureFactor = 1 },
        };
        var stroke = new Stroke(points, new DrawingAttributes
        {
            Width = 8,
            Height = 8,
            IgnorePressure = true,
        });

        AnnotationStrokeVelocityProcessor.Apply(stroke, combineHardwarePressure: false, AnnotationSpeedSizeMode.Off);

        Assert.True(stroke.DrawingAttributes.IgnorePressure);
        Assert.Equal(8, stroke.DrawingAttributes.Width);
        Assert.Equal(1, stroke.StylusPoints[0].PressureFactor);
    }

    [Fact]
    public void ApplyUsesStrengthToKeepBaseSizeCoherent()
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(0, 0) { PressureFactor = 1 },
            new StylusPoint(1, 0) { PressureFactor = 1 },
            new StylusPoint(30, 0) { PressureFactor = 1 },
        };
        var stroke = new Stroke(points, new DrawingAttributes
        {
            Width = 8,
            Height = 8,
            IgnorePressure = true,
        });

        AnnotationStrokeVelocityProcessor.Apply(
            stroke,
            combineHardwarePressure: false,
            AnnotationSpeedSizeMode.FastThick,
            strength: 0.35);

        Assert.False(stroke.DrawingAttributes.IgnorePressure);
        Assert.InRange(stroke.DrawingAttributes.Width, 8, 10);
        Assert.InRange(stroke.StylusPoints.Min(point => point.PressureFactor), 0.75, 1);
    }

    [Fact]
    public void ApplyUsesObservedStrokeDistancesForVisibleSpeedResponse()
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(0, 0) { PressureFactor = 1 },
            new StylusPoint(1, 0) { PressureFactor = 1 },
            new StylusPoint(10, 0) { PressureFactor = 1 },
        };
        var stroke = new Stroke(points, new DrawingAttributes
        {
            Width = 8,
            Height = 8,
            IgnorePressure = true,
        });

        AnnotationStrokeVelocityProcessor.Apply(
            stroke,
            combineHardwarePressure: false,
            AnnotationSpeedSizeMode.FastThick);

        Assert.InRange(stroke.StylusPoints[0].PressureFactor, 0.41, 0.47);
        Assert.InRange(stroke.StylusPoints[2].PressureFactor, 0.95, 1);
    }

    [Fact]
    public void CreateSpeedSizedStrokesRendersSpeedAsExplicitSegmentWidths()
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(0, 0) { PressureFactor = 1 },
            new StylusPoint(1, 0) { PressureFactor = 1 },
            new StylusPoint(10, 0) { PressureFactor = 1 },
        };
        var stroke = new Stroke(points, new DrawingAttributes
        {
            Width = 8,
            Height = 8,
            IgnorePressure = true,
        });

        var fragments = AnnotationStrokeVelocityProcessor.CreateSpeedSizedStrokes(
            stroke,
            combineHardwarePressure: false,
            AnnotationSpeedSizeMode.FastThick);

        Assert.Equal(2, fragments.Count);
        Assert.True(fragments[1].DrawingAttributes.Width > fragments[0].DrawingAttributes.Width);
        Assert.All(fragments, fragment => Assert.True(fragment.DrawingAttributes.IgnorePressure));
    }

    [Fact]
    public void ApplyLeavesStrokeUnchangedWhenStrengthIsZero()
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(0, 0) { PressureFactor = 1 },
            new StylusPoint(30, 0) { PressureFactor = 1 },
        };
        var stroke = new Stroke(points, new DrawingAttributes
        {
            Width = 8,
            Height = 8,
            IgnorePressure = true,
        });

        AnnotationStrokeVelocityProcessor.Apply(
            stroke,
            combineHardwarePressure: false,
            AnnotationSpeedSizeMode.FastThick,
            strength: 0);

        Assert.True(stroke.DrawingAttributes.IgnorePressure);
        Assert.Equal(8, stroke.DrawingAttributes.Width);
        Assert.All(stroke.StylusPoints, point => Assert.Equal(1, point.PressureFactor));
    }
}
