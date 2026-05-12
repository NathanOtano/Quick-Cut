using System.Text.Json;
using QuickCut.Contracts;
using QuickCut.Contracts.Jobs;
using QuickCut.Contracts.Validation;

namespace QuickCut.Contracts.Tests.Jobs;

public sealed class CaptureJobManifestTests
{
    [Fact]
    public void ValidManifestSerializesWithSnakeCaseContract()
    {
        var manifest = CreateManifest();

        var json = JsonSerializer.Serialize(manifest, QuickCutJson.DefaultOptions);

        Assert.Contains("\"schema_version\"", json, StringComparison.Ordinal);
        Assert.Contains("\"routing_profile\": \"offline-only\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SchemaVersion", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatorRejectsInvalidDimensions()
    {
        var manifest = CreateManifest() with
        {
            Bounds = new CaptureBounds { X = 0, Y = 0, Width = 0, Height = 1080 },
        };

        var result = CaptureJobManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("bounds.width", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidManifestPassesValidation()
    {
        var result = CaptureJobManifestValidator.Validate(CreateManifest());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static CaptureJobManifest CreateManifest() => new()
    {
        SchemaVersion = CaptureJobManifest.CurrentSchemaVersion,
        JobId = "qc-20260503-000001",
        CreatedAt = new DateTimeOffset(2026, 5, 3, 12, 0, 0, TimeSpan.Zero),
        Source = "capture-stub",
        ImagePath = "C:/Users/Example/AppData/Local/QuickCut/jobs/qc-20260503-000001/capture.png",
        Bounds = new CaptureBounds { X = 0, Y = 0, Width = 1920, Height = 1080 },
        Monitor = new MonitorDescriptor { Id = "primary", DeviceName = "DISPLAY1", IsPrimary = true },
        Dpi = new DpiDescriptor { ScaleX = 1.0, ScaleY = 1.0, DpiX = 96, DpiY = 96 },
        RoutingProfile = RoutingProfile.OfflineOnly,
        ArtifactDir = "C:/Users/Example/AppData/Local/QuickCut/jobs/qc-20260503-000001",
    };
}

