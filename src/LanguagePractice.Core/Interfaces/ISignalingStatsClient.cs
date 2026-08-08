namespace LanguagePractice.Core.Interfaces;

public record SignalingStats(
    int ActiveConnections,
    int ActiveRooms,
    int QueuedTotal,
    IReadOnlyDictionary<string, int> QueuedByLanguage,
    IReadOnlyDictionary<string, int> ActiveByLanguage);

public interface ISignalingStatsClient
{
    Task<SignalingStats> GetStatsAsync(CancellationToken cancellationToken = default);
}
