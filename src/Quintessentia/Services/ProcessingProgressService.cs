using System.Diagnostics;
using Quintessentia.Models;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    public class ProcessingProgressService : IProcessingProgressService
    {
        private readonly IAudioService _audioService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly ILogger<ProcessingProgressService> _logger;

        public ProcessingProgressService(
            IAudioService audioService,
            ICacheKeyService cacheKeyService,
            ILogger<ProcessingProgressService> logger)
        {
            _audioService = audioService;
            _cacheKeyService = cacheKeyService;
            _logger = logger;
        }

        public async Task<AudioProcessResult> ProcessWithProgressAsync(
            string audioUrl,
            AzureOpenAISettings? customSettings,
            Func<ProcessingStatus, Task> onProgress,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate URL
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    await onProgress(new ProcessingStatus
                    {
                        Stage = "error",
                        Message = "MP3 URL is required",
                        IsError = true,
                        ErrorMessage = "MP3 URL is required"
                    });
                    throw new ArgumentException("MP3 URL is required.", nameof(audioUrl));
                }

                if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    await onProgress(new ProcessingStatus
                    {
                        Stage = "error",
                        Message = "Invalid URL format",
                        IsError = true,
                        ErrorMessage = "Invalid URL format. Please provide a valid HTTP or HTTPS URL."
                    });
                    throw new ArgumentException("Invalid URL format.", nameof(audioUrl));
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Generate cache key
                var cacheKey = _cacheKeyService.GenerateFromUrl(audioUrl);

                // Check if already cached
                var wasCached = _audioService.IsEpisodeCached(cacheKey);
                var summaryWasCached = _audioService.IsSummaryCached(cacheKey);

                // Send initial status
                await onProgress(new ProcessingStatus
                {
                    Stage = "downloading",
                    Message = wasCached ? "Retrieving episode from cache..." : "Downloading episode...",
                    Progress = 10,
                    EpisodeId = cacheKey,
                    WasCached = wasCached
                });

                cancellationToken.ThrowIfCancellationRequested();

                // Download or retrieve cached episode
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl);

                if (string.IsNullOrEmpty(episodePath))
                {
                    await onProgress(new ProcessingStatus
                    {
                        Stage = "error",
                        Message = "Failed to download episode",
                        IsError = true,
                        ErrorMessage = "Failed to download or retrieve episode"
                    });
                    throw new InvalidOperationException("Failed to download or retrieve episode");
                }

                await onProgress(new ProcessingStatus
                {
                    Stage = "downloaded",
                    Message = wasCached ? "Episode retrieved from cache" : "Episode downloaded",
                    Progress = 20,
                    EpisodeId = cacheKey,
                    FilePath = episodePath,
                    WasCached = wasCached
                });

                cancellationToken.ThrowIfCancellationRequested();

                // Process through AI pipeline with progress updates
                _logger.LogInformation("Starting AI processing pipeline for episode: {CacheKey}", cacheKey);

                var summaryAudioPath = await _audioService.ProcessAndSummarizeEpisodeAsync(cacheKey, async (status) =>
                {
                    await onProgress(status);
                });

                stopwatch.Stop();

                cancellationToken.ThrowIfCancellationRequested();

                // Load transcript and summary text for final status
                var transcriptPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_transcript.txt");
                var summaryTextPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_summary.txt");

                string? summaryText = null;
                int? transcriptWordCount = null;
                int? summaryWordCount = null;

                if (File.Exists(summaryTextPath))
                {
                    summaryText = TrimNonAlphanumeric(await File.ReadAllTextAsync(summaryTextPath, cancellationToken));
                    summaryWordCount = summaryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                if (File.Exists(transcriptPath))
                {
                    var transcript = await File.ReadAllTextAsync(transcriptPath, cancellationToken);
                    transcriptWordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                // Build final result
                var result = new AudioProcessResult
                {
                    Success = true,
                    Message = summaryWasCached ? "Summary retrieved from cache" : "Episode processed and summarized successfully",
                    EpisodeId = cacheKey,
                    FilePath = "cached",
                    WasCached = wasCached,
                    SummaryAudioPath = summaryAudioPath,
                    SummaryWasCached = summaryWasCached,
                    SummaryText = summaryText,
                    ProcessingDuration = stopwatch.Elapsed,
                    TranscriptWordCount = transcriptWordCount,
                    SummaryWordCount = summaryWordCount
                };

                // Send final completion status
                await onProgress(new ProcessingStatus
                {
                    Stage = "complete",
                    Message = "Processing complete!",
                    Progress = 100,
                    IsComplete = true,
                    EpisodeId = cacheKey,
                    FilePath = episodePath,
                    WasCached = wasCached,
                    SummaryAudioPath = summaryAudioPath,
                    TranscriptWordCount = transcriptWordCount,
                    SummaryWordCount = summaryWordCount,
                    SummaryText = summaryText,
                    ProcessingDuration = stopwatch.Elapsed
                });

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Processing was cancelled");
                await onProgress(new ProcessingStatus
                {
                    Stage = "error",
                    Message = "Processing was cancelled",
                    IsError = true,
                    ErrorMessage = "Processing was cancelled by user"
                });
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in processing pipeline");
                await onProgress(new ProcessingStatus
                {
                    Stage = "error",
                    Message = "Processing failed",
                    IsError = true,
                    ErrorMessage = ex.Message
                });
                throw;
            }
        }

        private string TrimNonAlphanumeric(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Find first alphanumeric character from the start
            int start = 0;
            while (start < text.Length && !char.IsLetterOrDigit(text[start]))
            {
                start++;
            }

            // Find last alphanumeric character from the end
            int end = text.Length - 1;
            while (end >= start && !char.IsLetterOrDigit(text[end]))
            {
                end--;
            }

            // Return trimmed substring
            return start <= end ? text.Substring(start, end - start + 1) : string.Empty;
        }
    }
}
