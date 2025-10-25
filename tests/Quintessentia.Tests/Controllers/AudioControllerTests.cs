using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Controllers;
using Quintessentia.Models;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Tests.Controllers
{
    public class AudioControllerTests
    {
        private readonly Mock<IAudioService> _audioServiceMock;
        private readonly Mock<IStorageService> _storageServiceMock;
        private readonly Mock<IMetadataService> _metadataServiceMock;
        private readonly Mock<ILogger<AudioController>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly AudioController _controller;

        public AudioControllerTests()
        {
            _audioServiceMock = new Mock<IAudioService>();
            _storageServiceMock = new Mock<IStorageService>();
            _metadataServiceMock = new Mock<IMetadataService>();
            _loggerMock = new Mock<ILogger<AudioController>>();
            _configurationMock = new Mock<IConfiguration>();

            // Setup default configuration
            _configurationMock.Setup(c => c["AzureStorage:Containers:Episodes"]).Returns("episodes");
            _configurationMock.Setup(c => c["AzureStorage:Containers:Transcripts"]).Returns("transcripts");
            _configurationMock.Setup(c => c["AzureStorage:Containers:Summaries"]).Returns("summaries");

            _controller = new AudioController(
                _audioServiceMock.Object,
                _storageServiceMock.Object,
                _metadataServiceMock.Object,
                _loggerMock.Object,
                _configurationMock.Object
            );

            // Setup HttpContext for controller
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        #region TC-F-001: Valid MP3 URL download via Process endpoint
        [Fact]
        public async Task Process_WithValidUrl_ReturnsSuccessResult()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var cacheKey = "testcachekey123";
            _audioServiceMock.Setup(a => a.IsEpisodeCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl))
                .ReturnsAsync("/temp/path/audio.mp3");

            // Act
            var result = await _controller.Process(testUrl);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Result");
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.Success.Should().BeTrue();
            model.Message.Should().Contain("downloaded");
        }
        #endregion

        #region TC-F-002: Cache hit via Process endpoint
        [Fact]
        public async Task Process_WithCachedEpisode_ReturnsSuccessWithCacheMessage()
        {
            // Arrange
            var testUrl = "https://example.com/cached.mp3";
            _audioServiceMock.Setup(a => a.IsEpisodeCached(It.IsAny<string>())).Returns(true);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl))
                .ReturnsAsync("/temp/path/cached.mp3");

            // Act
            var result = await _controller.Process(testUrl);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.Success.Should().BeTrue();
            model.Message.Should().Contain("cache");
            model.WasCached.Should().BeTrue();
        }
        #endregion

        #region TC-F-003: Invalid URL format
        [Fact]
        public async Task Process_WithNullUrl_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Process(null!);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Process_WithEmptyUrl_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Process("");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Process_WithInvalidUrlFormat_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Process("not-a-valid-url");

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Invalid URL format. Please provide a valid HTTP or HTTPS URL.");
        }
        #endregion

        #region TC-F-004: Non-existent URL (404)
        [Fact]
        public async Task Process_WithNonExistentUrl_ReturnsErrorView()
        {
            // Arrange
            var testUrl = "https://example.com/notfound.mp3";
            _audioServiceMock.Setup(a => a.IsEpisodeCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl))
                .ThrowsAsync(new HttpRequestException("404 Not Found"));

            // Act
            var result = await _controller.Process(testUrl);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
            var model = viewResult.Model.Should().BeOfType<ErrorViewModel>().Subject;
            model.Message.Should().Contain("Failed to download");
        }
        #endregion

        #region TC-F-008: Full pipeline via ProcessAndSummarize
        [Fact]
        public async Task ProcessAndSummarize_WithValidUrl_ReturnsCompleteResult()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var summaryPath = "/temp/summary.mp3";
            _audioServiceMock.Setup(a => a.IsEpisodeCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.IsSummaryCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl))
                .ReturnsAsync("/temp/episode.mp3");
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>()))
                .ReturnsAsync(summaryPath);

            // Act
            var result = await _controller.ProcessAndSummarize(testUrl);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Result");
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.Success.Should().BeTrue();
            model.SummaryAudioPath.Should().Be(summaryPath);
        }
        #endregion

        #region TC-F-015-020: Custom settings override
        [Fact]
        public async Task ProcessAndSummarize_WithCustomEndpoint_StoresInHttpContext()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var customEndpoint = "https://custom.openai.azure.com/";
            _audioServiceMock.Setup(a => a.IsEpisodeCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.IsSummaryCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl))
                .ReturnsAsync("/temp/episode.mp3");
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>()))
                .ReturnsAsync("/temp/summary.mp3");

            // Act
            var result = await _controller.ProcessAndSummarize(
                testUrl,
                settingsEndpoint: customEndpoint
            );

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.Success.Should().BeTrue();
            
            // Verify custom settings were stored in HttpContext
            _controller.HttpContext.Items.Should().ContainKey("AzureOpenAISettings");
            var settings = _controller.HttpContext.Items["AzureOpenAISettings"] as AzureOpenAISettings;
            settings.Should().NotBeNull();
            settings!.Endpoint.Should().Be(customEndpoint);
        }

        [Fact]
        public async Task ProcessAndSummarize_WithCustomTtsSpeed_StoresInHttpContext()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var customSpeed = 1.5f;
            _audioServiceMock.Setup(a => a.IsEpisodeCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.IsSummaryCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl))
                .ReturnsAsync("/temp/episode.mp3");
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>()))
                .ReturnsAsync("/temp/summary.mp3");

            // Act
            var result = await _controller.ProcessAndSummarize(
                testUrl,
                settingsTtsSpeedRatio: customSpeed
            );

            // Assert
            var settings = _controller.HttpContext.Items["AzureOpenAISettings"] as AzureOpenAISettings;
            settings.Should().NotBeNull();
            settings!.TtsSpeedRatio.Should().Be(customSpeed);
        }
        #endregion

        #region TC-F-029-033: Result page functionality
        [Fact]
        public async Task Result_WithValidEpisodeId_ReturnsResultView()
        {
            // Arrange
            var episodeId = "testcachekey";
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(episodeId)).ReturnsAsync(true);
            _audioServiceMock.Setup(a => a.IsSummaryCached(episodeId)).Returns(false);

            // Act
            var result = await _controller.Result(episodeId);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.Success.Should().BeTrue();
            model.EpisodeId.Should().Be(episodeId);
        }

        [Fact]
        public async Task Result_WithNonExistentEpisode_ReturnsNotFound()
        {
            // Arrange
            var episodeId = "nonexistent";
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(episodeId)).ReturnsAsync(false);

            // Act
            var result = await _controller.Result(episodeId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Result_WithSummary_DisplaysSummaryInfo()
        {
            // Arrange
            var episodeId = "testcachekey";
            var summaryMetadata = new AudioSummary
            {
                CacheKey = episodeId,
                TranscriptWordCount = 1000,
                SummaryWordCount = 200,
                SummaryTextBlobPath = "transcripts/test_summary.txt",
                ProcessedDate = DateTime.UtcNow
            };

            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(episodeId)).ReturnsAsync(true);
            _audioServiceMock.Setup(a => a.IsSummaryCached(episodeId)).Returns(true);
            _metadataServiceMock.Setup(m => m.GetSummaryMetadataAsync(episodeId))
                .ReturnsAsync(summaryMetadata);
            
            var summaryText = "This is the summary text";
            var summaryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(summaryText));
            _storageServiceMock.Setup(s => s.DownloadToStreamAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<Stream>()))
                .Callback<string, string, Stream>((container, blob, stream) =>
                {
                    var data = System.Text.Encoding.UTF8.GetBytes(summaryText);
                    stream.Write(data, 0, data.Length);
                    stream.Position = 0;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Result(episodeId);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.SummaryWasCached.Should().BeTrue();
            model.TranscriptWordCount.Should().Be(1000);
            model.SummaryWordCount.Should().Be(200);
            model.SummaryText.Should().Be(summaryText);
        }
        #endregion

        #region TC-F-033: Download summary file
        [Fact]
        public async Task DownloadSummary_WithValidEpisodeId_ReturnsFileStream()
        {
            // Arrange
            var episodeId = "testcachekey";
            var audioData = new byte[] { 1, 2, 3, 4, 5 };
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(episodeId)).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>()))
                .Callback<string, string, Stream>((container, blob, stream) =>
                {
                    stream.Write(audioData, 0, audioData.Length);
                    stream.Position = 0;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DownloadSummary(episodeId);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("audio/mpeg");
            fileResult.FileDownloadName.Should().Contain(".mp3");
        }

        [Fact]
        public async Task DownloadSummary_WithNonExistentSummary_ReturnsNotFound()
        {
            // Arrange
            var episodeId = "nonexistent";
            _metadataServiceMock.Setup(m => m.SummaryExistsAsync(episodeId)).ReturnsAsync(false);

            // Act
            var result = await _controller.DownloadSummary(episodeId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }
        #endregion

        #region TC-F-033: Download episode file
        [Fact]
        public async Task Download_WithValidEpisodeId_ReturnsFileStream()
        {
            // Arrange
            var episodeId = "testcachekey";
            var audioData = new byte[] { 1, 2, 3, 4, 5 };
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(episodeId)).ReturnsAsync(true);
            _storageServiceMock.Setup(s => s.DownloadToStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>()))
                .Callback<string, string, Stream>((container, blob, stream) =>
                {
                    stream.Write(audioData, 0, audioData.Length);
                    stream.Position = 0;
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Download(episodeId);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("audio/mpeg");
        }

        [Fact]
        public async Task Download_WithNonExistentEpisode_ReturnsNotFound()
        {
            // Arrange
            var episodeId = "nonexistent";
            _metadataServiceMock.Setup(m => m.EpisodeExistsAsync(episodeId)).ReturnsAsync(false);

            // Act
            var result = await _controller.Download(episodeId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }
        #endregion

        #region Error handling
        [Fact]
        public async Task Process_WhenServiceThrows_ReturnsErrorView()
        {
            // Arrange
            var testUrl = "https://example.com/error.mp3";
            _audioServiceMock.Setup(a => a.IsEpisodeCached(It.IsAny<string>())).Returns(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.Process(testUrl);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
        }

        [Fact]
        public async Task ProcessAndSummarize_WhenServiceThrows_ReturnsErrorView()
        {
            // Arrange
            var testUrl = "https://example.com/error.mp3";
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl))
                .ThrowsAsync(new Exception("Processing failed"));

            // Act
            var result = await _controller.ProcessAndSummarize(testUrl);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
            var model = viewResult.Model.Should().BeOfType<ErrorViewModel>().Subject;
            model.Message.Should().Contain("error occurred");
        }
        #endregion
    }
}
