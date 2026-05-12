namespace QuickCut.Capture.Services;

public enum QuickCutSingleInstanceCommandKind
{
    Show,
    Capture,
    CaptureClipboardOnly,
}

public sealed record QuickCutSingleInstanceCommand(QuickCutSingleInstanceCommandKind Kind)
{
    private const string ShowPayload = "show";
    private const string CapturePayload = "capture";
    private const string CaptureClipboardOnlyPayload = "capture-clipboard-only";

    public static QuickCutSingleInstanceCommand FromLaunchOptions(QuickCutLaunchOptions options)
    {
        if (options.StartCapture)
        {
            return new QuickCutSingleInstanceCommand(options.ClipboardOnly
                ? QuickCutSingleInstanceCommandKind.CaptureClipboardOnly
                : QuickCutSingleInstanceCommandKind.Capture);
        }

        return new QuickCutSingleInstanceCommand(QuickCutSingleInstanceCommandKind.Show);
    }

    public string ToPayload() => Kind switch
    {
        QuickCutSingleInstanceCommandKind.Capture => CapturePayload,
        QuickCutSingleInstanceCommandKind.CaptureClipboardOnly => CaptureClipboardOnlyPayload,
        _ => ShowPayload,
    };

    public static bool TryParse(string? payload, out QuickCutSingleInstanceCommand command)
    {
        command = new QuickCutSingleInstanceCommand(QuickCutSingleInstanceCommandKind.Show);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        command = payload.Trim().ToLowerInvariant() switch
        {
            CapturePayload => new QuickCutSingleInstanceCommand(QuickCutSingleInstanceCommandKind.Capture),
            CaptureClipboardOnlyPayload => new QuickCutSingleInstanceCommand(QuickCutSingleInstanceCommandKind.CaptureClipboardOnly),
            ShowPayload => new QuickCutSingleInstanceCommand(QuickCutSingleInstanceCommandKind.Show),
            _ => command,
        };

        return command.ToPayload().Equals(payload.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
