using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quintessentia.Data;
using Quintessentia.Models;
using Quintessentia.Services;
using System.Diagnostics;
using System.Text.Json;
using System.Text;

namespace Quintessentia.Controllers
{
    public class AudioController : Controller
    {
        private readonly IAudioService _audioService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AudioController> _logger;
        private readonly IConfiguration _configuration;

        public AudioController(
            IAudioService audioService,
            IBlobStorageService blobStorageService,
            ApplicationDbContext dbContext,
            ILogger<AudioController> logger,
            IConfiguration configuration)
        {
            _audioService = audioService;
            _blobStorageService = blobStorageService;
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
        }

        private string GetContainerName(string containerType)
        {
            return _configuration[$"AzureStorage:Containers:{containerType}"] ?? containerType.ToLower();
        }

        [HttpPost]
        public async Task<IActionResult> Process(string audioUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    return BadRequest("MP3 URL is required.");
                }

                // Validate URL format
                if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return BadRequest("Invalid URL format. Please provide a valid HTTP or HTTPS URL.");
                }

                // Generate cache key from URL
                var cacheKey = GenerateCacheKey(audioUrl);

                // Check if already cached before attempting download
                var wasCached = _audioService.IsEpisodeCached(cacheKey);

                // Download or retrieve cached episode (pass the full URL as the "episodeId")
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl);

                if (string.IsNullOrEmpty(episodePath))
                {
                    return BadRequest("Failed to download or retrieve episode.");
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
                return View("Error", new ErrorViewModel { Message = "Failed to download the audio. Please check the URL and try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio URL: {Url}", audioUrl);
                return View("Error", new ErrorViewModel { Message = "An error occurred while processing your request." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Download(string episodeId)
        {
            try
            {
                // episodeId is already the cache key, no need to hash it again
                var cacheKey = episodeId;
                
                // Check if episode exists in database
                var episode = await _dbContext.AudioEpisodes
                    .FirstOrDefaultAsync(e => e.CacheKey == cacheKey);
                
                if (episode == null)
                {
                    return NotFound("Episode not found.");
                }

                // Stream from blob storage
                var containerName = GetContainerName("Episodes");
                var blobName = $"{cacheKey}.mp3";
                
                var exists = await _blobStorageService.ExistsAsync(containerName, blobName);
                if (!exists)
                {
                    return NotFound("Episode file not found in storage.");
                }

                var stream = new MemoryStream();
                await _blobStorageService.DownloadToStreamAsync(containerName, blobName, stream);
                stream.Position = 0;

                return File(stream, "audio/mpeg", $"{cacheKey}.mp3");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading episode: {EpisodeId}", episodeId);
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
            string? settingsTtsResponseFormat = null)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    return BadRequest("MP3 URL is required.");
                }

                // Create settings object if any overrides are provided
                AzureOpenAISettings? customSettings = null;
                if (!string.IsNullOrWhiteSpace(settingsEndpoint) ||
                    !string.IsNullOrWhiteSpace(settingsKey) ||
                    !string.IsNullOrWhiteSpace(settingsWhisperDeployment) ||
                    !string.IsNullOrWhiteSpace(settingsGptDeployment) ||
                    !string.IsNullOrWhiteSpace(settingsTtsDeployment) ||
                    settingsTtsSpeedRatio.HasValue ||
                    !string.IsNullOrWhiteSpace(settingsTtsResponseFormat))
                {
                    customSettings = new AzureOpenAISettings
                    {
                        Endpoint = settingsEndpoint,
                        Key = settingsKey,
                        WhisperDeployment = settingsWhisperDeployment,
                        GptDeployment = settingsGptDeployment,
                        TtsDeployment = settingsTtsDeployment,
                        TtsSpeedRatio = settingsTtsSpeedRatio,
                        TtsResponseFormat = settingsTtsResponseFormat
                    };
                    _logger.LogInformation("Using custom Azure OpenAI settings for this request");
                }

                // Validate URL format
                if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return BadRequest("Invalid URL format. Please provide a valid HTTP or HTTPS URL.");
                }

                // Generate cache key from URL
                var cacheKey = GenerateCacheKey(audioUrl);

                // Check if already cached before attempting download
                var wasCached = _audioService.IsEpisodeCached(cacheKey);
                var summaryWasCached = _audioService.IsSummaryCached(cacheKey);

                // Download or retrieve cached episode (pass the full URL as the "episodeId")
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl);

                if (string.IsNullOrEmpty(episodePath))
                {
                    return BadRequest("Failed to download or retrieve episode.");
                }

                // Store custom settings in HttpContext for services to access
                if (customSettings != null)
                {
                    HttpContext.Items["AzureOpenAISettings"] = customSettings;
                }

                // Process through AI pipeline (transcription, summarization, TTS)
                _logger.LogInformation("Starting AI processing pipeline for episode: {CacheKey}", cacheKey);
                var summaryAudioPath = await _audioService.ProcessAndSummarizeEpisodeAsync(cacheKey);

                stopwatch.Stop();

                // Load transcript and summary text for display
                var transcriptPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_transcript.txt");
                var summaryTextPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_summary.txt");

                string? summaryText = null;
                int? transcriptWordCount = null;
                int? summaryWordCount = null;

                if (System.IO.File.Exists(summaryTextPath))
                {
                    summaryText = TrimNonAlphanumeric(await System.IO.File.ReadAllTextAsync(summaryTextPath));
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
                return View("Error", new ErrorViewModel { Message = "Failed to download the audio. Please check the URL and try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio URL: {Url}", audioUrl);
                return View("Error", new ErrorViewModel { Message = $"An error occurred while processing your request: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Result(string episodeId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(episodeId))
                {
                    return BadRequest("Episode ID is required.");
                }

                // episodeId is already the cache key, no need to hash it again
                var cacheKey = episodeId;

                // Check if episode exists in database
                var episode = await _dbContext.AudioEpisodes
                    .Include(e => e.Summary)
                    .FirstOrDefaultAsync(e => e.CacheKey == cacheKey);

                if (episode == null)
                {
                    return NotFound("Episode not found.");
                }

                // Check if summary exists using the service method
                var hasSummary = _audioService.IsSummaryCached(episodeId);

                string? summaryText = null;
                int? transcriptWordCount = null;
                int? summaryWordCount = null;
                string? summaryAudioPath = null;

                // If summary exists, load the summary text from blob storage
                if (hasSummary && episode.Summary != null)
                {
                    var transcriptsContainer = GetContainerName("Transcripts");
                    var summaryTextBlobName = $"{cacheKey}_summary.txt";

                    try
                    {
                        var summaryTextStream = new MemoryStream();
                        await _blobStorageService.DownloadToStreamAsync(transcriptsContainer, summaryTextBlobName, summaryTextStream);
                        summaryTextStream.Position = 0;
                        using var reader = new StreamReader(summaryTextStream);
                        summaryText = TrimNonAlphanumeric(await reader.ReadToEndAsync());
                        summaryWordCount = episode.Summary.SummaryWordCount;
                        transcriptWordCount = episode.Summary.TranscriptWordCount;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not load summary text for episode: {CacheKey}", cacheKey);
                    }

                    // Set the summary audio path to indicate it exists (actual path doesn't matter for display)
                    summaryAudioPath = "available";
                }

                // Build result model
                var result = new AudioProcessResult
                {
                    Success = true,
                    Message = "Episode processed successfully",
                    EpisodeId = cacheKey,
                    FilePath = "cached", // Don't expose actual file path
                    WasCached = true,
                    SummaryAudioPath = summaryAudioPath,
                    SummaryWasCached = hasSummary,
                    SummaryText = summaryText,
                    TranscriptWordCount = transcriptWordCount,
                    SummaryWordCount = summaryWordCount
                };

                return View(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading result for episode: {EpisodeId}", episodeId);
                return View("Error", new ErrorViewModel { Message = "An error occurred while loading the result." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSummary(string episodeId)
        {
            try
            {
                // episodeId is already the cache key, no need to hash it again
                var cacheKey = episodeId;
                
                // Check if summary exists in database
                var episode = await _dbContext.AudioEpisodes
                    .Include(e => e.Summary)
                    .FirstOrDefaultAsync(e => e.CacheKey == cacheKey);
                
                if (episode?.Summary == null)
                {
                    return NotFound("Summary not found.");
                }

                // Stream from blob storage
                var containerName = GetContainerName("Summaries");
                var blobName = $"{cacheKey}_summary.mp3";
                
                var exists = await _blobStorageService.ExistsAsync(containerName, blobName);
                if (!exists)
                {
                    return NotFound("Summary file not found in storage.");
                }

                var stream = new MemoryStream();
                await _blobStorageService.DownloadToStreamAsync(containerName, blobName, stream);
                stream.Position = 0;

                return File(stream, "audio/mpeg", $"{cacheKey}_summary.mp3");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading summary: {EpisodeId}", episodeId);
                return NotFound("Summary not found.");
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
            string? settingsTtsResponseFormat = null)
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    await SendStatusUpdate(new ProcessingStatus
                    {
                        Stage = "error",
                        Message = "MP3 URL is required",
                        IsError = true,
                        ErrorMessage = "MP3 URL is required"
                    });
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
                    !string.IsNullOrWhiteSpace(settingsTtsResponseFormat))
                {
                    customSettings = new AzureOpenAISettings
                    {
                        Endpoint = settingsEndpoint,
                        Key = settingsKey,
                        WhisperDeployment = settingsWhisperDeployment,
                        GptDeployment = settingsGptDeployment,
                        TtsDeployment = settingsTtsDeployment,
                        TtsSpeedRatio = settingsTtsSpeedRatio,
                        TtsResponseFormat = settingsTtsResponseFormat
                    };
                    _logger.LogInformation("Using custom Azure OpenAI settings for streaming request");
                }

                // Validate URL format
                if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    await SendStatusUpdate(new ProcessingStatus
                    {
                        Stage = "error",
                        Message = "Invalid URL format",
                        IsError = true,
                        ErrorMessage = "Invalid URL format. Please provide a valid HTTP or HTTPS URL."
                    });
                    return;
                }

                // Generate cache key from URL
                var cacheKey = GenerateCacheKey(audioUrl);

                // Check if already cached
                var wasCached = _audioService.IsEpisodeCached(cacheKey);
                var summaryWasCached = _audioService.IsSummaryCached(cacheKey);

                // Send initial status
                await SendStatusUpdate(new ProcessingStatus
                {
                    Stage = "downloading",
                    Message = wasCached ? "Retrieving episode from cache..." : "Downloading episode...",
                    Progress = 10,
                    EpisodeId = cacheKey,
                    WasCached = wasCached
                });

                // Download or retrieve cached episode
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl);

                if (string.IsNullOrEmpty(episodePath))
                {
                    await SendStatusUpdate(new ProcessingStatus
                    {
                        Stage = "error",
                        Message = "Failed to download episode",
                        IsError = true,
                        ErrorMessage = "Failed to download or retrieve episode"
                    });
                    return;
                }

                await SendStatusUpdate(new ProcessingStatus
                {
                    Stage = "downloaded",
                    Message = wasCached ? "Episode retrieved from cache" : "Episode downloaded",
                    Progress = 20,
                    EpisodeId = cacheKey,
                    FilePath = episodePath,
                    WasCached = wasCached
                });

                // Store custom settings in HttpContext for services to access
                if (customSettings != null)
                {
                    HttpContext.Items["AzureOpenAISettings"] = customSettings;
                }

                // Process through AI pipeline with progress updates
                _logger.LogInformation("Starting AI processing pipeline for episode: {CacheKey}", cacheKey);
                
                var summaryAudioPath = await _audioService.ProcessAndSummarizeEpisodeAsync(cacheKey, async (status) =>
                {
                    await SendStatusUpdate(status);
                });

                stopwatch.Stop();

                // Load transcript and summary text for final status
                var transcriptPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_transcript.txt");
                var summaryTextPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{cacheKey}_summary.txt");

                string? summaryText = null;
                int? transcriptWordCount = null;
                int? summaryWordCount = null;

                if (System.IO.File.Exists(summaryTextPath))
                {
                    summaryText = TrimNonAlphanumeric(await System.IO.File.ReadAllTextAsync(summaryTextPath));
                    summaryWordCount = summaryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                if (System.IO.File.Exists(transcriptPath))
                {
                    var transcript = await System.IO.File.ReadAllTextAsync(transcriptPath);
                    transcriptWordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                // Send final completion status
                await SendStatusUpdate(new ProcessingStatus
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming processing pipeline");
                await SendStatusUpdate(new ProcessingStatus
                {
                    Stage = "error",
                    Message = "Processing failed",
                    IsError = true,
                    ErrorMessage = ex.Message
                });
            }
        }

        private async Task SendStatusUpdate(ProcessingStatus status)
        {
            var json = JsonSerializer.Serialize(status, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var data = $"data: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(data);
            
            await Response.Body.WriteAsync(bytes);
            await Response.Body.FlushAsync();
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

        private string GenerateCacheKey(string url)
        {
            // Generate a unique cache key from the URL using SHA256 hash
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            
            // Use first 32 characters for a reasonable filename
            var cacheKey = hash.Substring(0, 32);
            
            _logger.LogInformation("Generated cache key {CacheKey} for URL: {Url}", cacheKey, url);
            return cacheKey;
        }
    }
}
