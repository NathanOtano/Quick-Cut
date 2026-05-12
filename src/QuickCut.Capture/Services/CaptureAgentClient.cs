using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using QuickCut.Contracts;
using QuickCut.Contracts.Jobs;

namespace QuickCut.Capture.Services;

public sealed class CaptureAgentClient
{
    public static readonly Uri DefaultBaseAddress = new("http://127.0.0.1:8765/");

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    public CaptureAgentClient()
        : this(new HttpClient { BaseAddress = DefaultBaseAddress }, TimeSpan.FromSeconds(2))
    {
    }

    public CaptureAgentClient(HttpClient httpClient, TimeSpan? timeout = null)
    {
        _httpClient = httpClient;
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = DefaultBaseAddress;
        }

        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public async Task<CaptureAgentPostResult> TryPostJobAsync(
        CaptureJobManifest manifest,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            var json = JsonSerializer.Serialize(manifest, QuickCutJson.DefaultOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("v1/capture-jobs", content, timeout.Token);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                return CaptureAgentPostResult.Ingested((int)response.StatusCode);
            }

            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var message = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "ingestion refusee" : body;
            return CaptureAgentPostResult.Rejected((int)response.StatusCode, message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CaptureAgentPostResult.Unavailable("agent local indisponible ou trop lent");
        }
        catch (HttpRequestException ex)
        {
            return CaptureAgentPostResult.Unavailable(ex.Message);
        }
    }
}
