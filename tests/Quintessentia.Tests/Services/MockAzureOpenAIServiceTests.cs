using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Services;
using Xunit;

namespace Quintessentia.Tests.Services
{
    public class MockAzureOpenAIServiceTests : IDisposable
    {
        private readonly Mock<ILogger<MockAzureOpenAIService>> _mockLogger;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly MockAzureOpenAIService _service;
        private readonly string _testDirectory;

        public MockAzureOpenAIServiceTests()
        {
            _mockLogger = new Mock<ILogger<MockAzureOpenAIService>>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();

            _testDirectory = Path.Combine(Path.GetTempPath(), "MockAzureOpenAIServiceTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            _mockEnvironment.Setup(e => e.WebRootPath).Returns(_testDirectory);

            _service = new MockAzureOpenAIService(_mockLogger.Object, _mockEnvironment.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task TranscribeAudioAsync_ReturnsTranscript()
        {
            // Arrange
            var audioFilePath = "test-audio.mp3";

            // Act
            var result = await _service.TranscribeAudioAsync(audioFilePath);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Tech Insights");
            result.Should().Contain("artificial intelligence");
            result.Should().Contain("machine learning");
            result.Length.Should().BeGreaterThan(1000, "transcript should be substantial");
        }

        [Fact]
        public async Task TranscribeAudioAsync_ReturnsConsistentTranscript()
        {
            // Arrange
            var audioFilePath = "test-audio.mp3";

            // Act
            var result1 = await _service.TranscribeAudioAsync(audioFilePath);
            var result2 = await _service.TranscribeAudioAsync(audioFilePath);

            // Assert
            result1.Should().Be(result2, "mock should return consistent results");
        }

        [Fact]
        public async Task TranscribeAudioAsync_CanBeCancelled()
        {
            // Arrange
            var audioFilePath = "test-audio.mp3";
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100); // Cancel after 100ms

            // Act
            var act = async () => await _service.TranscribeAudioAsync(audioFilePath, cts.Token);

            // Assert
            await act.Should().ThrowAsync<TaskCanceledException>();
        }

        [Fact]
        public async Task SummarizeTranscriptAsync_ReturnsSummary()
        {
            // Arrange
            var transcript = "Long transcript about AI and machine learning topics...";

            // Act
            var result = await _service.SummarizeTranscriptAsync(transcript);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Tech Insights");
            result.Should().Contain("artificial intelligence");
            result.Should().Contain("machine learning");
            result.Length.Should().BeGreaterThan(500, "summary should be substantial");
        }

        [Fact]
        public async Task SummarizeTranscriptAsync_ReturnsSubstantialContent()
        {
            // Arrange
            var transcript = await _service.TranscribeAudioAsync("test.mp3");

            // Act
            var summary = await _service.SummarizeTranscriptAsync(transcript);

            // Assert
            summary.Length.Should().BeGreaterThan(500, "summary should contain substantial content");
            summary.Should().NotBe(transcript, "summary should be different from transcript");
        }

        [Fact]
        public async Task SummarizeTranscriptAsync_ReturnsConsistentSummary()
        {
            // Arrange
            var transcript = "Test transcript content";

            // Act
            var result1 = await _service.SummarizeTranscriptAsync(transcript);
            var result2 = await _service.SummarizeTranscriptAsync(transcript);

            // Assert
            result1.Should().Be(result2, "mock should return consistent results");
        }

        [Fact]
        public async Task SummarizeTranscriptAsync_CanBeCancelled()
        {
            // Arrange
            var transcript = "Test transcript";
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);

            // Act
            var act = async () => await _service.SummarizeTranscriptAsync(transcript, cts.Token);

            // Assert
            await act.Should().ThrowAsync<TaskCanceledException>();
        }

        [Fact]
        public async Task GenerateSpeechAsync_CreatesOutputFile()
        {
            // Arrange
            var text = "Test text to convert to speech";
            var outputPath = Path.Combine(_testDirectory, "output.mp3");

            // Act
            var result = await _service.GenerateSpeechAsync(text, outputPath);

            // Assert
            result.Should().Be(outputPath);
            File.Exists(outputPath).Should().BeTrue();
        }

        [Fact]
        public async Task GenerateSpeechAsync_CreatesDirectoryIfNeeded()
        {
            // Arrange
            var text = "Test text";
            var subDir = Path.Combine(_testDirectory, "subdir", "nested");
            var outputPath = Path.Combine(subDir, "output.mp3");

            // Act
            await _service.GenerateSpeechAsync(text, outputPath);

            // Assert
            Directory.Exists(subDir).Should().BeTrue();
            File.Exists(outputPath).Should().BeTrue();
        }

        [Fact]
        public async Task GenerateSpeechAsync_CreatesMinimalMp3WhenSampleNotFound()
        {
            // Arrange
            var text = "Test text";
            var outputPath = Path.Combine(_testDirectory, "minimal.mp3");
            // Sample file doesn't exist, so it should create minimal MP3

            // Act
            var result = await _service.GenerateSpeechAsync(text, outputPath);

            // Assert
            result.Should().Be(outputPath);
            File.Exists(outputPath).Should().BeTrue();
            var fileInfo = new FileInfo(outputPath);
            fileInfo.Length.Should().BeGreaterThan(0, "file should have content");
        }

        [Fact]
        public async Task GenerateSpeechAsync_CopiesSampleMp3WhenAvailable()
        {
            // Arrange
            var samplePath = Path.Combine(_testDirectory, "sample-audio.mp3");
            var sampleContent = new byte[] { 0xFF, 0xFB, 0x90, 0x00, 0x01, 0x02, 0x03 };
            await File.WriteAllBytesAsync(samplePath, sampleContent);

            var text = "Test text";
            var outputPath = Path.Combine(_testDirectory, "copied.mp3");

            // Act
            await _service.GenerateSpeechAsync(text, outputPath);

            // Assert
            File.Exists(outputPath).Should().BeTrue();
            var outputContent = await File.ReadAllBytesAsync(outputPath);
            outputContent.Should().Equal(sampleContent, "should copy sample file content");
        }

        [Fact]
        public async Task GenerateSpeechAsync_OverwritesExistingFile()
        {
            // Arrange
            var text = "Test text";
            var outputPath = Path.Combine(_testDirectory, "overwrite.mp3");
            
            // Create existing file with different content
            await File.WriteAllTextAsync(outputPath, "old content");
            var oldSize = new FileInfo(outputPath).Length;

            // Act
            await _service.GenerateSpeechAsync(text, outputPath);

            // Assert
            File.Exists(outputPath).Should().BeTrue();
            var newSize = new FileInfo(outputPath).Length;
            newSize.Should().NotBe(oldSize, "file should be overwritten");
        }

        [Fact]
        public async Task GenerateSpeechAsync_CanBeCancelled()
        {
            // Arrange
            var text = "Test text";
            var outputPath = Path.Combine(_testDirectory, "cancelled.mp3");
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);

            // Act
            var act = async () => await _service.GenerateSpeechAsync(text, outputPath, cts.Token);

            // Assert
            await act.Should().ThrowAsync<TaskCanceledException>();
        }

