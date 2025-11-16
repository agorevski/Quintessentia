using Quintessentia.Models;

namespace Quintessentia.Services.Contracts
{
    public interface IMetadataService
    {
        Task<AudioEpisode?> GetEpisodeMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);
        Task SaveEpisodeMetadataAsync(AudioEpisode episode, CancellationToken cancellationToken = default);
        Task<bool> EpisodeExistsAsync(string cacheKey, CancellationToken cancellationToken = default);
        
        Task<AudioSummary?> GetSummaryMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);
        Task SaveSummaryMetadataAsync(string cacheKey, AudioSummary summary, CancellationToken cancellationToken = default);
        Task<bool> SummaryExistsAsync(string cacheKey, CancellationToken cancellationToken = default);
        
        Task DeleteEpisodeMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);
        Task DeleteSummaryMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);
    }
}
