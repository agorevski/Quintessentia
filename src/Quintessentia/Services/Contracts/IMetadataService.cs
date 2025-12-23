using Quintessentia.Models;

namespace Quintessentia.Services.Contracts
{
    /// <summary>
    /// Service for managing episode and summary metadata.
    /// </summary>
    public interface IMetadataService
    {
        /// <summary>
        /// Gets episode metadata for the specified cache key.
        /// </summary>
        /// <param name="cacheKey">The unique cache key for the episode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The episode metadata, or null if not found.</returns>
        Task<AudioEpisode?> GetEpisodeMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves episode metadata.
        /// </summary>
        /// <param name="episode">The episode metadata to save.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveEpisodeMetadataAsync(AudioEpisode episode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if episode metadata exists for the specified cache key.
        /// </summary>
        /// <param name="cacheKey">The unique cache key for the episode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the episode exists; otherwise, false.</returns>
        Task<bool> EpisodeExistsAsync(string cacheKey, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets summary metadata for the specified cache key.
        /// </summary>
        /// <param name="cacheKey">The unique cache key for the episode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The summary metadata, or null if not found.</returns>
        Task<AudioSummary?> GetSummaryMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves summary metadata for an episode.
        /// </summary>
        /// <param name="cacheKey">The unique cache key for the episode.</param>
        /// <param name="summary">The summary metadata to save.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveSummaryMetadataAsync(string cacheKey, AudioSummary summary, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if summary metadata exists for the specified cache key.
        /// </summary>
        /// <param name="cacheKey">The unique cache key for the episode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the summary exists; otherwise, false.</returns>
        Task<bool> SummaryExistsAsync(string cacheKey, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes episode metadata for the specified cache key.
        /// </summary>
        /// <param name="cacheKey">The unique cache key for the episode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteEpisodeMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes summary metadata for the specified cache key.
        /// </summary>
        /// <param name="cacheKey">The unique cache key for the episode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteSummaryMetadataAsync(string cacheKey, CancellationToken cancellationToken = default);
    }
}
