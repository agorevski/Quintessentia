using System.Diagnostics;
using Quintessentia.Constants;
using Quintessentia.Models;
using Quintessentia.Services.Contracts;
using Quintessentia.Utilities;

namespace Quintessentia.Services
{
    /// <summary>
    /// Service for managing audio processing with real-time progress updates.
    /// </summary>
    public class ProcessingProgressService : IProcessingProgressService
    {
        private readonly IAudioService _audioService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly IUrlValidator _urlValidator;
        private readonly ILogger<ProcessingProgressService> _logger;

        public ProcessingProgressService(
            IAudioService audioService,
            ICacheKeyService cacheKeyService,
            IUrlValidator urlValidator,
            ILogger<ProcessingProgressService> logger)
        {
            _audioService = audioService;
            _cacheKeyService = cacheKeyService;
            _urlValidator = urlValidator;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<AudioProcessResult> ProcessWithProgressAsync(
            string audioUrl,
            AzureOpenAISettings? customSettings,
            Func<ProcessingStatus, Task> onProgress,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate URL using centralized validator
                if (!_urlValidator.ValidateUrl(audioUrl, out var errorMessage))
                {
                    await onProgress(ProcessingStatus.CreateError("Invalid URL", errorMessage));
                    throw new ArgumentException(errorMessage ?? "Invalid URL.", nameof(audioUrl));
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Generate cache key
                var cacheKey = _cacheKeyService.GenerateFromUrl(audioUrl);

                // Check if already cached
                var wasCached = await _audioService.IsEpisodeCachedAsync(cacheKey, cancellationToken);
                var summaryWasCached = await _audioService.IsSummaryCachedAsync(cacheKey, cancellationToken);

                // Send initial status
                await onProgress(new ProcessingStatus
                {
                    StageEnum = ProcessingStage.Downloading,
                    Message = wasCached ? "Retrieving episode from cache..." : "Downloading episode...",
                    Progress = ProcessingProgress.Downloading,
                    EpisodeId = cacheKey,
                    WasCached = wasCached
                });

                cancellationToken.ThrowIfCancellationRequested();

                // Download or retrieve cached episode
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl, cancellationToken);

                if (string.IsNullOrEmpty(episodePath))
                {
                    await onProgress(ProcessingStatus.CreateError("Failed to download episode", "Failed to download or retrieve episode"));
                    throw new InvalidOperationException("Failed to download or retrieve episode");
                }

                await onProgress(new ProcessingStatus
                {
                    StageEnum = ProcessingStage.Downloaded,
                    Message = wasCached ? "Episode retrieved from cache" : "Episode downloaded",
                    Progress = ProcessingProgress.Downloaded,
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
                }, cancellationToken);

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
                    summaryText = TextHelper.TrimNonAlphanumeric(await File.ReadAllTextAsync(summaryTextPath, cancellationToken));
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
                    StageEnum = ProcessingStage.Complete,
                    Message = "Processing complete!",
                    Progress = ProcessingProgress.Complete,
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
                await onProgress(ProcessingStatus.CreateError("Processing was cancelled", "Processing was cancelled by user"));
                throw;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid argument in processing pipeline");
                await onProgress(ProcessingStatus.CreateError("Processing failed", ex.Message));
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in processing pipeline");
                await onProgress(ProcessingStatus.CreateError("Processing failed", ex.Message));
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error in processing pipeline");
                await onProgress(ProcessingStatus.CreateError("Processing failed", ex.Message));
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error in processing pipeline");
                await onProgress(ProcessingStatus.CreateError("Processing failed", ex.Message));
                throw;
            }
        }
    }
}
