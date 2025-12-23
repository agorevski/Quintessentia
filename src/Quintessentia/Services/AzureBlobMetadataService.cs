using System.Text.Json;
using Azure;
using Quintessentia.Models;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Services
{
    /// <summary>
    /// Azure Blob Storage-based implementation of metadata service.
    /// </summary>
    public class AzureBlobMetadataService : IMetadataService
    {
        private readonly IStorageService _storageService;
        private readonly ILogger<AzureBlobMetadataService> _logger;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        public AzureBlobMetadataService(
            IStorageService storageService,
            JsonOptionsService jsonOptionsService,
            ILogger<AzureBlobMetadataService> logger,
            IConfiguration configuration)
        {
            _storageService = storageService;
            _logger = logger;
            _configuration = configuration;
            _jsonOptions = jsonOptionsService.Options;
        }

        private string GetContainerName(string containerType)
        {
            return _configuration[$"AzureStorage:Containers:{containerType}"] ?? containerType.ToLower();
        }

        public async Task<AudioEpisode?> GetEpisodeMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerName = GetContainerName("Episodes");
                var metadataPath = $"{cacheKey}.json";

                if (!await _storageService.ExistsAsync(containerName, metadataPath, cancellationToken))
                {
                    return null;
                }

                using var stream = new MemoryStream();
                await _storageService.DownloadToStreamAsync(containerName, metadataPath, stream, cancellationToken);
                
                stream.Position = 0;
                var episode = await JsonSerializer.DeserializeAsync<AudioEpisode>(stream, _jsonOptions, cancellationToken);
                
                _logger.LogInformation("Retrieved episode metadata for cache key: {CacheKey}", cacheKey);
                return episode;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error retrieving episode metadata for cache key: {CacheKey}", cacheKey);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for episode metadata cache key: {CacheKey}", cacheKey);
                return null;
            }
        }

        public async Task SaveEpisodeMetadataAsync(AudioEpisode episode, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerName = GetContainerName("Episodes");
                var metadataPath = $"{episode.CacheKey}.json";

                using var stream = new MemoryStream();
                await JsonSerializer.SerializeAsync(stream, episode, _jsonOptions, cancellationToken);
                stream.Position = 0;

                await _storageService.UploadStreamAsync(containerName, metadataPath, stream, cancellationToken);
                
                _logger.LogInformation("Saved episode metadata for cache key: {CacheKey}", episode.CacheKey);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error saving episode metadata for cache key: {CacheKey}", episode.CacheKey);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error for episode metadata cache key: {CacheKey}", episode.CacheKey);
                throw;
            }
        }

        public async Task<bool> EpisodeExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerName = GetContainerName("Episodes");
                
                // Check if both the audio file and metadata exist
                var audioExists = await _storageService.ExistsAsync(containerName, $"{cacheKey}.mp3", cancellationToken);
                var metadataExists = await _storageService.ExistsAsync(containerName, $"{cacheKey}.json", cancellationToken);
                
                return audioExists && metadataExists;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error checking episode existence for cache key: {CacheKey}", cacheKey);
                return false;
            }
        }

        public async Task<AudioSummary?> GetSummaryMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerName = GetContainerName("Summaries");
                var metadataPath = $"{cacheKey}_summary.json";

                if (!await _storageService.ExistsAsync(containerName, metadataPath, cancellationToken))
                {
                    return null;
                }

                using var stream = new MemoryStream();
                await _storageService.DownloadToStreamAsync(containerName, metadataPath, stream, cancellationToken);
                
                stream.Position = 0;
                var summary = await JsonSerializer.DeserializeAsync<AudioSummary>(stream, _jsonOptions, cancellationToken);
                
                _logger.LogInformation("Retrieved summary metadata for cache key: {CacheKey}", cacheKey);
                return summary;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error retrieving summary metadata for cache key: {CacheKey}", cacheKey);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for summary metadata cache key: {CacheKey}", cacheKey);
                return null;
            }
        }

        public async Task SaveSummaryMetadataAsync(string cacheKey, AudioSummary summary, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerName = GetContainerName("Summaries");
                var metadataPath = $"{cacheKey}_summary.json";

                using var stream = new MemoryStream();
                await JsonSerializer.SerializeAsync(stream, summary, _jsonOptions, cancellationToken);
                stream.Position = 0;

                await _storageService.UploadStreamAsync(containerName, metadataPath, stream, cancellationToken);
                
                _logger.LogInformation("Saved summary metadata for cache key: {CacheKey}", cacheKey);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error saving summary metadata for cache key: {CacheKey}", cacheKey);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error for summary metadata cache key: {CacheKey}", cacheKey);
                throw;
            }
        }

        public async Task<bool> SummaryExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerName = GetContainerName("Summaries");
                
                // Check if both the summary audio and metadata exist
                var audioExists = await _storageService.ExistsAsync(containerName, $"{cacheKey}_summary.mp3", cancellationToken);
                var metadataExists = await _storageService.ExistsAsync(containerName, $"{cacheKey}_summary.json", cancellationToken);
                
                return audioExists && metadataExists;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error checking summary existence for cache key: {CacheKey}", cacheKey);
                return false;
            }
        }

        public async Task DeleteEpisodeMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerName = GetContainerName("Episodes");
                
                // Delete both audio and metadata
                await _storageService.DeleteAsync(containerName, $"{cacheKey}.mp3", cancellationToken);
                await _storageService.DeleteAsync(containerName, $"{cacheKey}.json", cancellationToken);
                
                _logger.LogInformation("Deleted episode metadata for cache key: {CacheKey}", cacheKey);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error deleting episode metadata for cache key: {CacheKey}", cacheKey);
                throw;
            }
        }

        public async Task DeleteSummaryMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerName = GetContainerName("Summaries");
                
                // Delete summary audio and metadata
                await _storageService.DeleteAsync(containerName, $"{cacheKey}_summary.mp3", cancellationToken);
                await _storageService.DeleteAsync(containerName, $"{cacheKey}_summary.json", cancellationToken);
                
                _logger.LogInformation("Deleted summary metadata for cache key: {CacheKey}", cacheKey);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure storage error deleting summary metadata for cache key: {CacheKey}", cacheKey);
                throw;
            }
        }
    }
}