        [Fact]
        public async Task AllMethods_SimulateProcessingDelay()
        {
            // Arrange
            var startTime = DateTime.UtcNow;

            // Act
            await _service.TranscribeAudioAsync("test.mp3");
            var transcribeTime = DateTime.UtcNow;

            await _service.SummarizeTranscriptAsync("test");
            var summarizeTime = DateTime.UtcNow;

            var outputPath = Path.Combine(_testDirectory, "speech.mp3");
            await _service.GenerateSpeechAsync("test", outputPath);
            var speechTime = DateTime.UtcNow;

            // Assert - Each operation should take approximately 2 seconds
            (transcribeTime - startTime).TotalMilliseconds.Should().BeGreaterThanOrEqualTo(1900);
            (summarizeTime - transcribeTime).TotalMilliseconds.Should().BeGreaterThanOrEqualTo(1900);
            (speechTime - summarizeTime).TotalMilliseconds.Should().BeGreaterThanOrEqualTo(1900);
        }

        [Fact]
        public async Task TranscribeAudioAsync_HandlesVariousFilePaths()
        {
            // Act & Assert - Should work with various path formats
            var result1 = await _service.TranscribeAudioAsync("simple.mp3");
            result1.Should().NotBeNullOrEmpty();

            var result2 = await _service.TranscribeAudioAsync("path/to/file.mp3");
            result2.Should().NotBeNullOrEmpty();

            var result3 = await _service.TranscribeAudioAsync(@"C:\full\path\audio.mp3");
            result3.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task SummarizeTranscriptAsync_HandlesEmptyTranscript()
        {
            // Arrange
            var emptyTranscript = "";

            // Act
            var result = await _service.SummarizeTranscriptAsync(emptyTranscript);

            // Assert
            result.Should().NotBeNullOrEmpty("mock should still return summary even for empty input");
        }

        [Fact]
        public async Task SummarizeTranscriptAsync_HandlesVeryLongTranscript()
        {
            // Arrange
            var longTranscript = string.Join(" ", Enumerable.Repeat("word", 10000));

            // Act
            var result = await _service.SummarizeTranscriptAsync(longTranscript);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Length.Should().BeLessThan(longTranscript.Length);
        }
    }
}
