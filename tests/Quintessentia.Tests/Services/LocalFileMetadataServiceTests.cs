using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Models;
using Quintessentia.Services;
using Xunit;

namespace Quintessentia.Tests.Services
{
    public class LocalFileMetadataServiceTests : IDisposable
    {
        private readonly Mock<ILogger<LocalFileMetadataService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly LocalFileMetadataService _service;
        private readonly string _testBasePath;

        public LocalFileMetadataServiceTests()
        {
            _mockLogger = new Mock<ILogger<LocalFileMetadataService>>();
            _mockConfiguration = new Mock<IConfiguration>();

            _testBasePath = Path.Combine(Path.GetTempPath(), "LocalFileMetadataServiceTests", Guid.NewGuid().ToString());
            _mockConfiguration.Setup(c => c["LocalStorage:BasePath"]).Returns(_testBasePath);

            _service = new LocalFileMetadataService(_mockConfiguration.Object, _mockLogger.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBasePath))
            {
                Directory.Delete(_testBasePath, recursive: true);
            }
        }

        [Fact]
        public void Constructor_CreatesMetadataDirectories()
        {
            // Assert
            var metadataPath = Path.Combine(_testBasePath, "metadata");
            var episodesPath = Path.Combine(metadataPath, "episodes");
            var summariesPath = Path.Combine(metadataPath, "summaries");

            Directory.Exists(metadataPath).Should().BeTrue();
            Directory.Exists(episodesPath).Should().BeTrue();
            Directory.Exists(summariesPath).Should().BeTrue();
        }

        [Fact]
        public async Task SaveEpisodeMetadataAsync_CreatesMetadataFile()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                BlobPath = "/path/to/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };

            // Act
            await _service.SaveEpisodeMetadataAsync(episode);

            // Assert
            var exists = await _service.EpisodeExistsAsync("test-key");
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task GetEpisodeMetadataAsync_ReturnsNull_WhenNotFound()
        {
            // Act
            var result = await _service.GetEpisodeMetadataAsync("nonexistent-key");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetEpisodeMetadataAsync_ReturnsSavedMetadata()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                BlobPath = "/path/to/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };
            await _service.SaveEpisodeMetadataAsync(episode);

            // Act
            var result = await _service.GetEpisodeMetadataAsync("test-key");

            // Assert
            result.Should().NotBeNull();
            result!.CacheKey.Should().Be(episode.CacheKey);
            result.OriginalUrl.Should().Be(episode.OriginalUrl);
            result.BlobPath.Should().Be(episode.BlobPath);
        }

