using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class QuickCutSingleInstanceCommandTests
{
    [Fact]
    public void FromLaunchOptionsMapsCaptureModes()
    {
        Assert.Equal(
            QuickCutSingleInstanceCommandKind.Capture,
            QuickCutSingleInstanceCommand.FromLaunchOptions(
                new QuickCutLaunchOptions(true, true, false, false)).Kind);

        Assert.Equal(
            QuickCutSingleInstanceCommandKind.CaptureClipboardOnly,
            QuickCutSingleInstanceCommand.FromLaunchOptions(
                new QuickCutLaunchOptions(true, true, true, false)).Kind);
    }

    [Fact]
    public void FromLaunchOptionsMapsPlainLaunchToShow()
    {
        Assert.Equal(
            QuickCutSingleInstanceCommandKind.Show,
            QuickCutSingleInstanceCommand.FromLaunchOptions(
                new QuickCutLaunchOptions(false, false, false, false)).Kind);
    }

    [Theory]
    [InlineData("show", QuickCutSingleInstanceCommandKind.Show)]
    [InlineData("capture", QuickCutSingleInstanceCommandKind.Capture)]
    [InlineData("capture-clipboard-only", QuickCutSingleInstanceCommandKind.CaptureClipboardOnly)]
    public void TryParseAcceptsKnownPayloads(string payload, QuickCutSingleInstanceCommandKind kind)
    {
        var parsed = QuickCutSingleInstanceCommand.TryParse(payload, out var command);

        Assert.True(parsed);
        Assert.Equal(kind, command.Kind);
        Assert.Equal(payload, command.ToPayload());
    }

    [Fact]
    public void TryParseRejectsUnknownPayloads()
    {
        Assert.False(QuickCutSingleInstanceCommand.TryParse("unknown", out _));
    }
}
