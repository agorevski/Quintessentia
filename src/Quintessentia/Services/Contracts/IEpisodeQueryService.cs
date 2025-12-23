using Quintessentia.Models;

namespace Quintessentia.Services.Contracts
{
    /// <summary>
    /// Service for querying episode and summary information.
    /// </summary>
    public interface IEpisodeQueryService
    {
        /// <summary>
        /// Gets the complete result for an episode, including summary information if available.
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An AudioProcessResult with episode and summary details.</returns>
        Task<AudioProcessResult> GetResultAsync(string episodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a stream for downloading an episode audio file.
        /// <para>
        /// <b>Important:</b> The caller is responsible for disposing the returned stream.
        /// </para>
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A stream containing the episode audio data. Caller must dispose.</returns>
        Task<Stream> GetEpisodeStreamAsync(string episodeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a stream for downloading a summary audio file.
        /// <para>
        /// <b>Important:</b> The caller is responsible for disposing the returned stream.
        /// </para>
        /// </summary>
        /// <param name="episodeId">The episode ID or cache key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A stream containing the summary audio data. Caller must dispose.</returns>
        Task<Stream> GetSummaryStreamAsync(string episodeId, CancellationToken cancellationToken = default);
    }
}
