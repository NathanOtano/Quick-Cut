using System.Windows;
using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class AnnotationFramePlannerTests
{
    [Fact]
    public void PlanExpansionKeepsFrameWhenContentIsInside()
    {
        var expansion = AnnotationFramePlanner.PlanExpansion(
            currentWidth: 400,
            currentHeight: 300,
            contentBounds: new Rect(0, 0, 400, 300),
            allowOriginShift: true);

        Assert.False(expansion.Changed);
        Assert.Equal(400, expansion.Width);
        Assert.Equal(300, expansion.Height);
        Assert.Equal(0, expansion.ShiftX);
        Assert.Equal(0, expansion.ShiftY);
    }

    [Fact]
    public void PlanExpansionExtendsRightAndBottomWithTransparentPadding()
    {
        var expansion = AnnotationFramePlanner.PlanExpansion(
            currentWidth: 400,
            currentHeight: 300,
            contentBounds: new Rect(0, 0, 435.2, 321.4),
            allowOriginShift: true);

        Assert.True(expansion.Changed);
        Assert.Equal(460, expansion.Width);
        Assert.Equal(346, expansion.Height);
        Assert.Equal(0, expansion.ShiftX);
        Assert.Equal(0, expansion.ShiftY);
    }

    [Fact]
    public void PlanExpansionShiftsContentWhenItLeavesLeftOrTop()
    {
        var expansion = AnnotationFramePlanner.PlanExpansion(
            currentWidth: 400,
            currentHeight: 300,
            contentBounds: new Rect(-10.5, -20.2, 90, 80),
            allowOriginShift: true);

        Assert.True(expansion.Changed);
        Assert.Equal(35, expansion.ShiftX);
        Assert.Equal(45, expansion.ShiftY);
        Assert.Equal(435, expansion.Width);
        Assert.Equal(345, expansion.Height);
    }

    [Fact]
    public void PlanExpansionCanDeferLeftAndTopShiftDuringDrag()
    {
        var expansion = AnnotationFramePlanner.PlanExpansion(
            currentWidth: 400,
            currentHeight: 300,
            contentBounds: new Rect(-10.5, -20.2, 90, 80),
            allowOriginShift: false);

        Assert.False(expansion.Changed);
        Assert.Equal(400, expansion.Width);
        Assert.Equal(300, expansion.Height);
        Assert.Equal(0, expansion.ShiftX);
        Assert.Equal(0, expansion.ShiftY);
    }
}
