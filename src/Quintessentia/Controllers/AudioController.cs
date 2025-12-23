using Microsoft.AspNetCore.Mvc;
using Quintessentia.Constants;
using Quintessentia.Models;
using Quintessentia.Services;
using Quintessentia.Services.Contracts;
using Quintessentia.Utilities;
using System.Diagnostics;
using System.Text.Json;
using System.Text;

namespace Quintessentia.Controllers
{
    public class AudioController : Controller
    {
        private readonly IAudioService _audioService;
        private readonly IEpisodeQueryService _episodeQueryService;
        private readonly IProcessingProgressService _progressService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly IUrlValidator _urlValidator;
        private readonly JsonOptionsService _jsonOptions;
        private readonly ILogger<AudioController> _logger;

        public AudioController(
            IAudioService audioService,
            IEpisodeQueryService episodeQueryService,
            IProcessingProgressService progressService,
            ICacheKeyService cacheKeyService,
            IUrlValidator urlValidator,
            JsonOptionsService jsonOptions,
            ILogger<AudioController> logger)
        {
            _audioService = audioService;
            _episodeQueryService = episodeQueryService;
            _progressService = progressService;
            _cacheKeyService = cacheKeyService;
            _urlValidator = urlValidator;
            _jsonOptions = jsonOptions;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Process(string audioUrl, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    return CreateErrorResult("MP3 URL is required.");
                }

                // Validate URL format and security
                if (!_urlValidator.ValidateUrl(audioUrl, out var errorMessage))
                {
                    return CreateErrorResult(errorMessage ?? "Invalid URL.");
                }

                // Generate cache key from URL
                var cacheKey = _cacheKeyService.GenerateFromUrl(audioUrl);

                // Check if already cached before attempting download
                var wasCached = await _audioService.IsEpisodeCachedAsync(cacheKey, cancellationToken);

                // Download or retrieve cached episode (pass the full URL as the "episodeId")
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl, cancellationToken);

                if (string.IsNullOrEmpty(episodePath))
                {
                    return CreateErrorResult("Failed to download or retrieve episode.");
                }

                // Return success without exposing file path
                var result = new AudioProcessResult
                {
                    Success = true,
                    Message = wasCached ? "Episode retrieved from cache" : "Episode downloaded successfully",
                    EpisodeId = cacheKey,
                    FilePath = "cached", // Don't expose actual file path
                    WasCached = wasCached
                };

                return View("Result", result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error downloading audio from URL: {Url}", audioUrl);
                return CreateErrorResult("Failed to download the audio. Please check the URL and try again.");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error processing audio URL: {Url}", audioUrl);
                return CreateErrorResult("An error occurred while processing your request.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation error processing audio URL: {Url}", audioUrl);
                return CreateErrorResult("An error occurred while processing your request.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Download(string episodeId, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = await _episodeQueryService.GetEpisodeStreamAsync(episodeId, cancellationToken);
                return File(stream, "audio/mpeg", $"{episodeId}.mp3");
            }
            catch (FileNotFoundException)
            {
                return NotFound("Episode not found.");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error downloading episode: {EpisodeId}", episodeId);
                return NotFound("Episode not found.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessAndSummarize(
            string audioUrl,
            string? settingsEndpoint = null,
            string? settingsKey = null,
            string? settingsWhisperDeployment = null,
            string? settingsGptDeployment = null,
            string? settingsTtsDeployment = null,
            float? settingsTtsSpeedRatio = null,
            string? settingsTtsResponseFormat = null,
            bool? settingsEnableAutoplay = null)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    return CreateErrorResult("MP3 URL is required.");
                }

                // Create settings object if any overrides are provided
                AzureOpenAISettings? customSettings = null;
                if (!string.IsNullOrWhiteSpace(settingsEndpoint) ||
                    !string.IsNullOrWhiteSpace(settingsKey) ||
                    !string.IsNullOrWhiteSpace(settingsWhisperDeployment) ||
                    !string.IsNullOrWhiteSpace(settingsGptDeployment) ||
                    !string.IsNullOrWhiteSpace(settingsTtsDeployment) ||
                    settingsTtsSpeedRatio.HasValue ||
                    !string.IsNullOrWhiteSpace(settingsTtsResponseFormat) ||
                    settingsEnableAutoplay.HasValue)
                {
                    customSettings = new AzureOpenAISettings
                    {
                        Endpoint = settingsEndpoint,
                        Key = settingsKey,
                        WhisperDeployment = settingsWhisperDeployment,
                        GptDeployment = settingsGptDeployment,
                        TtsDeployment = settingsTtsDeployment,
                        TtsSpeedRatio = settingsTtsSpeedRatio,
                        TtsResponseFormat = settingsTtsResponseFormat,
                        EnableAutoplay = settingsEnableAutoplay
                    };
                    _logger.LogInformation("Using custom Azure OpenAI settings for this request");
                }

                // Validate URL format and security
                if (!_urlValidator.ValidateUrl(audioUrl, out var errorMessage))
                {
                    return CreateErrorResult(errorMessage ?? "Invalid URL.");
                }

                // Generate cache key from URL
                var cacheKey = _cacheKeyService.GenerateFromUrl(audioUrl);
                var cancellationToken = HttpContext.RequestAborted;

                // Check if already cached before attempting download
                var wasCached = await _audioService.IsEpisodeCachedAsync(cacheKey, cancellationToken);
                var summaryWasCached = await _audioService.IsSummaryCachedAsync(cacheKey, cancellationToken);

                // Download or retrieve cached episode (pass the full URL as the "episodeId")
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl, cancellationToken);

