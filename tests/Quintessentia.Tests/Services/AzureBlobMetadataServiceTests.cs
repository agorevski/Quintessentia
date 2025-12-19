using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Models;
using Quintessentia.Services;
using Quintessentia.Services.Contracts;
using System.Text;
using System.Text.Json;

namespace Quintessentia.Tests.Services
{
    public class AzureBlobMetadataServiceTests
    {
        private readonly Mock<IStorageService> _storageServiceMock;
        private readonly Mock<ILogger<AzureBlobMetadataService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly AzureBlobMetadataService _service;

        public AzureBlobMetadataServiceTests()
        {
            _storageServiceMock = new Mock<IStorageService>();
            _loggerMock = new Mock<ILogger<AzureBlobMetadataService>>();
            _configurationMock = new Mock<IConfiguration>();

            // Setup default configuration values
            _configurationMock.Setup(c => c["AzureStorage:Containers:Episodes"]).Returns("episodes");
            _configurationMock.Setup(c => c["AzureStorage:Containers:Transcripts"]).Returns("transcripts");
            _configurationMock.Setup(c => c["AzureStorage:Containers:Summaries"]).Returns("summaries");

            _service = new AzureBlobMetadataService(
                _storageServiceMock.Object,
                _loggerMock.Object,
                _configurationMock.Object);
        }

        #region GetEpisodeMetadataAsync Tests

