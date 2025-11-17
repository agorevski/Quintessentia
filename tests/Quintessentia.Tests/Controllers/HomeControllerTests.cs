using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Quintessentia.Controllers;
using Quintessentia.Models;

namespace Quintessentia.Tests.Controllers
{
    public class HomeControllerTests
    {
        private readonly Mock<ILogger<HomeController>> _loggerMock;
        private readonly HomeController _controller;

        public HomeControllerTests()
        {
            _loggerMock = new Mock<ILogger<HomeController>>();
            _controller = new HomeController(_loggerMock.Object);
            
            // Setup HttpContext for the controller
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        #region Index Tests

        [Fact]
        public void Index_ReturnsViewResult()
        {
            // Act
            var result = _controller.Index();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        #endregion

        #region Privacy Tests

        [Fact]
        public void Privacy_ReturnsViewResult()
        {
            // Act
            var result = _controller.Privacy();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        #endregion

        #region Error Tests

        [Fact]
        public void Error_ReturnsViewResult_WithErrorViewModel()
        {
            // Act
            var result = _controller.Error() as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result!.Model.Should().BeOfType<ErrorViewModel>();
        }

        [Fact]
        public void Error_ErrorViewModel_HasRequestId()
        {
            // Act
            var result = _controller.Error() as ViewResult;
            var model = result?.Model as ErrorViewModel;

            // Assert
            model.Should().NotBeNull();
            model!.RequestId.Should().NotBeNullOrEmpty();
        }

        #endregion
    }
}
