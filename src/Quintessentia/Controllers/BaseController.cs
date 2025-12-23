using Microsoft.AspNetCore.Mvc;
using Quintessentia.Models;

namespace Quintessentia.Controllers
{
    /// <summary>
    /// Base controller providing shared functionality for all controllers.
    /// </summary>
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// Creates a consistent error result for the controller.
        /// </summary>
        protected IActionResult CreateErrorResult(string message, int statusCode = StatusCodes.Status400BadRequest)
        {
            Response.StatusCode = statusCode;
            return View("Error", new ErrorViewModel { Message = message });
        }
    }
}
