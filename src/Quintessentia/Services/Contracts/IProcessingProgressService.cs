using Quintessentia.Models;

namespace Quintessentia.Services.Contracts
{
    /// <summary>
    /// Service for managing audio processing with real-time progress updates
    /// </summary>
    public interface IProcessingProgressService
    {
        /// <summary>
        /// Processes an audio episode through the full AI pipeline with progress updates
        /// </summary>
        /// <param name="audioUrl">The URL of the audio file to process</param>
        /// <param name="customSettings">Optional custom Azure OpenAI settings for this request</param>
        /// <param name="onProgress">Callback to report progress updates</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>The final processing result</returns>
        Task<AudioProcessResult> ProcessWithProgressAsync(
            string audioUrl,
            AzureOpenAISettings? customSettings,
            Func<ProcessingStatus, Task> onProgress,
            CancellationToken cancellationToken = default);
    }
}
