using Quintessentia.Models;

namespace Quintessentia.Services.Contracts
{
    /// <summary>
    /// Service for querying episode and summary information
    /// </summary>
    public interface IEpisodeQueryService
    {
        /// <summary>
        /// Gets the complete result for an episode, including summary information if available
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key</param>
        /// <returns>An AudioProcessResult with episode and summary details</returns>
        Task<AudioProcessResult> GetResultAsync(string episodeId);

        /// <summary>
        /// Gets a stream for downloading an episode audio file
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key</param>
        /// <returns>A stream containing the episode audio data</returns>
        Task<Stream> GetEpisodeStreamAsync(string episodeId);

        /// <summary>
        /// Gets a stream for downloading a summary audio file
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key</param>
        /// <returns>A stream containing the summary audio data</returns>
        Task<Stream> GetSummaryStreamAsync(string episodeId);
    }
}
