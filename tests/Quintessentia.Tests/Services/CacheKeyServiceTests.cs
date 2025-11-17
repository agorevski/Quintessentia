using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Services;
using Xunit;

namespace Quintessentia.Tests.Services
{
    public class CacheKeyServiceTests
    {
        private readonly Mock<ILogger<CacheKeyService>> _mockLogger;
        private readonly CacheKeyService _service;

        public CacheKeyServiceTests()
        {
            _mockLogger = new Mock<ILogger<CacheKeyService>>();
            _service = new CacheKeyService(_mockLogger.Object);
        }

        [Fact]
        public void GenerateFromUrl_WithHttpsUrl_GeneratesConsistentHash()
        {
            // Arrange
            var url = "https://example.com/test.mp3";

            // Act
            var result1 = _service.GenerateFromUrl(url);
            var result2 = _service.GenerateFromUrl(url);

            // Assert
            result1.Should().NotBeNullOrWhiteSpace();
            result1.Should().HaveLength(32);
            result1.Should().Be(result2, "same URL should generate same hash");
            result1.Should().MatchRegex("^[a-f0-9]{32}$", "should be lowercase hex");
        }

        [Fact]
        public void GenerateFromUrl_WithHttpUrl_GeneratesConsistentHash()
        {
            // Arrange
            var url = "http://example.com/test.mp3";

            // Act
            var result = _service.GenerateFromUrl(url);

            // Assert
            result.Should().NotBeNullOrWhiteSpace();
            result.Should().HaveLength(32);
            result.Should().MatchRegex("^[a-f0-9]{32}$");
        }

        [Fact]
        public void GenerateFromUrl_WithDifferentUrls_GeneratesDifferentHashes()
        {
            // Arrange
            var url1 = "https://example.com/test1.mp3";
            var url2 = "https://example.com/test2.mp3";

            // Act
            var hash1 = _service.GenerateFromUrl(url1);
            var hash2 = _service.GenerateFromUrl(url2);

            // Assert
            hash1.Should().NotBe(hash2, "different URLs should generate different hashes");
        }

        [Fact]
        public void GenerateFromUrl_WithCaseDifference_GeneratesDifferentHashes()
        {
            // Arrange
            var url1 = "https://example.com/TEST.mp3";
            var url2 = "https://example.com/test.mp3";

            // Act
            var hash1 = _service.GenerateFromUrl(url1);
            var hash2 = _service.GenerateFromUrl(url2);

            // Assert
            hash1.Should().NotBe(hash2, "URLs are case-sensitive");
        }

        [Fact]
        public void GenerateFromUrl_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var url = "https://example.com/audio%20file.mp3?key=value&other=123";

            // Act
            var result = _service.GenerateFromUrl(url);

            // Assert
            result.Should().NotBeNullOrWhiteSpace();
            result.Should().HaveLength(32);
            result.Should().MatchRegex("^[a-f0-9]{32}$");
        }

        [Fact]
        public void GenerateFromUrl_WithVeryLongUrl_GeneratesFixedLengthHash()
        {
            // Arrange
            var url = "https://example.com/very/long/path/with/many/segments/" +
                      new string('a', 500) + ".mp3?param=" + new string('b', 500);

            // Act
            var result = _service.GenerateFromUrl(url);

            // Assert
            result.Should().HaveLength(32, "hash should always be 32 characters regardless of URL length");
        }

        [Fact]
        public void GenerateFromUrl_WithNonUrlString_ReturnsAsIs()
        {
            // Arrange
            var nonUrl = "cached-file-123";

            // Act
            var result = _service.GenerateFromUrl(nonUrl);

            // Assert
            result.Should().Be(nonUrl, "non-URL strings should be returned as-is");
        }

        [Fact]
        public void GenerateFromUrl_WithFileProtocol_ReturnsAsIs()
        {
            // Arrange
            var fileUrl = "file:///C:/temp/test.mp3";

            // Act
            var result = _service.GenerateFromUrl(fileUrl);

            // Assert
            result.Should().Be(fileUrl, "file:// protocol should not be hashed");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void GenerateFromUrl_WithNullOrWhitespace_ThrowsArgumentException(string? invalidUrl)
        {
            // Act
            var act = () => _service.GenerateFromUrl(invalidUrl!);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithParameterName("url")
                .WithMessage("*cannot be null or empty*");
        }

        [Fact]
        public void GenerateFromUrl_WithHttpsUrl_LogsDebugMessage()
        {
            // Arrange
            var url = "https://example.com/test.mp3";

            // Act
            var result = _service.GenerateFromUrl(url);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated cache key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void GenerateFromUrl_WithNonUrl_LogsUsingAsIs()
        {
            // Arrange
            var nonUrl = "direct-cache-key";

            // Act
            var result = _service.GenerateFromUrl(nonUrl);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Using URL as-is")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void GenerateFromUrl_WithHttpUpperCase_GeneratesHash()
        {
            // Arrange - HTTP in uppercase
            var url = "HTTP://EXAMPLE.COM/TEST.MP3";

            // Act
            var result = _service.GenerateFromUrl(url);

            // Assert
            result.Should().NotBe(url, "should be hashed, not returned as-is");
            result.Should().HaveLength(32);
            result.Should().MatchRegex("^[a-f0-9]{32}$");
        }

        [Fact]
        public void GenerateFromUrl_WithKnownUrl_GeneratesExpectedHashPrefix()
        {
            // Arrange - Using a known URL to verify SHA256 implementation
            var url = "https://example.com/test.mp3";

            // Act
            var result = _service.GenerateFromUrl(url);

            // Assert
            result.Should().NotBeNullOrWhiteSpace();
            result.Should().HaveLength(32);
            // Verify it's consistent with SHA256 of this specific URL
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
            var expectedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant().Substring(0, 32);
            result.Should().Be(expectedHash);
        }
    }
}
