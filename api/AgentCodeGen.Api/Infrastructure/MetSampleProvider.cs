using System.Text.Json;
using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Infrastructure;

public sealed class MetSampleProvider(HttpClient httpClient, ILogger<MetSampleProvider> logger) : IGroundingProvider
{
    private const string BaseUrl = "https://collectionapi.metmuseum.org/public/collection/v1";

    // Only structural fields the generated code will actually read. Free-text
    // fields (descriptions, tags, constituents) are dropped: they are the
    // classic prompt-injection carrier and the code doesn't need them.
    private static readonly string[] FieldAllowlist =
    [
        "objectID", "title", "artistDisplayName", "objectDate", "department",
        "primaryImage", "primaryImageSmall", "objectURL", "isPublicDomain",
    ];

    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    private string? _cached;

    public async Task<Option<string>> GetMetSampleAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return Option<string>.Some(_cached);
        }

        await _fetchLock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null)
            {
                return Option<string>.Some(_cached);
            }

            var sample = await FetchSampleAsync(cancellationToken);
            if (sample is not null)
            {
                _cached = sample;
                return Option<string>.Some(sample);
            }

            return Option<string>.None;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not fetch a Met API sample; generating ungrounded");
            return Option<string>.None;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<string?> FetchSampleAsync(CancellationToken cancellationToken)
    {
        using var searchResponse = await httpClient.GetAsync(
            $"{BaseUrl}/search?hasImages=true&q=sunflowers", cancellationToken);
        if (!searchResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var search = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync(cancellationToken));
        if (!search.RootElement.TryGetProperty("objectIDs", out var ids) ||
            ids.ValueKind != JsonValueKind.Array ||
            ids.GetArrayLength() == 0)
        {
            return null;
        }

        var objectId = ids[0].GetInt32();
        using var objectResponse = await httpClient.GetAsync($"{BaseUrl}/objects/{objectId}", cancellationToken);
        if (!objectResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var artwork = JsonDocument.Parse(await objectResponse.Content.ReadAsStringAsync(cancellationToken));

        var trimmed = new Dictionary<string, JsonElement>();
        foreach (var field in FieldAllowlist)
        {
            if (artwork.RootElement.TryGetProperty(field, out var value))
            {
                trimmed[field] = value.Clone();
            }
        }

        return JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = true });
    }
}
