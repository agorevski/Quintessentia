using Quintessentia.Models;

namespace Quintessentia.Services.Contracts
{
    /// <summary>
    /// Service for downloading, caching, and processing audio files.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Gets or downloads an episode audio file.
        /// </summary>
        /// <param name="episodeId">The episode ID or URL.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The local path to the episode file.</returns>
        Task<string> GetOrDownloadEpisodeAsync(string episodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an episode is cached.
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the episode is cached; otherwise, false.</returns>
        Task<bool> IsEpisodeCachedAsync(string episodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads an episode from a URL.
        /// </summary>
        /// <param name="url">The URL to download from.</param>
        /// <param name="cacheKey">The cache key for storing the episode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The local path to the downloaded episode file.</returns>
        Task<string> DownloadEpisodeAsync(string url, string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the local path for a cached episode.
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <returns>The expected local path for the episode file.</returns>
        string GetCachedEpisodePath(string episodeId);

        /// <summary>
        /// Processes an audio episode through the full AI pipeline: transcription, summarization, and TTS.
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Path to the generated summary MP3 file.</returns>
        Task<string> ProcessAndSummarizeEpisodeAsync(string episodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes an audio episode through the full AI pipeline with progress updates.
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <param name="progressCallback">Callback to report progress updates.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Path to the generated summary MP3 file.</returns>
        Task<string> ProcessAndSummarizeEpisodeAsync(string episodeId, Action<ProcessingStatus>? progressCallback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the path to a cached summary MP3 file.
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <returns>Path to the summary MP3 file.</returns>
        string GetSummaryPath(string episodeId);

        /// <summary>
        /// Checks if a summary MP3 exists in cache.
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if summary exists, false otherwise.</returns>
        Task<bool> IsSummaryCachedAsync(string episodeId, CancellationToken cancellationToken = default);
    }
}
