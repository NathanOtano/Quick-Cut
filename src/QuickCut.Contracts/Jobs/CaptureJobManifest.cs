namespace QuickCut.Contracts.Jobs;

public sealed record CaptureJobManifest
{
    public const string CurrentSchemaVersion = "1.0";

    public required string SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string JobId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string Source { get; init; }

    public required string ImagePath { get; init; }

    public required CaptureBounds Bounds { get; init; }

    public required MonitorDescriptor Monitor { get; init; }

    public required DpiDescriptor Dpi { get; init; }

    public string? ClipboardTextPath { get; init; }

    public string? IntentHint { get; init; }

    public required RoutingProfile RoutingProfile { get; init; }

    public required string ArtifactDir { get; init; }
}