        [Fact]
        public async Task EpisodeExistsAsync_ReturnsFalse_WhenNotFound()
        {
            // Act
            var exists = await _service.EpisodeExistsAsync("nonexistent-key");

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task EpisodeExistsAsync_ReturnsTrue_WhenExists()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                BlobPath = "/path/to/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };
            await _service.SaveEpisodeMetadataAsync(episode);

            // Act
            var exists = await _service.EpisodeExistsAsync("test-key");

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteEpisodeMetadataAsync_RemovesMetadata()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                BlobPath = "/path/to/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };
            await _service.SaveEpisodeMetadataAsync(episode);

            // Act
            await _service.DeleteEpisodeMetadataAsync("test-key");

            // Assert
            var exists = await _service.EpisodeExistsAsync("test-key");
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteEpisodeMetadataAsync_DoesNotThrow_WhenNotFound()
        {
            // Act
            var act = async () => await _service.DeleteEpisodeMetadataAsync("nonexistent-key");

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task SaveSummaryMetadataAsync_CreatesMetadataFile()
        {
            // Arrange
            var summary = new AudioSummary
            {
                CacheKey = "test-key",
                TranscriptBlobPath = "/path/to/transcript.txt",
                SummaryTextBlobPath = "/path/to/summary.txt",
                SummaryAudioBlobPath = "/path/to/summary.mp3",
                ProcessedDate = DateTime.UtcNow
            };

            // Act
            await _service.SaveSummaryMetadataAsync("test-key", summary);

            // Assert
            var exists = await _service.SummaryExistsAsync("test-key");
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task GetSummaryMetadataAsync_ReturnsNull_WhenNotFound()
        {
            // Act
            var result = await _service.GetSummaryMetadataAsync("nonexistent-key");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSummaryMetadataAsync_ReturnsSavedMetadata()
        {
            // Arrange
            var summary = new AudioSummary
            {
                CacheKey = "test-key",
                TranscriptBlobPath = "/path/to/transcript.txt",
                SummaryTextBlobPath = "/path/to/summary.txt",
                SummaryAudioBlobPath = "/path/to/summary.mp3",
                ProcessedDate = DateTime.UtcNow
            };
            await _service.SaveSummaryMetadataAsync("test-key", summary);

            // Act
            var result = await _service.GetSummaryMetadataAsync("test-key");

            // Assert
            result.Should().NotBeNull();
            result!.TranscriptBlobPath.Should().Be(summary.TranscriptBlobPath);
            result.SummaryTextBlobPath.Should().Be(summary.SummaryTextBlobPath);
            result.SummaryAudioBlobPath.Should().Be(summary.SummaryAudioBlobPath);
        }

        [Fact]
        public async Task SummaryExistsAsync_ReturnsFalse_WhenNotFound()
        {
            // Act
            var exists = await _service.SummaryExistsAsync("nonexistent-key");

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task SummaryExistsAsync_ReturnsTrue_WhenExists()
        {
            // Arrange
            var summary = new AudioSummary
            {
                CacheKey = "test-key",
                TranscriptBlobPath = "/path/to/transcript.txt",
                SummaryTextBlobPath = "/path/to/summary.txt",
                SummaryAudioBlobPath = "/path/to/summary.mp3",
                ProcessedDate = DateTime.UtcNow
            };
            await _service.SaveSummaryMetadataAsync("test-key", summary);

            // Act
            var exists = await _service.SummaryExistsAsync("test-key");

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteSummaryMetadataAsync_RemovesMetadata()
        {
            // Arrange
            var summary = new AudioSummary
            {
                CacheKey = "test-key",
                TranscriptBlobPath = "/path/to/transcript.txt",
                SummaryTextBlobPath = "/path/to/summary.txt",
                SummaryAudioBlobPath = "/path/to/summary.mp3",
                ProcessedDate = DateTime.UtcNow
            };
            await _service.SaveSummaryMetadataAsync("test-key", summary);

            // Act
            await _service.DeleteSummaryMetadataAsync("test-key");

            // Assert
            var exists = await _service.SummaryExistsAsync("test-key");
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteSummaryMetadataAsync_DoesNotThrow_WhenNotFound()
        {
            // Act
            var act = async () => await _service.DeleteSummaryMetadataAsync("nonexistent-key");

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task SaveEpisodeMetadataAsync_OverwritesExistingMetadata()
        {
            // Arrange
            var episode1 = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/old.mp3",
                BlobPath = "/old/path.mp3",
                DownloadDate = DateTime.UtcNow.AddDays(-1)
            };
            await _service.SaveEpisodeMetadataAsync(episode1);

            var episode2 = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/new.mp3",
                BlobPath = "/new/path.mp3",
                DownloadDate = DateTime.UtcNow
            };

            // Act
            await _service.SaveEpisodeMetadataAsync(episode2);

            // Assert
            var result = await _service.GetEpisodeMetadataAsync("test-key");
            result.Should().NotBeNull();
            result!.OriginalUrl.Should().Be("https://example.com/new.mp3");
            result.BlobPath.Should().Be("/new/path.mp3");
        }

        [Fact]
        public async Task SaveSummaryMetadataAsync_OverwritesExistingMetadata()
        {
            // Arrange
            var summary1 = new AudioSummary
            {
                CacheKey = "test-key",
                TranscriptBlobPath = "/old/transcript.txt",
                SummaryTextBlobPath = "/old/summary.txt",
                SummaryAudioBlobPath = "/old/summary.mp3",
                ProcessedDate = DateTime.UtcNow.AddDays(-1)
            };
            await _service.SaveSummaryMetadataAsync("test-key", summary1);

            var summary2 = new AudioSummary
            {
                CacheKey = "test-key",
                TranscriptBlobPath = "/new/transcript.txt",
                SummaryTextBlobPath = "/new/summary.txt",
                SummaryAudioBlobPath = "/new/summary.mp3",
                ProcessedDate = DateTime.UtcNow
            };

            // Act
            await _service.SaveSummaryMetadataAsync("test-key", summary2);

            // Assert
            var result = await _service.GetSummaryMetadataAsync("test-key");
            result.Should().NotBeNull();
            result!.TranscriptBlobPath.Should().Be("/new/transcript.txt");
            result.SummaryTextBlobPath.Should().Be("/new/summary.txt");
        }

        [Fact]
        public async Task MetadataIsPersistedAsJson()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                BlobPath = "/path/to/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };
            await _service.SaveEpisodeMetadataAsync(episode);

            // Act - Read the file directly
            var metadataPath = Path.Combine(_testBasePath, "metadata", "episodes", "test-key.json");
            var json = await File.ReadAllTextAsync(metadataPath);

            // Assert
            json.Should().Contain("CacheKey");
            json.Should().Contain("test-key");
            json.Should().Contain("OriginalUrl");
            json.Should().Contain("https://example.com/audio.mp3");
        }

        [Fact]
        public async Task SupportsMultipleEpisodesAndSummaries()
        {
            // Arrange
            var episode1 = new AudioEpisode { CacheKey = "key1", OriginalUrl = "url1", BlobPath = "path1", DownloadDate = DateTime.UtcNow };
            var episode2 = new AudioEpisode { CacheKey = "key2", OriginalUrl = "url2", BlobPath = "path2", DownloadDate = DateTime.UtcNow };
            var summary1 = new AudioSummary { CacheKey = "key1", TranscriptBlobPath = "t1", SummaryTextBlobPath = "s1", SummaryAudioBlobPath = "a1", ProcessedDate = DateTime.UtcNow };
            var summary2 = new AudioSummary { CacheKey = "key2", TranscriptBlobPath = "t2", SummaryTextBlobPath = "s2", SummaryAudioBlobPath = "a2", ProcessedDate = DateTime.UtcNow };

            // Act
            await _service.SaveEpisodeMetadataAsync(episode1);
            await _service.SaveEpisodeMetadataAsync(episode2);
            await _service.SaveSummaryMetadataAsync("key1", summary1);
            await _service.SaveSummaryMetadataAsync("key2", summary2);

            // Assert
            (await _service.EpisodeExistsAsync("key1")).Should().BeTrue();
            (await _service.EpisodeExistsAsync("key2")).Should().BeTrue();
            (await _service.SummaryExistsAsync("key1")).Should().BeTrue();
            (await _service.SummaryExistsAsync("key2")).Should().BeTrue();

            var retrievedEpisode1 = await _service.GetEpisodeMetadataAsync("key1");
            var retrievedEpisode2 = await _service.GetEpisodeMetadataAsync("key2");
            retrievedEpisode1!.OriginalUrl.Should().Be("url1");
            retrievedEpisode2!.OriginalUrl.Should().Be("url2");
        }

        [Fact]
        public async Task CancellationToken_IsRespectedInAsyncOperations()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                BlobPath = "/path/to/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };
            using var cts = new CancellationTokenSource();

            // Act & Assert - Should complete without cancellation
            await _service.SaveEpisodeMetadataAsync(episode, cts.Token);
            var result = await _service.GetEpisodeMetadataAsync("test-key", cts.Token);
            result.Should().NotBeNull();
        }

        #region Specific Exception Type Tests

        [Fact]
        public async Task GetEpisodeMetadataAsync_WithCorruptedJson_ThrowsJsonException()
        {
            // Arrange
            var cacheKey = "corrupted-json-key";
            var filePath = Path.Combine(_testBasePath, "metadata", "episodes", $"{cacheKey}.json");
            await File.WriteAllTextAsync(filePath, "{ invalid json content {{{}}}");

            // Act & Assert
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => _service.GetEpisodeMetadataAsync(cacheKey));
        }

        [Fact]
        public async Task GetSummaryMetadataAsync_WithCorruptedJson_ThrowsJsonException()
        {
            // Arrange
            var cacheKey = "corrupted-summary-key";
            var filePath = Path.Combine(_testBasePath, "metadata", "summaries", $"{cacheKey}.json");
            await File.WriteAllTextAsync(filePath, "not valid json at all");

            // Act & Assert
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => _service.GetSummaryMetadataAsync(cacheKey));
        }

        #endregion
    }
}
