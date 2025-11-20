using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Models;
using Quintessentia.Services;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Tests.Services
{
    public class ProcessingProgressServiceTests
    {
        private readonly Mock<IAudioService> _audioServiceMock;
        private readonly Mock<ICacheKeyService> _cacheKeyServiceMock;
        private readonly Mock<ILogger<ProcessingProgressService>> _loggerMock;
        private readonly ProcessingProgressService _service;

        public ProcessingProgressServiceTests()
        {
            _audioServiceMock = new Mock<IAudioService>();
            _cacheKeyServiceMock = new Mock<ICacheKeyService>();
            _loggerMock = new Mock<ILogger<ProcessingProgressService>>();

            _cacheKeyServiceMock
                .Setup(c => c.GenerateFromUrl(It.IsAny<string>()))
                .Returns<string>(url => $"cache-{url.GetHashCode()}");

            _service = new ProcessingProgressService(
                _audioServiceMock.Object,
                _cacheKeyServiceMock.Object,
                _loggerMock.Object);
        }

        #region URL Validation Tests

        [Fact]
        public async Task ProcessWithProgressAsync_WithNullUrl_SendsErrorAndThrows()
        {
            // Arrange
            ProcessingStatus? errorStatus = null;
            Task OnProgress(ProcessingStatus status)
            {
                errorStatus = status;
                return Task.CompletedTask;
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ProcessWithProgressAsync(null!, null, OnProgress));

            exception.ParamName.Should().Be("audioUrl");
            errorStatus.Should().NotBeNull();
            errorStatus!.IsError.Should().BeTrue();
            errorStatus.ErrorMessage.Should().Contain("MP3 URL is required");
        }

        [Fact]
        public async Task ProcessWithProgressAsync_WithEmptyUrl_SendsErrorAndThrows()
        {
            // Arrange
            ProcessingStatus? errorStatus = null;
            Task OnProgress(ProcessingStatus status)
            {
                errorStatus = status;
                return Task.CompletedTask;
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ProcessWithProgressAsync("", null, OnProgress));

            exception.ParamName.Should().Be("audioUrl");
            errorStatus.Should().NotBeNull();
            errorStatus!.IsError.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessWithProgressAsync_WithWhitespaceUrl_SendsErrorAndThrows()
        {
            // Arrange
            ProcessingStatus? errorStatus = null;
            Task OnProgress(ProcessingStatus status)
            {
                errorStatus = status;
                return Task.CompletedTask;
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ProcessWithProgressAsync("   ", null, OnProgress));

            exception.ParamName.Should().Be("audioUrl");
            errorStatus.Should().NotBeNull();
            errorStatus!.IsError.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessWithProgressAsync_WithInvalidUrl_SendsErrorAndThrows()
        {
            // Arrange
            ProcessingStatus? errorStatus = null;
            Task OnProgress(ProcessingStatus status)
            {
                errorStatus = status;
                return Task.CompletedTask;
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ProcessWithProgressAsync("not-a-valid-url", null, OnProgress));

            exception.ParamName.Should().Be("audioUrl");
            errorStatus.Should().NotBeNull();
            errorStatus!.IsError.Should().BeTrue();
            errorStatus.ErrorMessage.Should().Contain("Invalid URL format");
        }

        [Fact]
        public async Task ProcessWithProgressAsync_WithNonHttpUrl_SendsErrorAndThrows()
        {
            // Arrange
            ProcessingStatus? errorStatus = null;
            Task OnProgress(ProcessingStatus status)
            {
                errorStatus = status;
                return Task.CompletedTask;
            }

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ProcessWithProgressAsync("ftp://example.com/audio.mp3", null, OnProgress));

            exception.ParamName.Should().Be("audioUrl");
            errorStatus.Should().NotBeNull();
            errorStatus!.IsError.Should().BeTrue();
        }

        #endregion

        #region Progress Tracking Tests

        [Fact]
        public async Task ProcessWithProgressAsync_SendsProgressUpdates_AtEachStage()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            var progressStatuses = new List<ProcessingStatus>();

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync("/path/to/episode.mp3");
            
            _audioServiceMock
                .Setup(a => a.ProcessAndSummarizeEpisodeAsync(cacheKey, It.IsAny<Action<ProcessingStatus>>(), It.IsAny<CancellationToken>()))
                .Callback<string, Action<ProcessingStatus>, CancellationToken>((id, onProgress, ct) =>
                {
                    onProgress(new ProcessingStatus { Stage = "transcribing" });
                    onProgress(new ProcessingStatus { Stage = "summarizing" });
                    onProgress(new ProcessingStatus { Stage = "generating-speech" });
                })
                .ReturnsAsync("/path/to/summary.mp3");

            Task OnProgress(ProcessingStatus status)
            {
                progressStatuses.Add(status);
                return Task.CompletedTask;
            }

            // Act
            var result = await _service.ProcessWithProgressAsync(audioUrl, null, OnProgress);

            // Assert
            progressStatuses.Should().Contain(s => s.Stage == "downloading");
            progressStatuses.Should().Contain(s => s.Stage == "downloaded");
            progressStatuses.Should().Contain(s => s.Stage == "transcribing");
            progressStatuses.Should().Contain(s => s.Stage == "summarizing");
            progressStatuses.Should().Contain(s => s.Stage == "generating-speech");
            progressStatuses.Should().Contain(s => s.Stage == "complete");
        }

        [Fact]
        public async Task ProcessWithProgressAsync_WithCachedEpisode_IndicatesCache()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            var progressStatuses = new List<ProcessingStatus>();

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync("/path/to/episode.mp3");
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(cacheKey, It.IsAny<Action<ProcessingStatus>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/path/to/summary.mp3");

            Task OnProgress(ProcessingStatus status)
            {
                progressStatuses.Add(status);
                return Task.CompletedTask;
            }

            // Act
            var result = await _service.ProcessWithProgressAsync(audioUrl, null, OnProgress);

            // Assert
            var downloadingStatus = progressStatuses.FirstOrDefault(s => s.Stage == "downloading");
            downloadingStatus.Should().NotBeNull();
            downloadingStatus!.Message.Should().Contain("cache");
            downloadingStatus.WasCached.Should().BeTrue();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task ProcessWithProgressAsync_WhenDownloadFails_SendsErrorStatus()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            ProcessingStatus? errorStatus = null;

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Download failed"));

            Task OnProgress(ProcessingStatus status)
            {
                if (status.IsError)
                    errorStatus = status;
                return Task.CompletedTask;
            }

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                _service.ProcessWithProgressAsync(audioUrl, null, OnProgress));

            errorStatus.Should().NotBeNull();
            errorStatus!.IsError.Should().BeTrue();
            errorStatus.Stage.Should().Be("error");
        }

        [Fact]
        public async Task ProcessWithProgressAsync_WhenDownloadReturnsNull_SendsErrorStatus()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            ProcessingStatus? errorStatus = null;

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync((string)null!);

            Task OnProgress(ProcessingStatus status)
            {
                if (status.IsError)
                    errorStatus = status;
                return Task.CompletedTask;
            }

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ProcessWithProgressAsync(audioUrl, null, OnProgress));

            errorStatus.Should().NotBeNull();
            errorStatus!.IsError.Should().BeTrue();
        }

        #endregion

        #region Cancellation Tests

        [Fact]
        public async Task ProcessWithProgressAsync_WhenCancelled_SendsCancellationError()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            ProcessingStatus? errorStatus = null;
            var cts = new CancellationTokenSource();

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

            Task OnProgress(ProcessingStatus status)
            {
                if (status.IsError)
                    errorStatus = status;
                return Task.CompletedTask;
            }

            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _service.ProcessWithProgressAsync(audioUrl, null, OnProgress, cts.Token));

            errorStatus.Should().NotBeNull();
            errorStatus!.ErrorMessage.Should().Contain("cancelled");
        }

        #endregion

        #region Result Assembly Tests

        [Fact]
        public async Task ProcessWithProgressAsync_ReturnsCompleteResult_WithAllDetails()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            var episodePath = "/path/to/episode.mp3";
            var summaryPath = "/path/to/summary.mp3";

            _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
            _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
            _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(cacheKey, It.IsAny<Action<ProcessingStatus>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(summaryPath);

            // Act
            var result = await _service.ProcessWithProgressAsync(audioUrl, null, _ => Task.CompletedTask);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EpisodeId.Should().Be(cacheKey);
            result.SummaryAudioPath.Should().Be(summaryPath);
            result.ProcessingDuration.Should().NotBeNull();
        }

        #endregion

        #region TrimNonAlphanumeric Integration Tests

        [Fact]
        public async Task ProcessWithProgressAsync_TrimsSummaryText_RemovingLeadingNonAlphanumeric()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            var tempDir = Path.GetTempPath();
            var episodePath = Path.Combine(tempDir, $"{cacheKey}.mp3");
            var summaryTextPath = Path.Combine(tempDir, $"{cacheKey}_summary.txt");

            try
            {
                // Create temp files with non-alphanumeric characters
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(episodePath, "temp");
                File.WriteAllText(summaryTextPath, "   ***Hello World***   ");

                _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
                _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
                _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(cacheKey, It.IsAny<Action<ProcessingStatus>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("/path/to/summary.mp3");

                // Act
                var result = await _service.ProcessWithProgressAsync(audioUrl, null, _ => Task.CompletedTask);

                // Assert
                result.SummaryText.Should().Be("Hello World");
            }
            finally
            {
                // Cleanup
                if (File.Exists(episodePath)) File.Delete(episodePath);
                if (File.Exists(summaryTextPath)) File.Delete(summaryTextPath);
            }
        }

        [Fact]
        public async Task ProcessWithProgressAsync_TrimsSummaryText_HandlesAllNonAlphanumeric()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            var tempDir = Path.GetTempPath();
            var episodePath = Path.Combine(tempDir, $"{cacheKey}.mp3");
            var summaryTextPath = Path.Combine(tempDir, $"{cacheKey}_summary.txt");

            try
            {
                // Create temp files
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(episodePath, "temp");
                File.WriteAllText(summaryTextPath, "!!!???***");

                _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
                _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
                _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(cacheKey, It.IsAny<Action<ProcessingStatus>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("/path/to/summary.mp3");

                // Act
                var result = await _service.ProcessWithProgressAsync(audioUrl, null, _ => Task.CompletedTask);

                // Assert - Should return empty string when no alphanumeric characters
                result.SummaryText.Should().BeEmpty();
            }
            finally
            {
                // Cleanup
                if (File.Exists(episodePath)) File.Delete(episodePath);
                if (File.Exists(summaryTextPath)) File.Delete(summaryTextPath);
            }
        }

        [Fact]
        public async Task ProcessWithProgressAsync_HandlesEmptySummaryText()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            var tempDir = Path.GetTempPath();
            var episodePath = Path.Combine(tempDir, $"{cacheKey}.mp3");
            var summaryTextPath = Path.Combine(tempDir, $"{cacheKey}_summary.txt");

            try
            {
                // Create temp files
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(episodePath, "temp");
                File.WriteAllText(summaryTextPath, "");

                _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
                _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
                _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(cacheKey, It.IsAny<Action<ProcessingStatus>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("/path/to/summary.mp3");

                // Act
                var result = await _service.ProcessWithProgressAsync(audioUrl, null, _ => Task.CompletedTask);

                // Assert
                result.SummaryText.Should().BeEmpty();
            }
            finally
            {
                // Cleanup
                if (File.Exists(episodePath)) File.Delete(episodePath);
                if (File.Exists(summaryTextPath)) File.Delete(summaryTextPath);
            }
        }

        [Fact]
        public async Task ProcessWithProgressAsync_CountsWordsCorrectly_InTranscriptAndSummary()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            var tempDir = Path.GetTempPath();
            var episodePath = Path.Combine(tempDir, $"{cacheKey}.mp3");
            var transcriptPath = Path.Combine(tempDir, $"{cacheKey}_transcript.txt");
            var summaryTextPath = Path.Combine(tempDir, $"{cacheKey}_summary.txt");

            try
            {
                // Create temp files
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(episodePath, "temp");
                File.WriteAllText(transcriptPath, "This is a test transcript with ten words in it.");
                File.WriteAllText(summaryTextPath, "Short summary text");

                _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
                _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
                _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(cacheKey, It.IsAny<Action<ProcessingStatus>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("/path/to/summary.mp3");

                // Act
                var result = await _service.ProcessWithProgressAsync(audioUrl, null, _ => Task.CompletedTask);

                // Assert
                result.TranscriptWordCount.Should().Be(10);
                result.SummaryWordCount.Should().Be(3);
            }
            finally
            {
                // Cleanup
                if (File.Exists(episodePath)) File.Delete(episodePath);
                if (File.Exists(transcriptPath)) File.Delete(transcriptPath);
                if (File.Exists(summaryTextPath)) File.Delete(summaryTextPath);
            }
        }

        [Fact]
        public async Task ProcessWithProgressAsync_HandlesMissingTranscriptFile()
        {
            // Arrange
            var audioUrl = "https://example.com/audio.mp3";
            var cacheKey = "test-cache-key";
            var tempDir = Path.GetTempPath();
            var episodePath = Path.Combine(tempDir, $"{cacheKey}.mp3");

            try
            {
                // Create only episode file, no transcript
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(episodePath, "temp");

                _cacheKeyServiceMock.Setup(c => c.GenerateFromUrl(audioUrl)).Returns(cacheKey);
                _audioServiceMock.Setup(a => a.IsEpisodeCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.IsSummaryCachedAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
                _audioServiceMock.Setup(a => a.GetOrDownloadEpisodeAsync(audioUrl, It.IsAny<CancellationToken>())).ReturnsAsync(episodePath);
                _audioServiceMock.Setup(a => a.ProcessAndSummarizeEpisodeAsync(cacheKey, It.IsAny<Action<ProcessingStatus>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("/path/to/summary.mp3");

                // Act
                var result = await _service.ProcessWithProgressAsync(audioUrl, null, _ => Task.CompletedTask);

                // Assert - Should handle missing files gracefully
                result.TranscriptWordCount.Should().BeNull();
                result.SummaryText.Should().BeNull();
            }
            finally
            {
                // Cleanup
                if (File.Exists(episodePath)) File.Delete(episodePath);
            }
        }

        #endregion
    }
}
