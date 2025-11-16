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
        private readonly Mock<IEpisodeQueryService> _episodeQueryServiceMock;
        private readonly Mock<IProcessingProgressService> _progressServiceMock;
        private readonly Mock<ICacheKeyService> _cacheKeyServiceMock;
        private readonly Mock<ILogger<AudioController>> _loggerMock;
        private readonly AudioController _controller;

        public AudioControllerTests()
        {
            _audioServiceMock = new Mock<IAudioService>();
            _episodeQueryServiceMock = new Mock<IEpisodeQueryService>();
            _progressServiceMock = new Mock<IProcessingProgressService>();
            _cacheKeyServiceMock = new Mock<ICacheKeyService>();
            _loggerMock = new Mock<ILogger<AudioController>>();

            // Setup cache key service to return predictable keys
            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(It.IsAny<string>()))
                .Returns<string>(url => "testcachekey123");

            _controller = new AudioController(
                _audioServiceMock.Object,
                _episodeQueryServiceMock.Object,
                _progressServiceMock.Object,
                _cacheKeyServiceMock.Object,
                _loggerMock.Object
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
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/path/audio.mp3");

            // Act
            var result = await _controller.Process(testUrl, CancellationToken.None);

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
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/path/cached.mp3");

            // Act
            var result = await _controller.Process(testUrl, CancellationToken.None);

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
            var result = await _controller.Process(null!, CancellationToken.None);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Process_WithEmptyUrl_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Process("", CancellationToken.None);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Process_WithInvalidUrlFormat_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Process("not-a-valid-url", CancellationToken.None);

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
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("404 Not Found"));

            // Act
            var result = await _controller.Process(testUrl, CancellationToken.None);

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
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/episode.mp3");
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/episode.mp3");
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/episode.mp3");
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            var expectedResult = new AudioProcessResult
            {
                Success = true,
                EpisodeId = episodeId,
                FilePath = "cached",
                WasCached = true,
                SummaryWasCached = false
            };
            _episodeQueryServiceMock.Setup(e => e.GetResultAsync(episodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.Result(episodeId, CancellationToken.None);

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
            _episodeQueryServiceMock.Setup(e => e.GetResultAsync(episodeId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FileNotFoundException());

            // Act
            var result = await _controller.Result(episodeId, CancellationToken.None);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Result_WithSummary_DisplaysSummaryInfo()
        {
            // Arrange
            var episodeId = "testcachekey";
            var expectedResult = new AudioProcessResult
            {
                Success = true,
                EpisodeId = episodeId,
                FilePath = "cached",
                WasCached = true,
                SummaryWasCached = true,
                TranscriptWordCount = 1000,
                SummaryWordCount = 200,
                SummaryText = "This is the summary text",
                SummaryAudioPath = "available"
            };
            _episodeQueryServiceMock.Setup(e => e.GetResultAsync(episodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.Result(episodeId, CancellationToken.None);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.SummaryWasCached.Should().BeTrue();
            model.TranscriptWordCount.Should().Be(1000);
            model.SummaryWordCount.Should().Be(200);
            model.SummaryText.Should().Be("This is the summary text");
        }
        #endregion

        #region TC-F-033: Download summary file
        [Fact]
        public async Task DownloadSummary_WithValidEpisodeId_ReturnsFileStream()
        {
            // Arrange
            var episodeId = "testcachekey";
            var audioData = new byte[] { 1, 2, 3, 4, 5 };
            var stream = new MemoryStream(audioData);
            _episodeQueryServiceMock.Setup(e => e.GetSummaryStreamAsync(episodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stream);

            // Act
            var result = await _controller.DownloadSummary(episodeId, CancellationToken.None);

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
            _episodeQueryServiceMock.Setup(e => e.GetSummaryStreamAsync(episodeId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FileNotFoundException());

            // Act
            var result = await _controller.DownloadSummary(episodeId, CancellationToken.None);

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
            var stream = new MemoryStream(audioData);
            _episodeQueryServiceMock.Setup(e => e.GetEpisodeStreamAsync(episodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stream);

            // Act
            var result = await _controller.Download(episodeId, CancellationToken.None);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("audio/mpeg");
        }

        [Fact]
        public async Task Download_WithNonExistentEpisode_ReturnsNotFound()
        {
            // Arrange
            var episodeId = "nonexistent";
            _episodeQueryServiceMock.Setup(e => e.GetEpisodeStreamAsync(episodeId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FileNotFoundException());

            // Act
            var result = await _controller.Download(episodeId, CancellationToken.None);

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
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.Process(testUrl, CancellationToken.None);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
        }

        [Fact]
        public async Task ProcessAndSummarize_WhenServiceThrows_ReturnsErrorView()
        {
            // Arrange
            var testUrl = "https://example.com/error.mp3";
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
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

        #region TC-F-034: Cancellation Token Support
        [Fact]
        public async Task Process_PassesCancellationTokenToService()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var cts = new CancellationTokenSource();
            var tokenPassed = false;

            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((url, ct) => {
                    tokenPassed = ct == cts.Token;
                })
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/path/audio.mp3");

            // Act
            var result = await _controller.Process(testUrl, cts.Token);

            // Assert
            tokenPassed.Should().BeTrue("CancellationToken should be passed to service");
            _audioServiceMock.Verify(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), cts.Token), Times.Once);
        }

        [Fact]
        public async Task Download_PassesCancellationTokenToService()
        {
            // Arrange
            var episodeId = "testcachekey";
            var cts = new CancellationTokenSource();
            var tokenPassed = false;
            var audioData = new byte[] { 1, 2, 3, 4, 5 };
            var stream = new MemoryStream(audioData);

            _episodeQueryServiceMock.Setup(e => e.GetEpisodeStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((id, ct) => {
                    tokenPassed = ct == cts.Token;
                })
                .ReturnsAsync(stream);

            // Act
            var result = await _controller.Download(episodeId, cts.Token);

            // Assert
            tokenPassed.Should().BeTrue("CancellationToken should be passed to service");
            _episodeQueryServiceMock.Verify(e => e.GetEpisodeStreamAsync(It.IsAny<string>(), cts.Token), Times.Once);
        }
        #endregion
    }
}
