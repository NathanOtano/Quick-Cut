using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class GlobalHotKeyServiceTests
{
    [Fact]
    public void DefaultHotKeySummaryIncludesWindowsLessThanAndAltGrPrintScreen()
    {
        Assert.Equal("Win + < ou AltGr + Impr. écran", GlobalHotKeyService.GetDefaultHotKeySummary());
    }

    [Fact]
    public void DefaultHotKeyLabelsKeepPrimaryShortcutsReadable()
    {
        Assert.Equal(["Win + <", "AltGr + Impr. écran"], GlobalHotKeyService.DefaultHotKeyLabels);
    }
}
