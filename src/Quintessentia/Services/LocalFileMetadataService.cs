using Quintessentia.Models;
using Quintessentia.Services.Contracts;
using System.Text.Json;

namespace Quintessentia.Services
{
    public class LocalFileMetadataService : IMetadataService
    {
        private readonly string _basePath;
        private readonly string _episodesMetadataPath;
        private readonly string _summariesMetadataPath;
        private readonly ILogger<LocalFileMetadataService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public LocalFileMetadataService(
            IConfiguration configuration,
            ILogger<LocalFileMetadataService> logger)
        {
            _logger = logger;
            
            var storageBasePath = configuration["LocalStorage:BasePath"] ?? "LocalStorageData";
            _basePath = Path.Combine(storageBasePath, "metadata");
            _episodesMetadataPath = Path.Combine(_basePath, "episodes");
            _summariesMetadataPath = Path.Combine(_basePath, "summaries");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            // Ensure directories exist
            InitializeDirectories();
        }

        private void InitializeDirectories()
        {
            foreach (var path in new[] { _basePath, _episodesMetadataPath, _summariesMetadataPath })
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    _logger.LogInformation("Created metadata directory: {Path}", path);
                }
            }
        }

        private string GetEpisodeFilePath(string cacheKey)
        {
            return Path.Combine(_episodesMetadataPath, $"{cacheKey}.json");
        }

        private string GetSummaryFilePath(string cacheKey)
        {
            return Path.Combine(_summariesMetadataPath, $"{cacheKey}.json");
        }

        public async Task<AudioEpisode?> GetEpisodeMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetEpisodeFilePath(cacheKey);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("Episode metadata not found: {CacheKey}", cacheKey);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var episode = JsonSerializer.Deserialize<AudioEpisode>(json, _jsonOptions);
                
                _logger.LogInformation("Retrieved episode metadata: {CacheKey}", cacheKey);
                return episode;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error retrieving episode metadata: {CacheKey}", cacheKey);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for episode metadata: {CacheKey}", cacheKey);
                throw;
            }
        }

        public async Task SaveEpisodeMetadataAsync(AudioEpisode episode, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetEpisodeFilePath(episode.CacheKey);
                var json = JsonSerializer.Serialize(episode, _jsonOptions);
                
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
                
                _logger.LogInformation("Saved episode metadata: {CacheKey}", episode.CacheKey);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error saving episode metadata: {CacheKey}", episode.CacheKey);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error for episode metadata: {CacheKey}", episode.CacheKey);
                throw;
            }
        }

        public Task<bool> EpisodeExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetEpisodeFilePath(cacheKey);
                var exists = File.Exists(filePath);
                
                _logger.LogDebug("Episode metadata exists check: {CacheKey} = {Exists}", cacheKey, exists);
                return Task.FromResult(exists);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error checking episode metadata existence: {CacheKey}", cacheKey);
                return Task.FromResult(false);
            }
        }

        public async Task<AudioSummary?> GetSummaryMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetSummaryFilePath(cacheKey);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("Summary metadata not found: {CacheKey}", cacheKey);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var summary = JsonSerializer.Deserialize<AudioSummary>(json, _jsonOptions);
                
                _logger.LogInformation("Retrieved summary metadata: {CacheKey}", cacheKey);
                return summary;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error retrieving summary metadata: {CacheKey}", cacheKey);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for summary metadata: {CacheKey}", cacheKey);
                throw;
            }
        }

        public async Task SaveSummaryMetadataAsync(string cacheKey, AudioSummary summary, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetSummaryFilePath(cacheKey);
                var json = JsonSerializer.Serialize(summary, _jsonOptions);
                
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
                
                _logger.LogInformation("Saved summary metadata: {CacheKey}", cacheKey);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error saving summary metadata: {CacheKey}", cacheKey);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error for summary metadata: {CacheKey}", cacheKey);
                throw;
            }
        }

        public Task<bool> SummaryExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetSummaryFilePath(cacheKey);
                var exists = File.Exists(filePath);
                
                _logger.LogDebug("Summary metadata exists check: {CacheKey} = {Exists}", cacheKey, exists);
                return Task.FromResult(exists);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error checking summary metadata existence: {CacheKey}", cacheKey);
                return Task.FromResult(false);
            }
        }

        public Task DeleteEpisodeMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetEpisodeFilePath(cacheKey);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted episode metadata: {CacheKey}", cacheKey);
                }
                else
                {
                    _logger.LogDebug("Episode metadata not found for deletion: {CacheKey}", cacheKey);
                }
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error deleting episode metadata: {CacheKey}", cacheKey);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied deleting episode metadata: {CacheKey}", cacheKey);
                throw;
            }
            
            return Task.CompletedTask;
        }

        public Task DeleteSummaryMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = GetSummaryFilePath(cacheKey);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted summary metadata: {CacheKey}", cacheKey);
                }
                else
                {
                    _logger.LogDebug("Summary metadata not found for deletion: {CacheKey}", cacheKey);
                }
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error deleting summary metadata: {CacheKey}", cacheKey);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied deleting summary metadata: {CacheKey}", cacheKey);
                throw;
            }
            
            return Task.CompletedTask;
        }
    }
}
