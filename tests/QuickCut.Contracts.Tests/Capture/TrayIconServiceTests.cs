using Forms = System.Windows.Forms;
using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class TrayIconServiceTests
{
    [Theory]
    [InlineData(Forms.MouseButtons.Left, true)]
    [InlineData(Forms.MouseButtons.Right, false)]
    [InlineData(Forms.MouseButtons.Middle, false)]
    public void ShouldRequestCaptureOnlyForLeftClick(Forms.MouseButtons button, bool expected)
    {
        Assert.Equal(expected, TrayIconService.ShouldRequestCapture(button));
    }
}
