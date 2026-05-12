using QuickCut.Contracts.Jobs;

namespace QuickCut.Capture.Services;

public sealed record CaptureJobWriteResult(
    string JobId,
    string ManifestPath,
    string ImagePath,
    CaptureJobManifest Manifest);
