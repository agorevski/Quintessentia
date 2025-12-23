using Microsoft.AspNetCore.Mvc;
using Quintessentia.Constants;
using Quintessentia.Models;
using Quintessentia.Services;
using Quintessentia.Services.Contracts;
using Quintessentia.Utilities;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Quintessentia.Controllers
{
    /// <summary>
    /// Controller responsible for Server-Sent Events streaming operations.
    /// </summary>
    public class StreamController : Controller
    {
        private readonly IAudioService _audioService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly IUrlValidator _urlValidator;
        private readonly JsonOptionsService _jsonOptions;
        private readonly ILogger<StreamController> _logger;

        public StreamController(
            IAudioService audioService,
            ICacheKeyService cacheKeyService,
            IUrlValidator urlValidator,
            JsonOptionsService jsonOptions,
            ILogger<StreamController> logger)
        {
            _audioService = audioService;
            _cacheKeyService = cacheKeyService;
            _urlValidator = urlValidator;
            _jsonOptions = jsonOptions;
            _logger = logger;
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

                AzureOpenAISettings? customSettings = CreateCustomSettings(
                    settingsEndpoint, settingsKey, settingsWhisperDeployment,
                    settingsGptDeployment, settingsTtsDeployment, settingsTtsSpeedRatio,
                    settingsTtsResponseFormat, settingsEnableAutoplay);

                if (customSettings != null)
                {
                    _logger.LogInformation("Using custom Azure OpenAI settings for streaming request");
                }

                if (!_urlValidator.ValidateUrl(audioUrl, out var errorMessage))
                {
                    await SendStatusUpdate(ProcessingStatus.CreateError("Invalid URL", errorMessage));
                    return;
                }

                var cacheKey = _cacheKeyService.GenerateFromUrl(audioUrl);
                var cancellationToken = HttpContext.RequestAborted;

                var wasCached = await _audioService.IsEpisodeCachedAsync(cacheKey, cancellationToken);
                var summaryWasCached = await _audioService.IsSummaryCachedAsync(cacheKey, cancellationToken);

                await SendStatusUpdate(new ProcessingStatus
                {
                    StageEnum = ProcessingStage.Downloading,
                    Message = wasCached ? "Retrieving episode from cache..." : "Downloading episode...",
                    Progress = ProcessingProgress.Downloading,
                    EpisodeId = cacheKey,
                    WasCached = wasCached
                });

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

                _logger.LogInformation("Starting AI processing pipeline for episode: {CacheKey}", cacheKey);

                var summaryAudioPath = await _audioService.ProcessAndSummarizeEpisodeAsync(cacheKey, async (status) =>
                {
                    await SendStatusUpdate(status);
                }, cancellationToken);

                stopwatch.Stop();

                var (summaryText, transcriptWordCount, summaryWordCount) = await LoadProcessingResultsAsync(episodePath, cacheKey, cancellationToken);

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

        private static async Task<(string? summaryText, int? transcriptWordCount, int? summaryWordCount)> LoadProcessingResultsAsync(
            string episodePath, string cacheKey, CancellationToken cancellationToken)
        {
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

            return (summaryText, transcriptWordCount, summaryWordCount);
        }
    }
}
