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
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly AudioService _audioService;

        public AudioServiceTests()
        {
            _loggerMock = new Mock<ILogger<AudioService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _azureOpenAIServiceMock = new Mock<IAzureOpenAIService>();
            _storageServiceMock = new Mock<IStorageService>();
            _metadataServiceMock = new Mock<IMetadataService>();
            _configurationMock = new Mock<IConfiguration>();

            // Setup default configuration
            _configurationMock.Setup(c => c["AzureStorage:Containers:Episodes"]).Returns("episodes");
            _configurationMock.Setup(c => c["AzureStorage:Containers:Transcripts"]).Returns("transcripts");
            _configurationMock.Setup(c => c["AzureStorage:Containers:Summaries"]).Returns("summaries");

            // Setup default HttpClient for tests that don't need specific HTTP behavior
            var defaultHttpClient = new HttpClient();
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(defaultHttpClient);

            _audioService = new AudioService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _azureOpenAIServiceMock.Object,
                _storageServiceMock.Object,
                _metadataServiceMock.Object,
                _configurationMock.Object
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
            
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _storageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(string.Empty);
            _metadataServiceMock.Setup(m => m.SaveEpisodeMetadataAsync(It.IsAny<AudioEpisode>()))
                .Returns(Task.CompletedTask);

            // Create a new service instance with the mocked HttpClient
            var audioService = new AudioService(
                _loggerMock.Object,
                httpClientFactoryMock.Object,
                _azureOpenAIServiceMock.Object,
                _storageServiceMock.Object,
                _metadataServiceMock.Object,
                _configurationMock.Object
            );

            // Act
            var result = await audioService.GetOrDownloadEpisodeAsync(testUrl);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith(".mp3");
            _storageServiceMock.Verify(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _metadataServiceMock.Verify(m => m.SaveEpisodeMetadataAsync(It.IsAny<AudioEpisode>()), Times.Once);
        }
        #endregion

        #region TC-F-002: Cache hit scenario
        [Fact]
        public async Task GetOrDownloadEpisodeAsync_WithCachedEpisode_RetrievesFromCache()
        {
            // Arrange
            var testUrl = "https://example.com/cached.mp3";
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _audioService.GetOrDownloadEpisodeAsync(testUrl);

            // Assert
            result.Should().NotBeNullOrEmpty();
            _storageServiceMock.Verify(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _storageServiceMock.Verify(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
        public void IsEpisodeCached_GeneratesSameKeyForSameUrl()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

            // Act
            var result1 = _audioService.IsEpisodeCached(testUrl);
            var result2 = _audioService.IsEpisodeCached(testUrl);

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

            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((container, blob, localPath) => {
                    // Create the file so it exists when the service checks for it
                    File.WriteAllText(localPath, "fake audio data");
                })
                .Returns(Task.CompletedTask);
            _azureOpenAIServiceMock.Setup(a => a.TranscribeAudioAsync(It.IsAny<string>()))
                .ReturnsAsync(transcript);
            _azureOpenAIServiceMock.Setup(a => a.SummarizeTranscriptAsync(It.IsAny<string>()))
                .ReturnsAsync(summary);
            _azureOpenAIServiceMock.Setup(a => a.GenerateSpeechAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(string.Empty);
            _storageServiceMock.Setup(s => s.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()))
                .ReturnsAsync(string.Empty);
            _storageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(string.Empty);
            _metadataServiceMock.Setup(m => m.SaveSummaryMetadataAsync(It.IsAny<string>(), It.IsAny<AudioSummary>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _audioService.ProcessAndSummarizeEpisodeAsync(episodeId);

            // Assert
            result.Should().NotBeNullOrEmpty();
            _azureOpenAIServiceMock.Verify(a => a.TranscribeAudioAsync(It.IsAny<string>()), Times.Once);
            _azureOpenAIServiceMock.Verify(a => a.SummarizeTranscriptAsync(It.IsAny<string>()), Times.Once);
            _azureOpenAIServiceMock.Verify(a => a.GenerateSpeechAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _metadataServiceMock.Verify(m => m.SaveSummaryMetadataAsync(It.IsAny<string>(), It.IsAny<AudioSummary>()), Times.Once);
        }
        #endregion

        #region TC-F-012: Summary cache hit
        [Fact]
        public async Task ProcessAndSummarizeEpisodeAsync_WithCachedSummary_RetrievesFromCache()
        {
            // Arrange
            var episodeId = "testcachekey";
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _audioService.ProcessAndSummarizeEpisodeAsync(episodeId);

            // Assert
            result.Should().NotBeNullOrEmpty();
            _storageServiceMock.Verify(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _azureOpenAIServiceMock.Verify(a => a.TranscribeAudioAsync(It.IsAny<string>()), Times.Never);
            _azureOpenAIServiceMock.Verify(a => a.SummarizeTranscriptAsync(It.IsAny<string>()), Times.Never);
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

            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((container, blob, localPath) => {
                    File.WriteAllText(localPath, "fake audio data");
                })
                .Returns(Task.CompletedTask);
            _azureOpenAIServiceMock.Setup(a => a.TranscribeAudioAsync(It.IsAny<string>()))
                .ReturnsAsync(transcript);
            _azureOpenAIServiceMock.Setup(a => a.SummarizeTranscriptAsync(It.IsAny<string>()))
                .ReturnsAsync(summary);
            _azureOpenAIServiceMock.Setup(a => a.GenerateSpeechAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(string.Empty);
            _storageServiceMock.Setup(s => s.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()))
                .ReturnsAsync(string.Empty);
            _storageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(string.Empty);
            _metadataServiceMock.Setup(m => m.SaveSummaryMetadataAsync(It.IsAny<string>(), It.IsAny<AudioSummary>()))
                .Returns(Task.CompletedTask);

            // Act
            await _audioService.ProcessAndSummarizeEpisodeAsync(episodeId, status => progressUpdates.Add(status));

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
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((container, blob, localPath) => {
                    File.WriteAllText(localPath, "fake audio data");
                })
                .Returns(Task.CompletedTask);
            _azureOpenAIServiceMock.Setup(a => a.TranscribeAudioAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("API Error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _audioService.ProcessAndSummarizeEpisodeAsync(episodeId));
        }
        #endregion

        #region TC-F-019: IsSummaryCached
        [Fact]
        public void IsSummaryCached_WithCachedSummary_ReturnsTrue()
        {
            // Arrange
            var episodeId = "testcachekey";
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

            // Act
            var result = _audioService.IsSummaryCached(episodeId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsSummaryCached_WithoutCachedSummary_ReturnsFalse()
        {
            // Arrange
            var episodeId = "testcachekey";
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

            // Act
            var result = _audioService.IsSummaryCached(episodeId);

            // Assert
            result.Should().BeFalse();
        }
        #endregion
    }
}