                if (string.IsNullOrEmpty(episodePath))
                {
                    return CreateErrorResult("Failed to download or retrieve episode.");
                }

                // Process through AI pipeline (transcription, summarization, TTS)
                // Pass custom settings explicitly through the service method
                _logger.LogInformation("Starting AI processing pipeline for episode: {CacheKey}", cacheKey);
                var summaryAudioPath = await _audioService.ProcessAndSummarizeEpisodeAsync(cacheKey, cancellationToken);

                stopwatch.Stop();

                // Load transcript and summary text for display
                var transcriptPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_transcript.txt");
                var summaryTextPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_summary.txt");

                string? summaryText = null;
                int? transcriptWordCount = null;
                int? summaryWordCount = null;

                if (System.IO.File.Exists(summaryTextPath))
                {
                    summaryText = TextHelper.TrimNonAlphanumeric(await System.IO.File.ReadAllTextAsync(summaryTextPath));
                    summaryWordCount = summaryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                if (System.IO.File.Exists(transcriptPath))
                {
                    var transcript = await System.IO.File.ReadAllTextAsync(transcriptPath);
                    transcriptWordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                // Return success with comprehensive result
                var result = new AudioProcessResult
                {
                    Success = true,
                    Message = summaryWasCached ? "Summary retrieved from cache" : "Episode processed and summarized successfully",
                    EpisodeId = cacheKey,
                    FilePath = "cached", // Don't expose actual file path
                    WasCached = wasCached,
                    SummaryAudioPath = summaryAudioPath,
                    SummaryWasCached = summaryWasCached,
                    SummaryText = summaryText,
                    ProcessingDuration = stopwatch.Elapsed,
                    TranscriptWordCount = transcriptWordCount,
                    SummaryWordCount = summaryWordCount
                };

                return View("Result", result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error downloading audio from URL: {Url}", audioUrl);
                return CreateErrorResult("Failed to download the audio. Please check the URL and try again.");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error processing audio URL: {Url}", audioUrl);
                return CreateErrorResult($"An error occurred while processing your request: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation error processing audio URL: {Url}", audioUrl);
                return CreateErrorResult($"An error occurred while processing your request: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Result(string episodeId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _episodeQueryService.GetResultAsync(episodeId, cancellationToken);
                return View(result);
            }
            catch (FileNotFoundException)
            {
                return CreateErrorResult("Episode not found.", StatusCodes.Status404NotFound);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error loading result for episode: {EpisodeId}", episodeId);
                return CreateErrorResult("An error occurred while loading the result.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation error loading result for episode: {EpisodeId}", episodeId);
                return CreateErrorResult("An error occurred while loading the result.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSummary(string episodeId, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = await _episodeQueryService.GetSummaryStreamAsync(episodeId, cancellationToken);
                return File(stream, "audio/mpeg", $"{episodeId}_summary.mp3");
            }
            catch (FileNotFoundException)
            {
                return CreateErrorResult("Summary not found.", StatusCodes.Status404NotFound);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error downloading summary: {EpisodeId}", episodeId);
                return CreateErrorResult("Summary not found.", StatusCodes.Status404NotFound);
            }
        }

        [HttpGet]
        public async Task ProcessAndSummarizeStream(
            string audioUrl,
            string? settingsEndpoint = null,
            string? settingsKey = null,
            string? settingsWhisperDeployment = null,
            string? settingsGptDeployment = null,
            string? settingsTtsDeployment = null,
            float? settingsTtsSpeedRatio = null,
            string? settingsTtsResponseFormat = null,
            bool? settingsEnableAutoplay = null)
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    await SendStatusUpdate(ProcessingStatus.CreateError("MP3 URL is required"));
                    return;
                }

                // Create settings object if any overrides are provided
                AzureOpenAISettings? customSettings = null;
                if (!string.IsNullOrWhiteSpace(settingsEndpoint) ||
                    !string.IsNullOrWhiteSpace(settingsKey) ||
                    !string.IsNullOrWhiteSpace(settingsWhisperDeployment) ||
                    !string.IsNullOrWhiteSpace(settingsGptDeployment) ||
                    !string.IsNullOrWhiteSpace(settingsTtsDeployment) ||
                    settingsTtsSpeedRatio.HasValue ||
                    !string.IsNullOrWhiteSpace(settingsTtsResponseFormat) ||
                    settingsEnableAutoplay.HasValue)
                {
                    customSettings = new AzureOpenAISettings
                    {
                        Endpoint = settingsEndpoint,
                        Key = settingsKey,
                        WhisperDeployment = settingsWhisperDeployment,
                        GptDeployment = settingsGptDeployment,
                        TtsDeployment = settingsTtsDeployment,
                        TtsSpeedRatio = settingsTtsSpeedRatio,
                        TtsResponseFormat = settingsTtsResponseFormat,
                        EnableAutoplay = settingsEnableAutoplay
                    };
                    _logger.LogInformation("Using custom Azure OpenAI settings for streaming request");
                }

                // Validate URL format and security
                if (!_urlValidator.ValidateUrl(audioUrl, out var errorMessage))
                {
                    await SendStatusUpdate(ProcessingStatus.CreateError("Invalid URL", errorMessage));
                    return;
                }

                // Generate cache key from URL
                var cacheKey = _cacheKeyService.GenerateFromUrl(audioUrl);

                // Get cancellation token
                var cancellationToken = HttpContext.RequestAborted;

                // Check if already cached
                var wasCached = await _audioService.IsEpisodeCachedAsync(cacheKey, cancellationToken);
                var summaryWasCached = await _audioService.IsSummaryCachedAsync(cacheKey, cancellationToken);

                // Send initial status
                await SendStatusUpdate(new ProcessingStatus
                {
                    StageEnum = ProcessingStage.Downloading,
                    Message = wasCached ? "Retrieving episode from cache..." : "Downloading episode...",
                    Progress = ProcessingProgress.Downloading,
                    EpisodeId = cacheKey,
                    WasCached = wasCached
                });

                // Download or retrieve cached episode
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl, cancellationToken);

                if (string.IsNullOrEmpty(episodePath))
                {
                    await SendStatusUpdate(ProcessingStatus.CreateError("Failed to download episode", "Failed to download or retrieve episode"));
                    return;
                }

                await SendStatusUpdate(new ProcessingStatus
                {
                    StageEnum = ProcessingStage.Downloaded,
                    Message = wasCached ? "Episode retrieved from cache" : "Episode downloaded",
                    Progress = ProcessingProgress.Downloaded,
                    EpisodeId = cacheKey,
                    FilePath = episodePath,
                    WasCached = wasCached
                });

                // Process through AI pipeline with progress updates
                _logger.LogInformation("Starting AI processing pipeline for episode: {CacheKey}", cacheKey);
                
                var summaryAudioPath = await _audioService.ProcessAndSummarizeEpisodeAsync(cacheKey, async (status) =>
                {
                    await SendStatusUpdate(status);
                }, cancellationToken);

                stopwatch.Stop();

                // Load transcript and summary text for final status
                var transcriptPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_transcript.txt");
                var summaryTextPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_summary.txt");

                string? summaryText = null;
                int? transcriptWordCount = null;
                int? summaryWordCount = null;

                if (System.IO.File.Exists(summaryTextPath))
                {
                    summaryText = TextHelper.TrimNonAlphanumeric(await System.IO.File.ReadAllTextAsync(summaryTextPath, cancellationToken));
                    summaryWordCount = summaryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                if (System.IO.File.Exists(transcriptPath))
                {
                    var transcript = await System.IO.File.ReadAllTextAsync(transcriptPath, cancellationToken);
                    transcriptWordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                // Send final completion status
                await SendStatusUpdate(new ProcessingStatus
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
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Streaming processing was cancelled by client disconnect");
                await SendStatusUpdate(ProcessingStatus.CreateError("Processing was cancelled"));
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error in streaming processing pipeline");
                await SendStatusUpdate(ProcessingStatus.CreateError("Processing failed", ex.Message));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error in streaming processing pipeline");
                await SendStatusUpdate(ProcessingStatus.CreateError("Processing failed", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation error in streaming processing pipeline");
                await SendStatusUpdate(ProcessingStatus.CreateError("Processing failed", ex.Message));
            }
        }

        private async Task SendStatusUpdate(ProcessingStatus status)
        {
            var json = JsonSerializer.Serialize(status, _jsonOptions.Options);
            
            var data = $"data: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(data);
            
            await Response.Body.WriteAsync(bytes);
            await Response.Body.FlushAsync();
        }

        /// <summary>
        /// Creates a consistent error result for the controller.
        /// </summary>
        private IActionResult CreateErrorResult(string message, int statusCode = StatusCodes.Status400BadRequest)
        {
            Response.StatusCode = statusCode;
            return View("Error", new ErrorViewModel { Message = message });
        }

    }
}
