using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Quintessentia.Models;
using Quintessentia.Services;
using Quintessentia.Services.Contracts;
using System.Net;
using System.Text;

namespace Quintessentia.Tests.Services
{
    public class AudioServiceTests
    {
        private readonly Mock<ILogger<AudioService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IAzureOpenAIService> _azureOpenAIServiceMock;
        private readonly Mock<IStorageService> _storageServiceMock;
        private readonly Mock<IMetadataService> _metadataServiceMock;
        private readonly Mock<IStorageConfiguration> _storageConfigurationMock;
        private readonly Mock<ICacheKeyService> _cacheKeyServiceMock;
        private readonly AudioService _audioService;

        public AudioServiceTests()
        {
            _loggerMock = new Mock<ILogger<AudioService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _azureOpenAIServiceMock = new Mock<IAzureOpenAIService>();
            _storageServiceMock = new Mock<IStorageService>();
            _metadataServiceMock = new Mock<IMetadataService>();
            _storageConfigurationMock = new Mock<IStorageConfiguration>();
            _cacheKeyServiceMock = new Mock<ICacheKeyService>();

            // Setup storage configuration
            _storageConfigurationMock.Setup(c => c.GetContainerName("Episodes")).Returns("episodes");
            _storageConfigurationMock.Setup(c => c.GetContainerName("Transcripts")).Returns("transcripts");
            _storageConfigurationMock.Setup(c => c.GetContainerName("Summaries")).Returns("summaries");

            // Setup cache key service to return predictable keys
            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(It.IsAny<string>()))
                .Returns<string>(url => "testcachekey");

            // Setup default HttpClient for tests that don't need specific HTTP behavior
            var defaultHttpClient = new HttpClient();
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(defaultHttpClient);

