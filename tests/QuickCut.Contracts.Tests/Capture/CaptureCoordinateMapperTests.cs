using System.Windows;
using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class CaptureCoordinateMapperTests
{
    [Fact]
    public void CreatePixelRegionScalesDipSelectionToPhysicalPixels()
    {
        var region = CaptureCoordinateMapper.CreatePixelRegion(
            new Point(0, 0),
            new Point(1706.6667, 960),
            pixelScaleX: 1.5,
            pixelScaleY: 1.5,
            maxPixelWidth: 2560,
            maxPixelHeight: 1440);

        Assert.Equal(new Int32Rect(0, 0, 2560, 1440), region);
    }

    [Fact]
    public void CreatePixelRegionHandlesReverseDragAndClampsToImage()
    {
        var region = CaptureCoordinateMapper.CreatePixelRegion(
            new Point(900, 700),
            new Point(-10, 100),
            pixelScaleX: 2,
            pixelScaleY: 2,
            maxPixelWidth: 1600,
            maxPixelHeight: 1200);

        Assert.Equal(new Int32Rect(0, 200, 1600, 1000), region);
    }

    [Fact]
    public void CreatePixelRegionSnapsNearScreenEdgesToFullSize()
    {
        var region = CaptureCoordinateMapper.CreatePixelRegion(
            new Point(1, 1),
            new Point(1919, 1079),
            pixelScaleX: 1,
            pixelScaleY: 1,
            maxPixelWidth: 1920,
            maxPixelHeight: 1080);

        Assert.Equal(new Int32Rect(0, 0, 1920, 1080), region);
    }
}
