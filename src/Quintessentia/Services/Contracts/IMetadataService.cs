using Quintessentia.Models;

namespace Quintessentia.Services.Contracts
{
    public interface IMetadataService
    {
        Task<AudioEpisode?> GetEpisodeMetadataAsync(string cacheKey);
        Task SaveEpisodeMetadataAsync(AudioEpisode episode);
        Task<bool> EpisodeExistsAsync(string cacheKey);
        
        Task<AudioSummary?> GetSummaryMetadataAsync(string cacheKey);
        Task SaveSummaryMetadataAsync(string cacheKey, AudioSummary summary);
        Task<bool> SummaryExistsAsync(string cacheKey);
        
        Task DeleteEpisodeMetadataAsync(string cacheKey);
        Task DeleteSummaryMetadataAsync(string cacheKey);
    }
}
