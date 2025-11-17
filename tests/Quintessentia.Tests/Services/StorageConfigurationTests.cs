using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Quintessentia.Services;
using Xunit;

namespace Quintessentia.Tests.Services
{
    public class StorageConfigurationTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly StorageConfiguration _service;

        public StorageConfigurationTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _service = new StorageConfiguration(_mockConfiguration.Object);
        }

        [Fact]
        public void GetContainerName_WithConfiguredValue_ReturnsConfiguredName()
        {
            // Arrange
            var containerType = "Audio";
            var expectedName = "my-audio-container";
            _mockConfiguration.Setup(c => c[$"AzureStorage:Containers:{containerType}"])
                .Returns(expectedName);

            // Act
            var result = _service.GetContainerName(containerType);

            // Assert
            result.Should().Be(expectedName);
        }

        [Fact]
        public void GetContainerName_WithoutConfiguredValue_ReturnsLowercaseType()
        {
            // Arrange
            var containerType = "Audio";
            _mockConfiguration.Setup(c => c[$"AzureStorage:Containers:{containerType}"])
                .Returns((string?)null);

            // Act
            var result = _service.GetContainerName(containerType);

            // Assert
            result.Should().Be("audio", "should convert to lowercase when no config exists");
        }

        [Fact]
        public void GetContainerName_WithMixedCaseType_ReturnsLowercaseDefault()
        {
            // Arrange
            var containerType = "MyContainerType";
            _mockConfiguration.Setup(c => c[$"AzureStorage:Containers:{containerType}"])
                .Returns((string?)null);

            // Act
            var result = _service.GetContainerName(containerType);

            // Assert
            result.Should().Be("mycontainertype");
        }

        [Fact]
        public void GetContainerName_WithUpperCaseType_ReturnsLowercaseDefault()
        {
            // Arrange
            var containerType = "SUMMARIES";
            _mockConfiguration.Setup(c => c[$"AzureStorage:Containers:{containerType}"])
                .Returns((string?)null);

            // Act
            var result = _service.GetContainerName(containerType);

            // Assert
            result.Should().Be("summaries");
        }

        [Fact]
        public void GetContainerName_WithConfiguredValue_DoesNotModifyCase()
        {
            // Arrange
            var containerType = "Metadata";
            var expectedName = "MyCustomContainer-Name";
            _mockConfiguration.Setup(c => c[$"AzureStorage:Containers:{containerType}"])
                .Returns(expectedName);

            // Act
            var result = _service.GetContainerName(containerType);

            // Assert
            result.Should().Be(expectedName, "configured values should not be modified");
        }

        [Fact]
        public void GetContainerName_CalledMultipleTimes_ReturnsConsistentResults()
        {
            // Arrange
            var containerType = "Episodes";
            var configuredName = "episodes-container";
            _mockConfiguration.Setup(c => c[$"AzureStorage:Containers:{containerType}"])
                .Returns(configuredName);

            // Act
            var result1 = _service.GetContainerName(containerType);
            var result2 = _service.GetContainerName(containerType);

            // Assert
            result1.Should().Be(result2);
            result1.Should().Be(configuredName);
        }

        [Fact]
        public void GetContainerName_WithDifferentTypes_ReturnsDifferentDefaults()
        {
            // Arrange
            var type1 = "Audio";
            var type2 = "Summaries";
            _mockConfiguration.Setup(c => c[It.IsAny<string>()])
                .Returns((string?)null);

            // Act
            var result1 = _service.GetContainerName(type1);
            var result2 = _service.GetContainerName(type2);

            // Assert
            result1.Should().Be("audio");
            result2.Should().Be("summaries");
            result1.Should().NotBe(result2);
        }

        [Fact]
        public void GetContainerName_WithEmptyStringType_ReturnsEmptyString()
        {
            // Arrange
            var containerType = "";
            _mockConfiguration.Setup(c => c[$"AzureStorage:Containers:{containerType}"])
                .Returns((string?)null);

            // Act
            var result = _service.GetContainerName(containerType);

            // Assert
            result.Should().Be("");
        }

        [Fact]
        public void GetContainerName_WithSpecialCharactersInType_HandlesCorrectly()
        {
            // Arrange
            var containerType = "Audio-Files";
            _mockConfiguration.Setup(c => c[$"AzureStorage:Containers:{containerType}"])
                .Returns((string?)null);

            // Act
            var result = _service.GetContainerName(containerType);

            // Assert
            result.Should().Be("audio-files");
        }
    }
}
