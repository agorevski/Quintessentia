using Quintessentia.Models;

namespace Quintessentia.Services.Contracts
{
    public interface IAudioService
    {
        Task<string> GetOrDownloadEpisodeAsync(string episodeId);
        bool IsEpisodeCached(string episodeId);
        Task<string> DownloadEpisodeAsync(string url, string cacheKey);
        string GetCachedEpisodePath(string episodeId);

        /// <summary>
        /// Processes an audio episode through the full AI pipeline: transcription, summarization, and TTS
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key</param>
        /// <returns>Path to the generated summary MP3 file</returns>
        Task<string> ProcessAndSummarizeEpisodeAsync(string episodeId);

        /// <summary>
        /// Processes an audio episode through the full AI pipeline with progress updates
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key</param>
        /// <param name="progressCallback">Callback to report progress updates</param>
        /// <returns>Path to the generated summary MP3 file</returns>
        Task<string> ProcessAndSummarizeEpisodeAsync(string episodeId, Action<ProcessingStatus> progressCallback);

        /// <summary>
        /// Gets the path to a cached summary MP3 file
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key</param>
        /// <returns>Path to the summary MP3 file</returns>
        string GetSummaryPath(string episodeId);

        /// <summary>
        /// Checks if a summary MP3 exists in cache
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key</param>
        /// <returns>True if summary exists, false otherwise</returns>
        bool IsSummaryCached(string episodeId);
    }
}
