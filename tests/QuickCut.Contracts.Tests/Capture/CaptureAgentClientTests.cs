using System.Net.Http;
using QuickCut.Capture.Services;
using QuickCut.Contracts.Jobs;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class CaptureAgentClientTests
{
    [Fact]
    public async Task TryPostJobAsyncReturnsUnavailableWhenAgentCannotBeReached()
    {
        using var httpClient = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://127.0.0.1:8765/"),
        };
        var client = new CaptureAgentClient(httpClient, TimeSpan.FromMilliseconds(100));

        var result = await client.TryPostJobAsync(CreateManifest());

        Assert.Equal(CaptureAgentPostStatus.Unavailable, result.Status);
    }

    private static CaptureJobManifest CreateManifest() => new()
    {
        SchemaVersion = CaptureJobManifest.CurrentSchemaVersion,
        JobId = "qc-20260505-000001",
        CreatedAt = new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
        Source = "capture-stub",
        ImagePath = "C:/Users/Example/AppData/Local/QuickCut/jobs/qc-20260505-000001/capture.png",
        Bounds = new CaptureBounds { X = 0, Y = 0, Width = 1, Height = 1 },
        Monitor = new MonitorDescriptor { Id = "primary", DeviceName = "DISPLAY1", IsPrimary = true },
        Dpi = new DpiDescriptor { ScaleX = 1.0, ScaleY = 1.0, DpiX = 96, DpiY = 96 },
        RoutingProfile = RoutingProfile.OfflineOnly,
        ArtifactDir = "C:/Users/Example/AppData/Local/QuickCut/jobs/qc-20260505-000001",
    };

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("connection refused");
        }
    }
}

