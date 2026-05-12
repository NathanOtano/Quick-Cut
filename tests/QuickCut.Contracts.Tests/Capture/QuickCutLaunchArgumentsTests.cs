using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class QuickCutLaunchArgumentsTests
{
    [Fact]
    public void ParseCaptureStartsHiddenAndRequestsCapture()
    {
        var options = QuickCutLaunchArguments.Parse(["--capture"]);

        Assert.True(options.StartInTray);
        Assert.True(options.StartCapture);
        Assert.False(options.ClipboardOnly);
        Assert.False(options.HotKeyLauncher);
    }

    [Fact]
    public void ParseClipboardCaptureKeepsClipboardOnly()
    {
        var options = QuickCutLaunchArguments.Parse(["--capture", "--clipboard-only"]);

        Assert.True(options.StartInTray);
        Assert.True(options.StartCapture);
        Assert.True(options.ClipboardOnly);
    }

    [Fact]
    public void ParseLegacyHotKeyLauncherStartsTrayMode()
    {
        var options = QuickCutLaunchArguments.Parse(["--hotkey-launcher"]);

        Assert.True(options.StartInTray);
        Assert.False(options.StartCapture);
        Assert.True(options.HotKeyLauncher);
    }

    [Fact]
    public void BuildCaptureArgumentsAddsClipboardFlagOnlyWhenRequested()
    {
        Assert.Equal("--capture", QuickCutLaunchArguments.BuildCaptureArguments(clipboardOnly: false));
        Assert.Equal("--capture --clipboard-only", QuickCutLaunchArguments.BuildCaptureArguments(clipboardOnly: true));
    }
}
