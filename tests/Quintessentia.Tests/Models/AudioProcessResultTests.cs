using FluentAssertions;
using Quintessentia.Models;

namespace Quintessentia.Tests.Models
{
    public class AudioProcessResultTests
    {
        [Fact]
        public void TranscriptPath_CanBeSetAndRetrieved()
        {
            // Arrange
            var result = new AudioProcessResult();
            var expectedPath = "/path/to/transcript.txt";

            // Act
            result.TranscriptPath = expectedPath;

            // Assert
            result.TranscriptPath.Should().Be(expectedPath);
        }

        [Fact]
        public void TranscriptPath_DefaultsToNull()
        {
            // Arrange & Act
            var result = new AudioProcessResult();

            // Assert
            result.TranscriptPath.Should().BeNull();
        }

        [Fact]
        public void AudioProcessResult_AllPropertiesCanBeSet()
        {
            // Arrange
            var result = new AudioProcessResult
            {
                Success = true,
                Message = "Test message",
                EpisodeId = "test-episode-id",
                FilePath = "test-file-path",
                WasCached = true,
                TranscriptPath = "/path/to/transcript.txt",
                SummaryText = "Summary text",
                SummaryAudioPath = "/path/to/summary.mp3",
                SummaryWasCached = false,
                ProcessingDuration = TimeSpan.FromSeconds(10),
                TranscriptWordCount = 100,
                SummaryWordCount = 20
            };

            // Assert
            result.Success.Should().BeTrue();
            result.Message.Should().Be("Test message");
            result.EpisodeId.Should().Be("test-episode-id");
            result.FilePath.Should().Be("test-file-path");
            result.WasCached.Should().BeTrue();
            result.TranscriptPath.Should().Be("/path/to/transcript.txt");
            result.SummaryText.Should().Be("Summary text");
            result.SummaryAudioPath.Should().Be("/path/to/summary.mp3");
            result.SummaryWasCached.Should().BeFalse();
            result.ProcessingDuration.Should().Be(TimeSpan.FromSeconds(10));
            result.TranscriptWordCount.Should().Be(100);
            result.SummaryWordCount.Should().Be(20);
        }
    }
}
