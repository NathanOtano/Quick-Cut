using QuickCut.Capture;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class CaptureActionToastWindowTests
{
    [Fact]
    public void AutoCloseDelayIsThirtySeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), CaptureActionToastWindow.AutoCloseDelay);
    }

    [Fact]
    public void ClipboardOnlyToastDoesNotExposeDiskActions()
    {
        var actions = CaptureActionToastWindow.GetAvailableActions(CaptureActionToastMode.ClipboardOnly);

        Assert.Empty(actions);
    }

    [Fact]
    public void SavedFileToastKeepsDeleteAction()
    {
        var actions = CaptureActionToastWindow.GetAvailableActions(CaptureActionToastMode.SavedFile);

        Assert.Contains(CaptureActionToastAction.Delete, actions);
    }
}
