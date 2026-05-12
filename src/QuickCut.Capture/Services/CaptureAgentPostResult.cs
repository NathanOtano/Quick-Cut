namespace QuickCut.Capture.Services;

public sealed record CaptureAgentPostResult(
    CaptureAgentPostStatus Status,
    string Message,
    int? StatusCode = null)
{
    public static CaptureAgentPostResult Ingested(int statusCode) =>
        new(CaptureAgentPostStatus.Ingested, "job ingere par l'agent local", statusCode);

    public static CaptureAgentPostResult Unavailable(string message) =>
        new(CaptureAgentPostStatus.Unavailable, message);

    public static CaptureAgentPostResult Rejected(int statusCode, string message) =>
        new(CaptureAgentPostStatus.Rejected, message, statusCode);
}
