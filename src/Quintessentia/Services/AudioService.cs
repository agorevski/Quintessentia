using Quintessentia.Models;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    public class AudioService : IAudioService
    {
        private readonly ILogger<AudioService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IAzureOpenAIService _azureOpenAIService;
        private readonly IStorageService _storageService;
        private readonly IMetadataService _metadataService;
        private readonly IStorageConfiguration _storageConfiguration;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly string _tempDirectory;

        public AudioService(
            ILogger<AudioService> logger,
            IHttpClientFactory httpClientFactory,
            IAzureOpenAIService azureOpenAIService,
            IStorageService storageService,
            IMetadataService metadataService,
            IStorageConfiguration storageConfiguration,
            ICacheKeyService cacheKeyService)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _azureOpenAIService = azureOpenAIService;
            _storageService = storageService;
            _metadataService = metadataService;
            _storageConfiguration = storageConfiguration;
            _cacheKeyService = cacheKeyService;

            // Use system temp directory for processing
            _tempDirectory = Path.GetTempPath();
            _logger.LogInformation("Using temp directory: {TempDirectory}", _tempDirectory);
        }

        public async Task<string> GetOrDownloadEpisodeAsync(string episodeId)
        {
            if (string.IsNullOrWhiteSpace(episodeId))
            {
                throw new ArgumentException("Episode ID/URL cannot be null or empty.", nameof(episodeId));
            }

            // Generate cache key from URL if it's a URL, otherwise use as-is
            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);

            // Check if episode exists in blob storage
            if (await _metadataService.EpisodeExistsAsync(cacheKey))
            {
                _logger.LogInformation("Episode found in cache: {CacheKey}", cacheKey);

                // Download to temp file for processing
                var containerName = _storageConfiguration.GetContainerName("Episodes");
                var tempPath = GetTempFilePath(cacheKey, ".mp3");
                await _storageService.DownloadToFileAsync(containerName, $"{cacheKey}.mp3", tempPath);

                return tempPath;
            }

            // Download the episode - episodeId is the URL here
            _logger.LogInformation("Episode not in cache, downloading from URL...");
            return await DownloadEpisodeAsync(episodeId, cacheKey);
        }

        public bool IsEpisodeCached(string episodeId)
        {
            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);
            return _metadataService.EpisodeExistsAsync(cacheKey).Result;
        }

        public string GetCachedEpisodePath(string episodeId)
        {
            // Return a temp path where the file will be downloaded when needed
            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);
            return GetTempFilePath(cacheKey, ".mp3");
        }

        public async Task<string> DownloadEpisodeAsync(string url, string cacheKey)
        {
            string tempPath = GetTempFilePath(cacheKey, ".mp3");

            try
            {
                // Download the actual MP3 file from the URL to temp location
                await DownloadMp3FileAsync(url, tempPath);

                // Upload to blob storage
                var containerName = _storageConfiguration.GetContainerName("Episodes");
                var blobPath = $"{cacheKey}.mp3";
                await _storageService.UploadFileAsync(containerName, blobPath, tempPath);

                // Get file size
                var fileInfo = new FileInfo(tempPath);

                // Save metadata to blob storage
                var episode = new AudioEpisode
                {
                    CacheKey = cacheKey,
                    OriginalUrl = url,
                    BlobPath = $"{containerName}/{blobPath}",
                    FileSize = fileInfo.Length,
                    DownloadDate = DateTime.UtcNow
                };

                await _metadataService.SaveEpisodeMetadataAsync(episode);

                _logger.LogInformation("Successfully downloaded and cached episode: {CacheKey}", cacheKey);
                return tempPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading episode from URL");

                // Clean up temp file on error
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }

                throw;
            }
        }

        private async Task DownloadMp3FileAsync(string url, string filePath)
        {
            try
            {
                _logger.LogInformation("Downloading MP3 from URL: {Url}", url);

                // Download the file with streaming to handle large files efficiently
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // Verify content type is audio
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType != null && !contentType.StartsWith("audio/"))
                {
                    _logger.LogWarning("Content type is {ContentType}, expected audio/*", contentType);
                }

                // Stream the content to file
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                await contentStream.CopyToAsync(fileStream);

                _logger.LogInformation("Successfully downloaded {Bytes} bytes to {FilePath}",
                    new FileInfo(filePath).Length, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading MP3 file from URL: {Url}", url);

                // Clean up partial download if it exists
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
                throw;
            }
        }

        public async Task<string> ProcessAndSummarizeEpisodeAsync(string episodeId)
        {
            return await ProcessAndSummarizeEpisodeAsync(episodeId, null);
        }

        public async Task<string> ProcessAndSummarizeEpisodeAsync(string episodeId, Action<ProcessingStatus>? progressCallback)
        {
            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);

            try
            {
                _logger.LogInformation("Starting full AI processing pipeline for episode: {CacheKey}", cacheKey);

                // Check if summary already exists in blob storage
                if (await _metadataService.SummaryExistsAsync(cacheKey))
                {
                    _logger.LogInformation("Summary found in cache: {CacheKey}", cacheKey);

                    // Download summary to temp location
                    var summaryContainer = _storageConfiguration.GetContainerName("Summaries");
                    var tempSummaryPath = GetTempFilePath(cacheKey, "_summary.mp3");
                    await _storageService.DownloadToFileAsync(
                        summaryContainer,
                        $"{cacheKey}_summary.mp3",
                        tempSummaryPath);

                    progressCallback?.Invoke(new ProcessingStatus
                    {
                        Stage = "complete",
                        Message = "Summary retrieved from cache",
                        Progress = 100,
                        IsComplete = true,
                        EpisodeId = cacheKey,
                        SummaryAudioPath = tempSummaryPath
                    });

                    return tempSummaryPath;
                }

                // Get the episode file (download from blob to temp if needed)
                var episodePath = await GetOrDownloadEpisodeAsync(episodeId);

                if (!File.Exists(episodePath))
                {
                    throw new FileNotFoundException($"Episode file not found: {episodePath}");
                }

                // Step 1: Transcribe audio to text
                _logger.LogInformation("Step 1/3: Transcribing audio to text...");
                progressCallback?.Invoke(new ProcessingStatus
                {
                    Stage = "transcribing",
                    Message = "Transcribing audio to text using Azure Whisper...",
                    Progress = 25,
                    EpisodeId = cacheKey
                });

                var transcript = await _azureOpenAIService.TranscribeAudioAsync(episodePath);

                // Save transcript to blob storage
                var transcriptsContainer = _storageConfiguration.GetContainerName("Transcripts");
                var transcriptBlobName = $"{cacheKey}_transcript.txt";
                using (var transcriptStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(transcript)))
                {
                    await _storageService.UploadStreamAsync(transcriptsContainer, transcriptBlobName, transcriptStream);
                }

                var transcriptWordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                progressCallback?.Invoke(new ProcessingStatus
                {
                    Stage = "transcribed",
                    Message = $"Transcription complete ({transcriptWordCount:N0} words)",
                    Progress = 40,
                    EpisodeId = cacheKey,
                    TranscriptWordCount = transcriptWordCount
                });

                // Step 2: Summarize transcript with GPT
                _logger.LogInformation("Step 2/3: Summarizing transcript with GPT...");
                progressCallback?.Invoke(new ProcessingStatus
                {
                    Stage = "summarizing",
                    Message = "Summarizing transcript with GPT-5...",
                    Progress = 50,
                    EpisodeId = cacheKey,
                    TranscriptWordCount = transcriptWordCount
                });

                var summary = await _azureOpenAIService.SummarizeTranscriptAsync(transcript);

                // Save summary text to blob storage
                var summaryTextBlobName = $"{cacheKey}_summary.txt";
                using (var summaryTextStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(summary)))
                {
                    await _storageService.UploadStreamAsync(transcriptsContainer, summaryTextBlobName, summaryTextStream);
                }

                var summaryWordCount = summary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                progressCallback?.Invoke(new ProcessingStatus
                {
                    Stage = "summarized",
                    Message = $"Summary complete ({summaryWordCount:N0} words)",
                    Progress = 70,
                    EpisodeId = cacheKey,
                    TranscriptWordCount = transcriptWordCount,
                    SummaryWordCount = summaryWordCount,
                    SummaryText = summary
                });

                // Step 3: Generate speech from summary
                _logger.LogInformation("Step 3/3: Generating speech from summary...");
                progressCallback?.Invoke(new ProcessingStatus
                {
                    Stage = "generating-speech",
                    Message = "Generating speech from summary using GPT-4o-mini-tts...",
                    Progress = 80,
                    EpisodeId = cacheKey,
                    TranscriptWordCount = transcriptWordCount,
                    SummaryWordCount = summaryWordCount,
                    SummaryText = summary
                });

                // Generate speech to temp file
                var tempSummaryAudioPath = GetTempFilePath(cacheKey, "_summary.mp3");
                await _azureOpenAIService.GenerateSpeechAsync(summary, tempSummaryAudioPath);

                // Upload summary audio to blob storage
                var summariesContainer = _storageConfiguration.GetContainerName("Summaries");
                var summaryAudioBlobName = $"{cacheKey}_summary.mp3";
                await _storageService.UploadFileAsync(summariesContainer, summaryAudioBlobName, tempSummaryAudioPath);

                // Save summary metadata to blob storage
                var audioSummary = new AudioSummary
                {
                    CacheKey = cacheKey,
                    TranscriptBlobPath = $"{transcriptsContainer}/{transcriptBlobName}",
                    SummaryTextBlobPath = $"{transcriptsContainer}/{summaryTextBlobName}",
                    SummaryAudioBlobPath = $"{summariesContainer}/{summaryAudioBlobName}",
                    TranscriptWordCount = transcriptWordCount,
                    SummaryWordCount = summaryWordCount,
                    ProcessedDate = DateTime.UtcNow
                };

                await _metadataService.SaveSummaryMetadataAsync(cacheKey, audioSummary);

                _logger.LogInformation("Full AI processing pipeline completed successfully. Summary audio at: {SummaryAudioPath}", tempSummaryAudioPath);

                progressCallback?.Invoke(new ProcessingStatus
                {
                    Stage = "complete",
                    Message = "Processing complete!",
                    Progress = 100,
                    IsComplete = true,
                    EpisodeId = cacheKey,
                    TranscriptWordCount = transcriptWordCount,
                    SummaryWordCount = summaryWordCount,
                    SummaryText = summary,
                    SummaryAudioPath = tempSummaryAudioPath
                });

                return tempSummaryAudioPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI processing pipeline for episode: {CacheKey}", cacheKey);

                progressCallback?.Invoke(new ProcessingStatus
                {
                    Stage = "error",
                    Message = "Processing failed",
                    Progress = 0,
                    IsError = true,
                    ErrorMessage = ex.Message,
                    EpisodeId = cacheKey
                });

                throw;
            }
        }

        public string GetSummaryPath(string episodeId)
        {
            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);
            return GetTempFilePath(cacheKey, "_summary.mp3");
        }

        public bool IsSummaryCached(string episodeId)
        {
            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);
            return _metadataService.SummaryExistsAsync(cacheKey).Result;
        }

        private string GetTempFilePath(string cacheKey, string suffix)
        {
            return Path.Combine(_tempDirectory, $"audio_{cacheKey}{suffix}");
        }
    }
}
