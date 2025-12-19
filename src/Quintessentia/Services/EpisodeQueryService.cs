using Quintessentia.Models;
using Quintessentia.Services.Contracts;
using Quintessentia.Utilities;

namespace Quintessentia.Services
{
    public class EpisodeQueryService : IEpisodeQueryService
    {
        private readonly IStorageService _storageService;
        private readonly IMetadataService _metadataService;
        private readonly IStorageConfiguration _storageConfiguration;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly ILogger<EpisodeQueryService> _logger;

        public EpisodeQueryService(
            IStorageService storageService,
            IMetadataService metadataService,
            IStorageConfiguration storageConfiguration,
            ICacheKeyService cacheKeyService,
            ILogger<EpisodeQueryService> logger)
        {
            _storageService = storageService;
            _metadataService = metadataService;
            _storageConfiguration = storageConfiguration;
            _cacheKeyService = cacheKeyService;
            _logger = logger;
        }

        public async Task<AudioProcessResult> GetResultAsync(string episodeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(episodeId))
            {
                throw new ArgumentException("Episode ID is required.", nameof(episodeId));
            }

            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);

            cancellationToken.ThrowIfCancellationRequested();

            // Check if episode exists
            if (!await _metadataService.EpisodeExistsAsync(cacheKey, cancellationToken))
            {
                throw new FileNotFoundException($"Episode not found: {cacheKey}");
            }

            // Check if summary exists
            var hasSummary = await _metadataService.SummaryExistsAsync(cacheKey, cancellationToken);

            string? summaryText = null;
            int? transcriptWordCount = null;
            int? summaryWordCount = null;
            string? summaryAudioPath = null;

            // If summary exists, load the summary text and metadata
            if (hasSummary)
            {
                var summaryMetadata = await _metadataService.GetSummaryMetadataAsync(cacheKey, cancellationToken);
                if (summaryMetadata != null)
                {
                    var transcriptsContainer = _storageConfiguration.GetContainerName("Transcripts");
                    var summaryTextBlobName = $"{cacheKey}_summary.txt";

                    try
                    {
                        var summaryTextStream = new MemoryStream();
                        await _storageService.DownloadToStreamAsync(transcriptsContainer, summaryTextBlobName, summaryTextStream, cancellationToken);
                        summaryTextStream.Position = 0;
                        using var reader = new StreamReader(summaryTextStream);
                        var rawSummaryText = await reader.ReadToEndAsync(cancellationToken);
                        summaryText = TextHelper.TrimNonAlphanumeric(rawSummaryText);
                        summaryWordCount = summaryMetadata.SummaryWordCount;
                        transcriptWordCount = summaryMetadata.TranscriptWordCount;
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "IO error loading summary text for episode: {CacheKey}", cacheKey);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(ex, "Could not load summary text for episode: {CacheKey}", cacheKey);
                    }

                    // Set the summary audio path to indicate it exists
                    summaryAudioPath = "available";
                }
            }

            return new AudioProcessResult
            {
                Success = true,
                Message = "Episode processed successfully",
                EpisodeId = cacheKey,
                FilePath = "cached",
                WasCached = true,
                SummaryAudioPath = summaryAudioPath,
                SummaryWasCached = hasSummary,
                SummaryText = summaryText,
                TranscriptWordCount = transcriptWordCount,
                SummaryWordCount = summaryWordCount
            };
        }

        public async Task<Stream> GetEpisodeStreamAsync(string episodeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(episodeId))
            {
                throw new ArgumentException("Episode ID is required.", nameof(episodeId));
            }

            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);

            cancellationToken.ThrowIfCancellationRequested();

            // Check if episode exists
            if (!await _metadataService.EpisodeExistsAsync(cacheKey, cancellationToken))
            {
                throw new FileNotFoundException($"Episode not found: {cacheKey}");
            }

            // Stream from blob storage
            var containerName = _storageConfiguration.GetContainerName("Episodes");
            var blobName = $"{cacheKey}.mp3";

            var stream = new MemoryStream();
            await _storageService.DownloadToStreamAsync(containerName, blobName, stream, cancellationToken);
            stream.Position = 0;

            return stream;
        }

        public async Task<Stream> GetSummaryStreamAsync(string episodeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(episodeId))
            {
                throw new ArgumentException("Episode ID is required.", nameof(episodeId));
            }

            var cacheKey = _cacheKeyService.GenerateFromUrl(episodeId);

            cancellationToken.ThrowIfCancellationRequested();

            // Check if summary exists
            if (!await _metadataService.SummaryExistsAsync(cacheKey, cancellationToken))
            {
                throw new FileNotFoundException($"Summary not found: {cacheKey}");
            }

            // Stream from blob storage
            var containerName = _storageConfiguration.GetContainerName("Summaries");
            var blobName = $"{cacheKey}_summary.mp3";

            var stream = new MemoryStream();
            await _storageService.DownloadToStreamAsync(containerName, blobName, stream, cancellationToken);
            stream.Position = 0;

            return stream;
        }
    }
}
