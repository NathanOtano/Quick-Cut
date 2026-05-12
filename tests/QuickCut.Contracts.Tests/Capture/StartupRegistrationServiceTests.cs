using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void BuildRunCommandQuotesExecutablePathAndStartsTrayInstance()
    {
        var command = StartupRegistrationService.BuildRunCommand(@"C:\Program Files\QuickCut\QuickCut.Capture.exe");

        Assert.Equal("\"C:\\Program Files\\QuickCut\\QuickCut.Capture.exe\" --tray", command);
    }

    [Fact]
    public void BuildTrayCommandKeepsManualTrayLaunch()
    {
        var command = StartupRegistrationService.BuildTrayCommand(@"C:\Program Files\QuickCut\QuickCut.Capture.exe");

        Assert.Equal("\"C:\\Program Files\\QuickCut\\QuickCut.Capture.exe\" --tray", command);
    }
}