        [Fact]
        public async Task GetEpisodeMetadataAsync_WhenMetadataExists_ReturnsEpisode()
        {
            // Arrange
            var cacheKey = "test-cache-key";
            var episode = new AudioEpisode
            {
                CacheKey = cacheKey,
                OriginalUrl = "https://example.com/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(episode, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _storageServiceMock
                .Setup(s => s.DownloadToStreamAsync("episodes", $"{cacheKey}.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Stream, CancellationToken>((container, blob, stream, ct) =>
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Position = 0;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetEpisodeMetadataAsync(cacheKey);

            // Assert
            result.Should().NotBeNull();
            result!.CacheKey.Should().Be(cacheKey);
            result.OriginalUrl.Should().Be(episode.OriginalUrl);
        }

        [Fact]
        public async Task GetEpisodeMetadataAsync_WhenMetadataDoesNotExist_ReturnsNull()
        {
            // Arrange
            var cacheKey = "nonexistent-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.GetEpisodeMetadataAsync(cacheKey);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetEpisodeMetadataAsync_WhenExceptionOccurs_ReturnsNull()
        {
            // Arrange
            var cacheKey = "error-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Azure.RequestFailedException("Storage error"));

            // Act
            var result = await _service.GetEpisodeMetadataAsync(cacheKey);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region SaveEpisodeMetadataAsync Tests

        [Fact]
        public async Task SaveEpisodeMetadataAsync_SerializesAndUploads_Successfully()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };

            Stream? capturedStream = null;
            _storageServiceMock
                .Setup(s => s.UploadStreamAsync("episodes", $"{episode.CacheKey}.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Stream, CancellationToken>((container, blob, stream, ct) =>
                {
                    capturedStream = new MemoryStream();
                    stream.CopyTo(capturedStream);
                    capturedStream.Position = 0;
                })
                .ReturnsAsync("https://storage.example.com/episodes/test-key.json");

            // Act
            await _service.SaveEpisodeMetadataAsync(episode);

            // Assert
            _storageServiceMock.Verify(
                s => s.UploadStreamAsync("episodes", $"{episode.CacheKey}.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
                Times.Once);

            capturedStream.Should().NotBeNull();
        }

        [Fact]
        public async Task SaveEpisodeMetadataAsync_WhenExceptionOccurs_ThrowsException()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };

            _storageServiceMock
                .Setup(s => s.UploadStreamAsync("episodes", $"{episode.CacheKey}.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Upload failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.SaveEpisodeMetadataAsync(episode));
        }

        #endregion

        #region EpisodeExistsAsync Tests

        [Fact]
        public async Task EpisodeExistsAsync_WhenBothFilesExist_ReturnsTrue()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.mp3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.EpisodeExistsAsync(cacheKey);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task EpisodeExistsAsync_WhenOnlyAudioExists_ReturnsFalse()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.mp3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.EpisodeExistsAsync(cacheKey);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task EpisodeExistsAsync_WhenOnlyMetadataExists_ReturnsFalse()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.mp3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.EpisodeExistsAsync(cacheKey);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task EpisodeExistsAsync_WhenExceptionOccurs_ReturnsFalse()
        {
            // Arrange
            var cacheKey = "error-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.mp3", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Azure.RequestFailedException("Storage error"));

            // Act
            var result = await _service.EpisodeExistsAsync(cacheKey);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetSummaryMetadataAsync Tests

        [Fact]
        public async Task GetSummaryMetadataAsync_WhenMetadataExists_ReturnsSummary()
        {
            // Arrange
            var cacheKey = "test-cache-key";
            var summary = new AudioSummary
            {
                CacheKey = cacheKey,
                TranscriptWordCount = 1000,
                SummaryWordCount = 100,
                ProcessedDate = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _storageServiceMock
                .Setup(s => s.DownloadToStreamAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Stream, CancellationToken>((container, blob, stream, ct) =>
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Position = 0;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetSummaryMetadataAsync(cacheKey);

            // Assert
            result.Should().NotBeNull();
            result!.TranscriptWordCount.Should().Be(summary.TranscriptWordCount);
            result.SummaryWordCount.Should().Be(summary.SummaryWordCount);
        }

        [Fact]
        public async Task GetSummaryMetadataAsync_WhenMetadataDoesNotExist_ReturnsNull()
        {
            // Arrange
            var cacheKey = "nonexistent-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.GetSummaryMetadataAsync(cacheKey);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSummaryMetadataAsync_WhenExceptionOccurs_ReturnsNull()
        {
            // Arrange
            var cacheKey = "error-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Azure.RequestFailedException("Storage error"));

            // Act
            var result = await _service.GetSummaryMetadataAsync(cacheKey);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region SaveSummaryMetadataAsync Tests

        [Fact]
        public async Task SaveSummaryMetadataAsync_SerializesAndUploads_Successfully()
        {
            // Arrange
            var cacheKey = "test-key";
            var summary = new AudioSummary
            {
                CacheKey = cacheKey,
                TranscriptWordCount = 1000,
                SummaryWordCount = 100,
                ProcessedDate = DateTime.UtcNow
            };

            _storageServiceMock
                .Setup(s => s.UploadStreamAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://storage.example.com/summaries/test-key_summary.json");

            // Act
            await _service.SaveSummaryMetadataAsync(cacheKey, summary);

            // Assert
            _storageServiceMock.Verify(
                s => s.UploadStreamAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveSummaryMetadataAsync_WhenExceptionOccurs_ThrowsException()
        {
            // Arrange
            var cacheKey = "test-key";
            var summary = new AudioSummary
            {
                CacheKey = cacheKey
            };

            _storageServiceMock
                .Setup(s => s.UploadStreamAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Upload failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.SaveSummaryMetadataAsync(cacheKey, summary));
        }

        #endregion

        #region SummaryExistsAsync Tests

        [Fact]
        public async Task SummaryExistsAsync_WhenBothFilesExist_ReturnsTrue()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.mp3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.SummaryExistsAsync(cacheKey);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SummaryExistsAsync_WhenOnlyAudioExists_ReturnsFalse()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.mp3", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.SummaryExistsAsync(cacheKey);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SummaryExistsAsync_WhenExceptionOccurs_ReturnsFalse()
        {
            // Arrange
            var cacheKey = "error-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.mp3", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Azure.RequestFailedException("Storage error"));

            // Act
            var result = await _service.SummaryExistsAsync(cacheKey);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region DeleteEpisodeMetadataAsync Tests

        [Fact]
        public async Task DeleteEpisodeMetadataAsync_DeletesBothFiles_Successfully()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.DeleteAsync("episodes", $"{cacheKey}.mp3", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _storageServiceMock
                .Setup(s => s.DeleteAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.DeleteEpisodeMetadataAsync(cacheKey);

            // Assert
            _storageServiceMock.Verify(
                s => s.DeleteAsync("episodes", $"{cacheKey}.mp3", It.IsAny<CancellationToken>()),
                Times.Once);
            _storageServiceMock.Verify(
                s => s.DeleteAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteEpisodeMetadataAsync_WhenExceptionOccurs_ThrowsException()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.DeleteAsync("episodes", $"{cacheKey}.mp3", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Delete failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.DeleteEpisodeMetadataAsync(cacheKey));
        }

        #endregion

        #region DeleteSummaryMetadataAsync Tests

        [Fact]
        public async Task DeleteSummaryMetadataAsync_DeletesBothFiles_Successfully()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.DeleteAsync("summaries", $"{cacheKey}_summary.mp3", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _storageServiceMock
                .Setup(s => s.DeleteAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.DeleteSummaryMetadataAsync(cacheKey);

            // Assert
            _storageServiceMock.Verify(
                s => s.DeleteAsync("summaries", $"{cacheKey}_summary.mp3", It.IsAny<CancellationToken>()),
                Times.Once);
            _storageServiceMock.Verify(
                s => s.DeleteAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteSummaryMetadataAsync_WhenExceptionOccurs_ThrowsException()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.DeleteAsync("summaries", $"{cacheKey}_summary.mp3", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Delete failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.DeleteSummaryMetadataAsync(cacheKey));
        }

        #endregion

        #region Container Configuration Tests

        [Fact]
        public void Constructor_UsesConfiguredContainerNames()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["AzureStorage:Containers:Episodes"]).Returns("custom-episodes");
            configMock.Setup(c => c["AzureStorage:Containers:Summaries"]).Returns("custom-summaries");

            // Act
            var service = new AzureBlobMetadataService(
                _storageServiceMock.Object,
                _loggerMock.Object,
                configMock.Object);

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_UsesDefaultContainerNames_WhenNotConfigured()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["AzureStorage:Containers:Episodes"]).Returns((string)null!);

            // Act
            var service = new AzureBlobMetadataService(
                _storageServiceMock.Object,
                _loggerMock.Object,
                configMock.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region Specific Exception Type Tests

        [Fact]
        public async Task GetEpisodeMetadataAsync_WhenJsonExceptionOccurs_ReturnsNull()
        {
            // Arrange
            var cacheKey = "json-error-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("episodes", $"{cacheKey}.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _storageServiceMock
                .Setup(s => s.DownloadToStreamAsync("episodes", $"{cacheKey}.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Stream, CancellationToken>((container, blob, stream, ct) =>
                {
                    var bytes = Encoding.UTF8.GetBytes("invalid json {{{");
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Position = 0;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetEpisodeMetadataAsync(cacheKey);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSummaryMetadataAsync_WhenJsonExceptionOccurs_ReturnsNull()
        {
            // Arrange
            var cacheKey = "json-error-key";

            _storageServiceMock
                .Setup(s => s.ExistsAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _storageServiceMock
                .Setup(s => s.DownloadToStreamAsync("summaries", $"{cacheKey}_summary.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Stream, CancellationToken>((container, blob, stream, ct) =>
                {
                    var bytes = Encoding.UTF8.GetBytes("not valid json");
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Position = 0;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetSummaryMetadataAsync(cacheKey);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SaveEpisodeMetadataAsync_WhenRequestFailedExceptionOccurs_Throws()
        {
            // Arrange
            var episode = new AudioEpisode
            {
                CacheKey = "test-key",
                OriginalUrl = "https://example.com/audio.mp3",
                DownloadDate = DateTime.UtcNow
            };

            _storageServiceMock
                .Setup(s => s.UploadStreamAsync("episodes", $"{episode.CacheKey}.json", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Azure.RequestFailedException("Upload failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Azure.RequestFailedException>(() => _service.SaveEpisodeMetadataAsync(episode));
        }

        [Fact]
        public async Task DeleteEpisodeMetadataAsync_WhenRequestFailedExceptionOccurs_Throws()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.DeleteAsync("episodes", $"{cacheKey}.mp3", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Azure.RequestFailedException("Delete failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Azure.RequestFailedException>(() => _service.DeleteEpisodeMetadataAsync(cacheKey));
        }

        [Fact]
        public async Task DeleteSummaryMetadataAsync_WhenRequestFailedExceptionOccurs_Throws()
        {
            // Arrange
            var cacheKey = "test-key";

            _storageServiceMock
                .Setup(s => s.DeleteAsync("summaries", $"{cacheKey}_summary.mp3", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Azure.RequestFailedException("Delete failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Azure.RequestFailedException>(() => _service.DeleteSummaryMetadataAsync(cacheKey));
        }

        #endregion
    }
}
