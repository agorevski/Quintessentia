using Quintessentia.Utilities;
using Xunit;

namespace Quintessentia.Tests.Utilities
{
    public class TextHelperTests
    {
        [Fact]
        public void TrimNonAlphanumeric_WithNullInput_ReturnsNull()
        {
            // Act
            var result = TextHelper.TrimNonAlphanumeric(null!);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TrimNonAlphanumeric_WithEmptyString_ReturnsEmptyString()
        {
            // Act
            var result = TextHelper.TrimNonAlphanumeric(string.Empty);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TrimNonAlphanumeric_WithOnlyAlphanumeric_ReturnsSameString()
        {
            // Arrange
            var input = "HelloWorld123";

            // Act
            var result = TextHelper.TrimNonAlphanumeric(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void TrimNonAlphanumeric_WithLeadingSpecialChars_TrimsLeading()
        {
            // Arrange
            var input = "!!!Hello World";

            // Act
            var result = TextHelper.TrimNonAlphanumeric(input);

            // Assert
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void TrimNonAlphanumeric_WithTrailingSpecialChars_TrimsTrailing()
        {
            // Arrange
            var input = "Hello World!!!";

            // Act
            var result = TextHelper.TrimNonAlphanumeric(input);

            // Assert
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void TrimNonAlphanumeric_WithBothLeadingAndTrailing_TrimsBoth()
        {
            // Arrange
            var input = "***Hello World***";

            // Act
            var result = TextHelper.TrimNonAlphanumeric(input);

            // Assert
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void TrimNonAlphanumeric_WithQuotesAndNewlines_TrimsCorrectly()
        {
            // Arrange
            var input = "\n\r\"Hello World\"\n\r";

            // Act
            var result = TextHelper.TrimNonAlphanumeric(input);

            // Assert
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void TrimNonAlphanumeric_WithOnlySpecialChars_ReturnsEmptyString()
        {
            // Arrange
            var input = "!@#$%^&*()";

            // Act
            var result = TextHelper.TrimNonAlphanumeric(input);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TrimNonAlphanumeric_PreservesInternalSpecialChars()
        {
            // Arrange
            var input = "   Hello! How are you?   ";

            // Act
            var result = TextHelper.TrimNonAlphanumeric(input);

            // Assert
            Assert.Equal("Hello! How are you", result);
        }

        [Fact]
        public void TrimNonAlphanumeric_WithSingleAlphanumericChar_ReturnsChar()
        {
            // Arrange
            var input = "...A...";

            // Act
            var result = TextHelper.TrimNonAlphanumeric(input);

            // Assert
            Assert.Equal("A", result);
        }
    }
}
