using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class CaptureImageComposerTests
{
    [Fact]
    public void PlanLayoutAppendsCapturesWithoutMovingEarlierPlacements()
    {
        var layout = CaptureImageComposer.PlanLayout(
            [
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
            ],
            gutter: 12);

        Assert.Equal(1612, layout.Width);
        Assert.Equal(1212, layout.Height);
        Assert.Equal(3, layout.Placements.Count);
        AssertPlacement(layout.Placements[0], x: 0, y: 0, width: 800, height: 600);
        AssertPlacement(layout.Placements[1], x: 812, y: 0, width: 800, height: 600);
        AssertPlacement(layout.Placements[2], x: 0, y: 612, width: 800, height: 600);
    }

    [Fact]
    public void PlanLayoutKeepsFiveShortcutCapturesInRows()
    {
        var layout = CaptureImageComposer.PlanLayout(
            [
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
            ],
            gutter: 12);

        Assert.Equal(2424, layout.Width);
        Assert.Equal(1212, layout.Height);
        AssertPlacement(layout.Placements[0], x: 0, y: 0, width: 800, height: 600);
        AssertPlacement(layout.Placements[1], x: 812, y: 0, width: 800, height: 600);
        AssertPlacement(layout.Placements[2], x: 1624, y: 0, width: 800, height: 600);
        AssertPlacement(layout.Placements[3], x: 0, y: 612, width: 800, height: 600);
        AssertPlacement(layout.Placements[4], x: 812, y: 612, width: 800, height: 600);
    }

    [Fact]
    public void PlanLayoutKeepsSixShortcutCapturesInThreeByTwoRows()
    {
        var layout = CaptureImageComposer.PlanLayout(
            [
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
                new CaptureImageSize(800, 600),
            ],
            gutter: 12);

        Assert.Equal(2424, layout.Width);
        Assert.Equal(1212, layout.Height);
        AssertPlacement(layout.Placements[0], x: 0, y: 0, width: 800, height: 600);
        AssertPlacement(layout.Placements[1], x: 812, y: 0, width: 800, height: 600);
        AssertPlacement(layout.Placements[2], x: 1624, y: 0, width: 800, height: 600);
        AssertPlacement(layout.Placements[3], x: 0, y: 612, width: 800, height: 600);
        AssertPlacement(layout.Placements[4], x: 812, y: 612, width: 800, height: 600);
        AssertPlacement(layout.Placements[5], x: 1624, y: 612, width: 800, height: 600);
    }

    [Fact]
    public void PlanLayoutAppendsToBottomWhenThatKeepsAspectCloser()
    {
        var layout = CaptureImageComposer.PlanLayout(
            [
                new CaptureImageSize(1200, 400),
                new CaptureImageSize(1200, 400),
            ],
            gutter: 12);

        Assert.Equal(1200, layout.Width);
        Assert.Equal(812, layout.Height);
        AssertPlacement(layout.Placements[0], x: 0, y: 0, width: 1200, height: 400);
        AssertPlacement(layout.Placements[1], x: 0, y: 412, width: 1200, height: 400);
    }

    [Fact]
    public void PlanLayoutPlacesTallCapturesSideBySide()
    {
        var layout = CaptureImageComposer.PlanLayout(
            [
                new CaptureImageSize(420, 900),
                new CaptureImageSize(420, 900),
            ],
            gutter: 12);

        Assert.Equal(852, layout.Width);
        Assert.Equal(900, layout.Height);
        AssertPlacement(layout.Placements[0], x: 0, y: 0, width: 420, height: 900);
        AssertPlacement(layout.Placements[1], x: 432, y: 0, width: 420, height: 900);
    }

    [Fact]
    public void PlanLayoutDoesNotCropOrResizeMismatchedCaptures()
    {
        var layout = CaptureImageComposer.PlanLayout(
            [
                new CaptureImageSize(1000, 500),
                new CaptureImageSize(300, 900),
            ],
            gutter: 12);

        Assert.Equal(1312, layout.Width);
        Assert.Equal(900, layout.Height);
        AssertPlacement(layout.Placements[0], x: 0, y: 0, width: 1000, height: 500);
        AssertPlacement(layout.Placements[1], x: 1012, y: 0, width: 300, height: 900);
    }

    [Fact]
    public void PlanLayoutRejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => CaptureImageComposer.PlanLayout([]));
    }

    private static void AssertPlacement(
        CaptureImagePlacement placement,
        int x,
        int y,
        int width,
        int height)
    {
        Assert.Equal(x, placement.X);
        Assert.Equal(y, placement.Y);
        Assert.Equal(width, placement.Width);
        Assert.Equal(height, placement.Height);
    }
}
