namespace QuickCut.Capture.Services;

public sealed record QuickCutLaunchOptions(
    bool StartInTray,
    bool StartCapture,
    bool ClipboardOnly,
    bool HotKeyLauncher);

public static class QuickCutLaunchArguments
{
    public const string TrayArgument = "--tray";
    public const string CaptureArgument = "--capture";
    public const string ClipboardOnlyArgument = "--clipboard-only";
    public const string HotKeyLauncherArgument = "--hotkey-launcher";

    public static QuickCutLaunchOptions Parse(IEnumerable<string> arguments)
    {
        var normalized = arguments
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .Select(argument => argument.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hotKeyLauncher = normalized.Contains(HotKeyLauncherArgument);
        var startCapture = normalized.Contains(CaptureArgument);
        return new QuickCutLaunchOptions(
            StartInTray: normalized.Contains(TrayArgument) || startCapture || hotKeyLauncher,
            StartCapture: startCapture,
            ClipboardOnly: normalized.Contains(ClipboardOnlyArgument),
            HotKeyLauncher: hotKeyLauncher);
    }

    public static string BuildCaptureArguments(bool clipboardOnly)
    {
        return clipboardOnly
            ? $"{CaptureArgument} {ClipboardOnlyArgument}"
            : CaptureArgument;
    }
}
