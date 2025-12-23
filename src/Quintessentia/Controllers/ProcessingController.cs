using Microsoft.AspNetCore.Mvc;
using Quintessentia.Models;
using Quintessentia.Services;
using Quintessentia.Services.Contracts;
using System.Diagnostics;

namespace Quintessentia.Controllers
{
    /// <summary>
    /// Controller responsible for audio processing operations.
    /// </summary>
    public class ProcessingController : BaseController
    {
        private readonly IAudioService _audioService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly IUrlValidator _urlValidator;
        private readonly ILogger<ProcessingController> _logger;

        public ProcessingController(
            IAudioService audioService,
            ICacheKeyService cacheKeyService,
            IUrlValidator urlValidator,
            ILogger<ProcessingController> logger)
        {
            _audioService = audioService;
            _cacheKeyService = cacheKeyService;
            _urlValidator = urlValidator;
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

                if (!_urlValidator.ValidateUrl(audioUrl, out var errorMessage))
                {
                    return CreateErrorResult(errorMessage ?? "Invalid URL.");
                }

                var cacheKey = _cacheKeyService.GenerateFromUrl(audioUrl);
                var wasCached = await _audioService.IsEpisodeCachedAsync(cacheKey, cancellationToken);
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl, cancellationToken);

                if (string.IsNullOrEmpty(episodePath))
                {
                    return CreateErrorResult("Failed to download or retrieve episode.");
                }

                var result = new AudioProcessResult
                {
                    Success = true,
                    Message = wasCached ? "Episode retrieved from cache" : "Episode downloaded successfully",
                    EpisodeId = cacheKey,
                    FilePath = "cached",
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

                AzureOpenAISettings? customSettings = CreateCustomSettings(
                    settingsEndpoint, settingsKey, settingsWhisperDeployment,
                    settingsGptDeployment, settingsTtsDeployment, settingsTtsSpeedRatio,
                    settingsTtsResponseFormat, settingsEnableAutoplay);

                if (customSettings != null)
                {
                    _logger.LogInformation("Using custom Azure OpenAI settings for this request");
                }

                if (!_urlValidator.ValidateUrl(audioUrl, out var errorMessage))
                {
                    return CreateErrorResult(errorMessage ?? "Invalid URL.");
                }

                var cacheKey = _cacheKeyService.GenerateFromUrl(audioUrl);
                var cancellationToken = HttpContext.RequestAborted;

                var wasCached = await _audioService.IsEpisodeCachedAsync(cacheKey, cancellationToken);
                var summaryWasCached = await _audioService.IsSummaryCachedAsync(cacheKey, cancellationToken);
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl, cancellationToken);

                if (string.IsNullOrEmpty(episodePath))
                {
                    return CreateErrorResult("Failed to download or retrieve episode.");
                }

                _logger.LogInformation("Starting AI processing pipeline for episode: {CacheKey}", cacheKey);
                var summaryAudioPath = await _audioService.ProcessAndSummarizeEpisodeAsync(cacheKey, cancellationToken);

                stopwatch.Stop();

                var (summaryText, transcriptWordCount, summaryWordCount) = await LoadProcessingResultsAsync(episodePath, cacheKey);

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

        private static AzureOpenAISettings? CreateCustomSettings(
            string? endpoint, string? key, string? whisperDeployment,
            string? gptDeployment, string? ttsDeployment, float? ttsSpeedRatio,
            string? ttsResponseFormat, bool? enableAutoplay)
        {
            if (!string.IsNullOrWhiteSpace(endpoint) ||
                !string.IsNullOrWhiteSpace(key) ||
                !string.IsNullOrWhiteSpace(whisperDeployment) ||
                !string.IsNullOrWhiteSpace(gptDeployment) ||
                !string.IsNullOrWhiteSpace(ttsDeployment) ||
                ttsSpeedRatio.HasValue ||
                !string.IsNullOrWhiteSpace(ttsResponseFormat) ||
                enableAutoplay.HasValue)
            {
                return new AzureOpenAISettings
                {
                    Endpoint = endpoint,
                    Key = key,
                    WhisperDeployment = whisperDeployment,
                    GptDeployment = gptDeployment,
                    TtsDeployment = ttsDeployment,
                    TtsSpeedRatio = ttsSpeedRatio,
                    TtsResponseFormat = ttsResponseFormat,
                    EnableAutoplay = enableAutoplay
                };
            }
            return null;
        }

        private static async Task<(string? summaryText, int? transcriptWordCount, int? summaryWordCount)> LoadProcessingResultsAsync(string episodePath, string cacheKey)
        {
            var transcriptPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_transcript.txt");
            var summaryTextPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_summary.txt");

            string? summaryText = null;
            int? transcriptWordCount = null;
            int? summaryWordCount = null;

            if (System.IO.File.Exists(summaryTextPath))
            {
                summaryText = Utilities.TextHelper.TrimNonAlphanumeric(await System.IO.File.ReadAllTextAsync(summaryTextPath));
                summaryWordCount = summaryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            }

            if (System.IO.File.Exists(transcriptPath))
            {
                var transcript = await System.IO.File.ReadAllTextAsync(transcriptPath);
                transcriptWordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            }

            return (summaryText, transcriptWordCount, summaryWordCount);
        }
    }
}