            _audioService = new AudioService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _azureOpenAIServiceMock.Object,
                _storageServiceMock.Object,
                _metadataServiceMock.Object,
                _storageConfigurationMock.Object,
                _cacheKeyServiceMock.Object
            );
        }

        #region TC-F-001: Valid MP3 URL download
        [Fact]
        public async Task GetOrDownloadEpisodeAsync_WithValidUrl_DownloadsSuccessfully()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var audioContent = new byte[1024]; // Create a fake audio file with some content
            for (int i = 0; i < audioContent.Length; i++)
            {
                audioContent[i] = (byte)(i % 256);
            }

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(audioContent)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg") }
                    }
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _storageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);
            _metadataServiceMock.Setup(m => m.SaveEpisodeMetadataAsync(It.IsAny<AudioEpisode>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Create a new service instance with the mocked HttpClient
            var audioService = new AudioService(
                _loggerMock.Object,
                httpClientFactoryMock.Object,
                _azureOpenAIServiceMock.Object,
                _storageServiceMock.Object,
                _metadataServiceMock.Object,
                _storageConfigurationMock.Object,
                _cacheKeyServiceMock.Object
            );

            // Act
            var result = await audioService.GetOrDownloadEpisodeAsync(testUrl);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith(".mp3");
            _storageServiceMock.Verify(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _metadataServiceMock.Verify(m => m.SaveEpisodeMetadataAsync(It.IsAny<AudioEpisode>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        #endregion

        #region TC-F-002: Cache hit scenario
        [Fact]
        public async Task GetOrDownloadEpisodeAsync_WithCachedEpisode_RetrievesFromCache()
        {
            // Arrange
            var testUrl = "https://example.com/cached.mp3";
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _audioService.GetOrDownloadEpisodeAsync(testUrl);

            // Assert
            result.Should().NotBeNullOrEmpty();
            _storageServiceMock.Verify(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _storageServiceMock.Verify(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        #endregion

        #region TC-F-003: Invalid URL format
        [Fact]
        public async Task GetOrDownloadEpisodeAsync_WithNullUrl_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _audioService.GetOrDownloadEpisodeAsync(null!));
        }

        [Fact]
        public async Task GetOrDownloadEpisodeAsync_WithEmptyUrl_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _audioService.GetOrDownloadEpisodeAsync(""));
        }
        #endregion

        #region TC-F-004: Non-existent URL (404)
        [Fact]
        public async Task DownloadEpisodeAsync_WithNonExistentUrl_ThrowsHttpRequestException()
        {
            // Arrange
            var testUrl = "https://example.com/notfound.mp3";
            var cacheKey = "testcachekey";
            
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _audioService.DownloadEpisodeAsync(testUrl, cacheKey));
        }
        #endregion

        #region TC-F-007: Cache key generation
        [Fact]
        public async Task IsEpisodeCachedAsync_GeneratesSameKeyForSameUrl()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result1 = await _audioService.IsEpisodeCachedAsync(testUrl);
            var result2 = await _audioService.IsEpisodeCachedAsync(testUrl);

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeTrue();
        }
        #endregion

        #region TC-F-008: Full pipeline execution
        [Fact]
        public async Task ProcessAndSummarizeEpisodeAsync_FullPipeline_CompletesSuccessfully()
        {
            // Arrange
            var episodeId = "testcachekey";
            var transcript = "This is a test transcript with many words.";
            var summary = "This is a summary.";

            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, CancellationToken>((container, blob, localPath, ct) => {
                    // Create the file so it exists when the service checks for it
                    File.WriteAllText(localPath, "fake audio data");
                })
                .Returns(Task.CompletedTask);
            _azureOpenAIServiceMock.Setup(a => a.TranscribeAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(transcript);
            _azureOpenAIServiceMock.Setup(a => a.SummarizeTranscriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(summary);
            _azureOpenAIServiceMock.Setup(a => a.GenerateSpeechAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);
            _storageServiceMock.Setup(s => s.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);
            _storageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);
            _metadataServiceMock.Setup(m => m.SaveSummaryMetadataAsync(It.IsAny<string>(), It.IsAny<AudioSummary>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _audioService.ProcessAndSummarizeEpisodeAsync(episodeId);

            // Assert
            result.Should().NotBeNullOrEmpty();
            _azureOpenAIServiceMock.Verify(a => a.TranscribeAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _azureOpenAIServiceMock.Verify(a => a.SummarizeTranscriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _azureOpenAIServiceMock.Verify(a => a.GenerateSpeechAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _metadataServiceMock.Verify(m => m.SaveSummaryMetadataAsync(It.IsAny<string>(), It.IsAny<AudioSummary>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        #endregion

        #region TC-F-012: Summary cache hit
        [Fact]
        public async Task ProcessAndSummarizeEpisodeAsync_WithCachedSummary_RetrievesFromCache()
        {
            // Arrange
            var episodeId = "testcachekey";
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _audioService.ProcessAndSummarizeEpisodeAsync(episodeId);

            // Assert
            result.Should().NotBeNullOrEmpty();
            _storageServiceMock.Verify(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _azureOpenAIServiceMock.Verify(a => a.TranscribeAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _azureOpenAIServiceMock.Verify(a => a.SummarizeTranscriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        #endregion

        #region TC-F-021: Real-time progress updates
        [Fact]
        public async Task ProcessAndSummarizeEpisodeAsync_WithProgressCallback_SendsUpdates()
        {
            // Arrange
            var episodeId = "testcachekey";
            var progressUpdates = new List<ProcessingStatus>();
            var transcript = "Test transcript";
            var summary = "Test summary";

            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, CancellationToken>((container, blob, localPath, ct) => {
                    File.WriteAllText(localPath, "fake audio data");
                })
                .Returns(Task.CompletedTask);
            _azureOpenAIServiceMock.Setup(a => a.TranscribeAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(transcript);
            _azureOpenAIServiceMock.Setup(a => a.SummarizeTranscriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(summary);
            _azureOpenAIServiceMock.Setup(a => a.GenerateSpeechAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);
            _storageServiceMock.Setup(s => s.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);
            _storageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);
            _metadataServiceMock.Setup(m => m.SaveSummaryMetadataAsync(It.IsAny<string>(), It.IsAny<AudioSummary>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _audioService.ProcessAndSummarizeEpisodeAsync(episodeId, status => progressUpdates.Add(status), CancellationToken.None);

            // Assert
            progressUpdates.Should().NotBeEmpty();
            progressUpdates.Should().Contain(s => s.Stage == "transcribing");
            progressUpdates.Should().Contain(s => s.Stage == "summarizing");
            progressUpdates.Should().Contain(s => s.Stage == "generating-speech");
            progressUpdates.Should().Contain(s => s.Stage == "complete");
        }
        #endregion

        #region TC-F-013: Azure OpenAI API failure
        [Fact]
        public async Task ProcessAndSummarizeEpisodeAsync_WhenTranscriptionFails_ThrowsException()
        {
            // Arrange
            var episodeId = "testcachekey";
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, CancellationToken>((container, blob, localPath, ct) => {
                    File.WriteAllText(localPath, "fake audio data");
                })
                .Returns(Task.CompletedTask);
            _azureOpenAIServiceMock.Setup(a => a.TranscribeAudioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API Error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _audioService.ProcessAndSummarizeEpisodeAsync(episodeId));
        }
        #endregion

        #region TC-F-019: IsSummaryCachedAsync
        [Fact]
        public async Task IsSummaryCachedAsync_WithCachedSummary_ReturnsTrue()
        {
            // Arrange
            var episodeId = "testcachekey";
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _audioService.IsSummaryCachedAsync(episodeId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsSummaryCachedAsync_WithoutCachedSummary_ReturnsFalse()
        {
            // Arrange
            var episodeId = "testcachekey";
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await _audioService.IsSummaryCachedAsync(episodeId);

            // Assert
            result.Should().BeFalse();
        }
        #endregion

        #region TC-F-022: Cancellation Token Support
        [Fact]
        public async Task GetOrDownloadEpisodeAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                _audioService.GetOrDownloadEpisodeAsync(testUrl, cts.Token));
        }

        [Fact]
        public async Task ProcessAndSummarizeEpisodeAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            var episodeId = "testcachekey";
            var cts = new CancellationTokenSource();
            
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                _audioService.ProcessAndSummarizeEpisodeAsync(episodeId, null, cts.Token));
        }
        #endregion

        #region GetCachedEpisodePath Tests

        [Fact]
        public void GetCachedEpisodePath_ReturnsCorrectTempPath()
        {
            // Arrange
            var episodeId = "https://example.com/test.mp3";
            var expectedCacheKey = "test-cache-key";

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(episodeId)).Returns(expectedCacheKey);

            // Act
            var result = _audioService.GetCachedEpisodePath(episodeId);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain(expectedCacheKey);
            result.Should().EndWith(".mp3");
            result.Should().Contain("audio_");
        }

        [Fact]
        public void GetCachedEpisodePath_WithDifferentUrls_ReturnsDifferentPaths()
        {
            // Arrange
            var url1 = "https://example.com/episode1.mp3";
            var url2 = "https://example.com/episode2.mp3";

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(url1)).Returns("cache-key-1");
            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(url2)).Returns("cache-key-2");

            // Act
            var path1 = _audioService.GetCachedEpisodePath(url1);
            var path2 = _audioService.GetCachedEpisodePath(url2);

            // Assert
            path1.Should().NotBe(path2);
        }

        #endregion

        #region GetSummaryPath Tests

        [Fact]
        public void GetSummaryPath_ReturnsCorrectTempPath()
        {
            // Arrange
            var episodeId = "https://example.com/test.mp3";
            var expectedCacheKey = "test-cache-key";

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(episodeId)).Returns(expectedCacheKey);

            // Act
            var result = _audioService.GetSummaryPath(episodeId);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain(expectedCacheKey);
            result.Should().EndWith("_summary.mp3");
            result.Should().Contain("audio_");
        }

        [Fact]
        public void GetSummaryPath_WithDifferentUrls_ReturnsDifferentPaths()
        {
            // Arrange
            var url1 = "https://example.com/episode1.mp3";
            var url2 = "https://example.com/episode2.mp3";

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(url1)).Returns("cache-key-1");
            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(url2)).Returns("cache-key-2");

            // Act
            var path1 = _audioService.GetSummaryPath(url1);
            var path2 = _audioService.GetSummaryPath(url2);

            // Assert
            path1.Should().NotBe(path2);
        }

        [Fact]
        public void GetSummaryPath_DifferentFromEpisodePath()
        {
            // Arrange
            var episodeId = "https://example.com/test.mp3";
            var cacheKey = "test-cache-key";

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(episodeId)).Returns(cacheKey);

            // Act
            var episodePath = _audioService.GetCachedEpisodePath(episodeId);
            var summaryPath = _audioService.GetSummaryPath(episodeId);

            // Assert
            episodePath.Should().NotBe(summaryPath);
            episodePath.Should().EndWith(".mp3");
            summaryPath.Should().EndWith("_summary.mp3");
        }

        #endregion
    }
}
