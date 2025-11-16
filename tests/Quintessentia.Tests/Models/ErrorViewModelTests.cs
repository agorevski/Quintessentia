using FluentAssertions;
using Quintessentia.Models;
using Xunit;

namespace Quintessentia.Tests.Models;

public class ErrorViewModelTests
{
    [Fact]
    public void ShowRequestId_WhenRequestIdIsNull_ReturnsFalse()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = null
        };

        // Act
        var result = model.ShowRequestId;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShowRequestId_WhenRequestIdIsEmpty_ReturnsFalse()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = string.Empty
        };

        // Act
        var result = model.ShowRequestId;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShowRequestId_WhenRequestIdHasValue_ReturnsTrue()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = "test-request-id-123"
        };

        // Act
        var result = model.ShowRequestId;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RequestId_CanBeSetAndRetrieved()
    {
        // Arrange
        var expectedRequestId = "request-12345";
        var model = new ErrorViewModel();

        // Act
        model.RequestId = expectedRequestId;
        var actualRequestId = model.RequestId;

        // Assert
        actualRequestId.Should().Be(expectedRequestId);
    }

    [Fact]
    public void Message_CanBeSetAndRetrieved()
    {
        // Arrange
        var expectedMessage = "An error occurred during processing";
        var model = new ErrorViewModel();

        // Act
        model.Message = expectedMessage;

        // Assert
        model.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void Message_CanBeNull()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            Message = null
        };

        // Assert
        model.Message.Should().BeNull();
    }

    [Fact]
    public void RequestId_CanBeNull()
    {
        // Arrange
        var model = new ErrorViewModel
        {
            RequestId = null
        };

        // Assert
        model.RequestId.Should().BeNull();
    }
}
