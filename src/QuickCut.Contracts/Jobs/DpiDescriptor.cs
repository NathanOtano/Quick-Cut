namespace QuickCut.Contracts.Jobs;

public sealed record DpiDescriptor
{
    public required double ScaleX { get; init; }

    public required double ScaleY { get; init; }

    public required double DpiX { get; init; }

    public required double DpiY { get; init; }
}
