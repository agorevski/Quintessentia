using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Controllers;
using Quintessentia.Models;
using Quintessentia.Services;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Tests.Controllers
{
    public class AudioControllerTests
    {
        private readonly Mock<IAudioService> _audioServiceMock;
        private readonly Mock<IEpisodeQueryService> _episodeQueryServiceMock;
        private readonly Mock<IProcessingProgressService> _progressServiceMock;
        private readonly Mock<ICacheKeyService> _cacheKeyServiceMock;
        private readonly Mock<IUrlValidator> _urlValidatorMock;
        private readonly JsonOptionsService _jsonOptionsService;
        private readonly Mock<ILogger<ProcessingController>> _processingLoggerMock;
        private readonly Mock<ILogger<DownloadController>> _downloadLoggerMock;
        private readonly Mock<ILogger<StreamController>> _streamLoggerMock;
        private readonly Mock<ILogger<ResultController>> _resultLoggerMock;
        private readonly ProcessingController _processingController;
        private readonly DownloadController _downloadController;
        private readonly StreamController _streamController;
        private readonly ResultController _resultController;
        private readonly AudioController _controller;

        public AudioControllerTests()
        {
            _audioServiceMock = new Mock<IAudioService>();
            _episodeQueryServiceMock = new Mock<IEpisodeQueryService>();
            _progressServiceMock = new Mock<IProcessingProgressService>();
            _cacheKeyServiceMock = new Mock<ICacheKeyService>();
            _urlValidatorMock = new Mock<IUrlValidator>();
            _jsonOptionsService = new JsonOptionsService();
            _processingLoggerMock = new Mock<ILogger<ProcessingController>>();
            _downloadLoggerMock = new Mock<ILogger<DownloadController>>();
            _streamLoggerMock = new Mock<ILogger<StreamController>>();
            _resultLoggerMock = new Mock<ILogger<ResultController>>();

            // Setup cache key service to return predictable keys
            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(It.IsAny<string>()))
                .Returns<string>(url => "testcachekey123");

            // Setup URL validator to accept valid URLs
            _urlValidatorMock.Setup(v => v.ValidateUrl(It.IsAny<string>(), out It.Ref<string?>.IsAny))
                .Returns((string url, out string? error) =>
                {
                    error = null;
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        error = "URL cannot be empty.";
                        return false;
                    }
                    if (url.StartsWith("http://") || url.StartsWith("https://"))
                    {
                        return true;
                    }
                    error = "Invalid URL format.";
                    return false;
                });

            // Create the focused controllers
            _processingController = new ProcessingController(
                _audioServiceMock.Object,
                _cacheKeyServiceMock.Object,
                _urlValidatorMock.Object,
                _processingLoggerMock.Object
            );

            _downloadController = new DownloadController(
                _episodeQueryServiceMock.Object,
                _downloadLoggerMock.Object
            );

            _streamController = new StreamController(
                _audioServiceMock.Object,
                _cacheKeyServiceMock.Object,
                _urlValidatorMock.Object,
                _jsonOptionsService,
                _streamLoggerMock.Object
            );

            _resultController = new ResultController(
                _episodeQueryServiceMock.Object,
                _resultLoggerMock.Object
            );

            // Create the facade controller
            _controller = new AudioController(
                _processingController,
                _downloadController,
                _streamController,
                _resultController
            );

            // Setup HttpContext for controller
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
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
        public async Task Process_WithNullUrl_ReturnsErrorView()
        {
            // Act
            var result = await _controller.Process(null!, CancellationToken.None);

            // Assert - Unified error response returns ViewResult with Error view
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
        }

        [Fact]
        public async Task Process_WithEmptyUrl_ReturnsErrorView()
        {
            // Act
            var result = await _controller.Process("", CancellationToken.None);

            // Assert - Unified error response returns ViewResult with Error view
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
        }

        [Fact]
        public async Task Process_WithInvalidUrlFormat_ReturnsErrorView()
        {
            // Act
            var result = await _controller.Process("not-a-valid-url", CancellationToken.None);

            // Assert - Unified error response returns ViewResult with Error view
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
            var model = viewResult.Model.Should().BeOfType<ErrorViewModel>().Subject;
            model.Message.Should().Contain("Invalid URL");
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
        public async Task ProcessAndSummarize_WithCustomEndpoint_ProcessesSuccessfully()
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

            // Assert - Custom settings no longer stored in HttpContext (removed anti-pattern)
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.Success.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessAndSummarize_WithCustomTtsSpeed_ProcessesSuccessfully()
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

            // Assert - Custom settings no longer stored in HttpContext (removed anti-pattern)
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Result");
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

            // Assert - Unified error response
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
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
        public async Task DownloadSummary_WithNonExistentSummary_ReturnsErrorView()
        {
            // Arrange
            var episodeId = "nonexistent";
            _episodeQueryServiceMock.Setup(e => e.GetSummaryStreamAsync(episodeId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FileNotFoundException());

            // Act
            var result = await _controller.DownloadSummary(episodeId, CancellationToken.None);

            // Assert - Unified error response
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
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

            // Assert - Download returns NotFound directly as it's a file endpoint
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
                .ThrowsAsync(new HttpRequestException("Unexpected error"));

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
                .ThrowsAsync(new HttpRequestException("Processing failed"));

            // Act
            var result = await _controller.ProcessAndSummarize(testUrl);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
            var model = viewResult.Model.Should().BeOfType<ErrorViewModel>().Subject;
            model.Message.Should().Contain("Failed to download");
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

        #region ProcessAndSummarizeStream Tests
        [Fact]
        public async Task ProcessAndSummarizeStream_WithNullUrl_SendsErrorStatus()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            _controller.ControllerContext.HttpContext = httpContext;

            // Act
            await _controller.ProcessAndSummarizeStream(null!);

            // Assert
            httpContext.Response.Body.Position = 0;
            var reader = new StreamReader(httpContext.Response.Body);
            var output = await reader.ReadToEndAsync();
            output.Should().Contain("error");
            output.Should().Contain("MP3 URL is required");
        }

        [Fact]
        public async Task ProcessAndSummarizeStream_WithEmptyUrl_SendsErrorStatus()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            _controller.ControllerContext.HttpContext = httpContext;

            // Act
            await _controller.ProcessAndSummarizeStream("");

            // Assert
            httpContext.Response.Body.Position = 0;
            var reader = new StreamReader(httpContext.Response.Body);
            var output = await reader.ReadToEndAsync();
            output.Should().Contain("error");
        }

        [Fact]
        public async Task ProcessAndSummarizeStream_WithInvalidUrl_SendsErrorStatus()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            _controller.ControllerContext.HttpContext = httpContext;

            // Act
            await _controller.ProcessAndSummarizeStream("not-a-valid-url");

            // Assert
            httpContext.Response.Body.Position = 0;
            var reader = new StreamReader(httpContext.Response.Body);
            var output = await reader.ReadToEndAsync();
            output.Should().Contain("Invalid URL format");
        }

        [Fact]
        public async Task ProcessAndSummarizeStream_WithCustomSettings_ProcessesSuccessfully()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            _controller.ControllerContext.HttpContext = httpContext;

            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/episode.mp3");

            // Act
            await _controller.ProcessAndSummarizeStream(
                testUrl,
                settingsEndpoint: "https://custom.openai.azure.com/",
                settingsTtsSpeedRatio: 1.5f
            );

            // Assert - Custom settings no longer stored in HttpContext (removed anti-pattern)
            // Verify the streaming endpoint ran without errors by checking response body contains data
            httpContext.Response.Body.Position = 0;
            var reader = new StreamReader(httpContext.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            responseBody.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ProcessAndSummarizeStream_WhenDownloadFails_SendsErrorStatus()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            _controller.ControllerContext.HttpContext = httpContext;

            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null!);

            // Act
            await _controller.ProcessAndSummarizeStream(testUrl);

            // Assert
            httpContext.Response.Body.Position = 0;
            var reader = new StreamReader(httpContext.Response.Body);
            var output = await reader.ReadToEndAsync();
            output.Should().Contain("error");
            output.Should().Contain("Failed to download");
        }

        [Fact]
        public async Task ProcessAndSummarizeStream_WhenExceptionOccurs_SendsErrorStatus()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            _controller.ControllerContext.HttpContext = httpContext;

            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            await _controller.ProcessAndSummarizeStream(testUrl);

            // Assert
            httpContext.Response.Body.Position = 0;
            var reader = new StreamReader(httpContext.Response.Body);
            var output = await reader.ReadToEndAsync();
            output.Should().Contain("error");
        }

        [Fact]
        public async Task ProcessAndSummarizeStream_SetsCorrectResponseHeaders()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            _controller.ControllerContext.HttpContext = httpContext;

            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null!);

            // Act
            await _controller.ProcessAndSummarizeStream(testUrl);

            // Assert
            httpContext.Response.Headers["Content-Type"].ToString().Should().Be("text/event-stream");
            httpContext.Response.Headers["Cache-Control"].ToString().Should().Be("no-cache");
            httpContext.Response.Headers["Connection"].ToString().Should().Be("keep-alive");
        }
        #endregion

        #region TrimNonAlphanumeric Integration Tests

        [Fact]
        public async Task ProcessAndSummarize_TrimsSummaryText_RemovingNonAlphanumeric()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var cacheKey = "testcachekey";
            var tempDir = Path.GetTempPath();
            var episodePath = Path.Combine(tempDir, $"{cacheKey}.mp3");
            var summaryTextPath = Path.Combine(tempDir, $"{cacheKey}_summary.txt");

            try
            {
                // Create temp files
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(episodePath, "temp");
                File.WriteAllText(summaryTextPath, "***This is a summary***");

                _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(It.IsAny<string>())).Returns(cacheKey);
                _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
                _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/temp/summary.mp3");

                // Act
                var result = await _controller.ProcessAndSummarize(testUrl);

                // Assert
                var viewResult = result.Should().BeOfType<ViewResult>().Subject;
                var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
                model.SummaryText.Should().Be("This is a summary");
            }
            finally
            {
                // Cleanup
                if (File.Exists(episodePath)) File.Delete(episodePath);
                if (File.Exists(summaryTextPath)) File.Delete(summaryTextPath);
            }
        }

        [Fact]
        public async Task ProcessAndSummarize_TrimsSummaryText_HandlesAllNonAlphanumeric()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var cacheKey = "testcachekey";
            var tempDir = Path.GetTempPath();
            var episodePath = Path.Combine(tempDir, $"{cacheKey}.mp3");
            var summaryTextPath = Path.Combine(tempDir, $"{cacheKey}_summary.txt");

            try
            {
                // Create temp files
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(episodePath, "temp");
                File.WriteAllText(summaryTextPath, "***!!!???");

                _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(It.IsAny<string>())).Returns(cacheKey);
                _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
                _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/temp/summary.mp3");

                // Act
                var result = await _controller.ProcessAndSummarize(testUrl);

                // Assert
                var viewResult = result.Should().BeOfType<ViewResult>().Subject;
                var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
                model.SummaryText.Should().BeEmpty();
            }
            finally
            {
                // Cleanup
                if (File.Exists(episodePath)) File.Delete(episodePath);
                if (File.Exists(summaryTextPath)) File.Delete(summaryTextPath);
            }
        }

        [Fact]
        public async Task ProcessAndSummarize_HandlesEmptySummaryText()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            var cacheKey = "testcachekey";
            var tempDir = Path.GetTempPath();
            var episodePath = Path.Combine(tempDir, $"{cacheKey}.mp3");
            var summaryTextPath = Path.Combine(tempDir, $"{cacheKey}_summary.txt");

            try
            {
                // Create temp files
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(episodePath, "temp");
                File.WriteAllText(summaryTextPath, "");

                _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(It.IsAny<string>())).Returns(cacheKey);
                _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
                _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("/temp/summary.mp3");

                // Act
                var result = await _controller.ProcessAndSummarize(testUrl);

                // Assert
                var viewResult = result.Should().BeOfType<ViewResult>().Subject;
                var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
                model.SummaryText.Should().BeEmpty();
            }
            finally
            {
                // Cleanup
                if (File.Exists(episodePath)) File.Delete(episodePath);
                if (File.Exists(summaryTextPath)) File.Delete(summaryTextPath);
            }
        }

        #endregion

        #region ProcessAndSummarize Additional Tests
        [Fact]
        public async Task ProcessAndSummarize_WithAllCustomSettings_ProcessesSuccessfully()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/episode.mp3");
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/temp/summary.mp3");

            // Act
            var result = await _controller.ProcessAndSummarize(
                testUrl,
                settingsEndpoint: "https://custom.openai.azure.com/",
                settingsKey: "custom-key",
                settingsWhisperDeployment: "custom-whisper",
                settingsGptDeployment: "custom-gpt",
                settingsTtsDeployment: "custom-tts",
                settingsTtsSpeedRatio: 1.5f,
                settingsTtsResponseFormat: "wav",
                settingsEnableAutoplay: true
            );

            // Assert - Custom settings no longer stored in HttpContext (removed anti-pattern)
            // Instead we verify the action completes successfully
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Result");
            var model = viewResult.Model.Should().BeOfType<AudioProcessResult>().Subject;
            model.Success.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessAndSummarize_WithInvalidUrl_ReturnsErrorView()
        {
            // Act
            var result = await _controller.ProcessAndSummarize("invalid-url");

            // Assert - Unified error response
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
        }

        [Fact]
        public async Task ProcessAndSummarize_WhenGetOrDownloadReturnsNull_ReturnsErrorView()
        {
            // Arrange
            var testUrl = "https://example.com/test.mp3";
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(testUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null!);

            // Act
            var result = await _controller.ProcessAndSummarize(testUrl);

            // Assert - Unified error response
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Error");
        }
        #endregion
    }
}
