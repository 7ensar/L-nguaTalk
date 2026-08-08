using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LanguagePractice.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LanguagePractice.Infrastructure.Services;

public class SignalingOptions
{
    public const string SectionName = "Signaling";
    public string PublicUrl { get; set; } = "http://localhost:5050";
}

public class SignalingStatsClient : ISignalingStatsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SignalingStatsClient> _logger;

    public SignalingStatsClient(HttpClient http, ILogger<SignalingStatsClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<SignalingStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await _http.GetFromJsonAsync<SignalingStatsDto>("/stats", cancellationToken);
            if (dto is null)
            {
                return Empty();
            }

            var queued = dto.QueuedByLanguage ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var active = dto.ActiveByLanguage;
            if (active is null || active.Count == 0)
            {
                active = new Dictionary<string, int>(queued, StringComparer.OrdinalIgnoreCase);
                if (dto.InCallByLanguage is not null)
                {
                    foreach (var (lang, count) in dto.InCallByLanguage)
                    {
                        active[lang] = active.GetValueOrDefault(lang) + count;
                    }
                }
            }

            return new SignalingStats(
                dto.Connections,
                dto.Rooms,
                dto.QueuedTotal,
                queued,
                active);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signaling /stats alınamadı; kuyruk 0 gösterilecek.");
            return Empty();
        }
    }

    private static SignalingStats Empty()
        => new(0, 0, 0,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

    private sealed class SignalingStatsDto
    {
        [JsonPropertyName("connections")]
        public int Connections { get; set; }

        [JsonPropertyName("rooms")]
        public int Rooms { get; set; }

        [JsonPropertyName("queuedTotal")]
        public int QueuedTotal { get; set; }

        [JsonPropertyName("queuedByLanguage")]
        public Dictionary<string, int>? QueuedByLanguage { get; set; }

        [JsonPropertyName("inCallByLanguage")]
        public Dictionary<string, int>? InCallByLanguage { get; set; }

        [JsonPropertyName("activeByLanguage")]
        public Dictionary<string, int>? ActiveByLanguage { get; set; }
    }
}
