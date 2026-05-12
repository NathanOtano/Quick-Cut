namespace QuickCut.Contracts.Jobs;

public sealed record CaptureBounds
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }
}
