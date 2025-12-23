using Microsoft.AspNetCore.Mvc;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Controllers
{
    /// <summary>
    /// Controller responsible for displaying processing results.
    /// </summary>
    public class ResultController : BaseController
    {
        private readonly IEpisodeQueryService _episodeQueryService;
        private readonly ILogger<ResultController> _logger;

        public ResultController(
            IEpisodeQueryService episodeQueryService,
            ILogger<ResultController> logger)
        {
            _episodeQueryService = episodeQueryService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Result(string episodeId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _episodeQueryService.GetResultAsync(episodeId, cancellationToken);
                return View(result);
            }
            catch (FileNotFoundException)
            {
                return CreateErrorResult("Episode not found.", StatusCodes.Status404NotFound);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error loading result for episode: {EpisodeId}", episodeId);
                return CreateErrorResult("An error occurred while loading the result.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation error loading result for episode: {EpisodeId}", episodeId);
                return CreateErrorResult("An error occurred while loading the result.");
            }
        }
    }
}
