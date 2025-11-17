using FluentAssertions;
using Quintessentia.Models;

namespace Quintessentia.Tests.Models
{
    public class AzureOpenAISettingsTests
    {
        [Fact]
        public void HasAnyOverride_WithNoOverrides_ReturnsFalse()
        {
            // Arrange
            var settings = new AzureOpenAISettings();

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasAnyOverride_WithEndpointOnly_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                Endpoint = "https://custom.openai.azure.com"
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithKeyOnly_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                Key = "custom-key"
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithWhisperDeploymentOnly_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                WhisperDeployment = "custom-whisper"
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithGptDeploymentOnly_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                GptDeployment = "custom-gpt"
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithTtsDeploymentOnly_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                TtsDeployment = "custom-tts"
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithTtsSpeedRatioOnly_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                TtsSpeedRatio = 1.5f
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithTtsResponseFormatOnly_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                TtsResponseFormat = "wav"
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithEnableAutoplayOnly_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                EnableAutoplay = true
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithMultipleOverrides_ReturnsTrue()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                Endpoint = "https://custom.openai.azure.com",
                Key = "custom-key",
                TtsSpeedRatio = 1.5f
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                Endpoint = "https://test.openai.azure.com",
                Key = "test-key",
                WhisperDeployment = "whisper-1",
                GptDeployment = "gpt-4",
                TtsDeployment = "tts-1",
                TtsSpeedRatio = 1.25f,
                TtsResponseFormat = "mp3",
                EnableAutoplay = true
            };

            // Assert
            settings.Endpoint.Should().Be("https://test.openai.azure.com");
            settings.Key.Should().Be("test-key");
            settings.WhisperDeployment.Should().Be("whisper-1");
            settings.GptDeployment.Should().Be("gpt-4");
            settings.TtsDeployment.Should().Be("tts-1");
            settings.TtsSpeedRatio.Should().Be(1.25f);
            settings.TtsResponseFormat.Should().Be("mp3");
            settings.EnableAutoplay.Should().BeTrue();
        }

        [Fact]
        public void HasAnyOverride_WithEmptyStrings_ReturnsFalse()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                Endpoint = "",
                Key = "",
                WhisperDeployment = "",
                GptDeployment = "",
                TtsDeployment = "",
                TtsResponseFormat = ""
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasAnyOverride_WithWhitespaceStrings_ReturnsFalse()
        {
            // Arrange
            var settings = new AzureOpenAISettings
            {
                Endpoint = "   ",
                Key = "  ",
                TtsResponseFormat = "\t"
            };

            // Act
            var result = settings.HasAnyOverride();

            // Assert
            result.Should().BeFalse();
        }
    }
}
