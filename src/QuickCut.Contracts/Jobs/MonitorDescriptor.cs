namespace QuickCut.Contracts.Jobs;

public sealed record MonitorDescriptor
{
    public required string Id { get; init; }

    public required string DeviceName { get; init; }

    public required bool IsPrimary { get; init; }
}
