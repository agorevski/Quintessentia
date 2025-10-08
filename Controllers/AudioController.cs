using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpotifySummarizer.Data;
using SpotifySummarizer.Models;
using SpotifySummarizer.Services;
using System.Diagnostics;
using System.Text.Json;
using System.Text;

namespace SpotifySummarizer.Controllers
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

                // Return success with the cached file path
                var result = new AudioProcessResult
                {
                    Success = true,
                    Message = wasCached ? "Episode retrieved from cache" : "Episode downloaded successfully",
                    EpisodeId = cacheKey,
                    FilePath = episodePath,
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
                var cacheKey = GenerateCacheKey(episodeId);
                
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
        public async Task<IActionResult> ProcessAndSummarize(string audioUrl)
        {
            var stopwatch = Stopwatch.StartNew();
            
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
                var summaryWasCached = _audioService.IsSummaryCached(cacheKey);

                // Download or retrieve cached episode (pass the full URL as the "episodeId")
                var episodePath = await _audioService.GetOrDownloadEpisodeAsync(audioUrl);

                if (string.IsNullOrEmpty(episodePath))
                {
                    return BadRequest("Failed to download or retrieve episode.");
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
                    summaryText = await System.IO.File.ReadAllTextAsync(summaryTextPath);
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
                    FilePath = episodePath,
                    WasCached = wasCached,
                    SummaryAudioPath = summaryAudioPath,
                    SummaryWasCached = summaryWasCached,
                    TranscriptPath = transcriptPath,
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

                // Get episode path
                var episodePath = _audioService.GetCachedEpisodePath(episodeId);
                if (!System.IO.File.Exists(episodePath))
                {
                    return NotFound("Episode not found.");
                }

                // Check if summary exists
                var summaryPath = _audioService.GetSummaryPath(episodeId);
                var hasSummary = System.IO.File.Exists(summaryPath);

                // Load transcript and summary text for display
                var transcriptPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{episodeId}_transcript.txt");
                var summaryTextPath = Path.Combine(Path.GetDirectoryName(episodePath)!, $"{episodeId}_summary.txt");

                string? summaryText = null;
                int? transcriptWordCount = null;
                int? summaryWordCount = null;

                if (System.IO.File.Exists(summaryTextPath))
                {
                    summaryText = await System.IO.File.ReadAllTextAsync(summaryTextPath);
                    summaryWordCount = summaryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                if (System.IO.File.Exists(transcriptPath))
                {
                    var transcript = await System.IO.File.ReadAllTextAsync(transcriptPath);
                    transcriptWordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }

                // Build result model
                var result = new AudioProcessResult
                {
                    Success = true,
                    Message = "Episode processed successfully",
                    EpisodeId = episodeId,
                    FilePath = episodePath,
                    WasCached = true,
                    SummaryAudioPath = hasSummary ? summaryPath : null,
                    SummaryWasCached = hasSummary,
                    TranscriptPath = transcriptPath,
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
                var cacheKey = GenerateCacheKey(episodeId);
                
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
        public async Task ProcessAndSummarizeStream(string audioUrl)
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
                    summaryText = await System.IO.File.ReadAllTextAsync(summaryTextPath);
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
