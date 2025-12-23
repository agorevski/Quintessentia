using Microsoft.AspNetCore.Mvc;
using Quintessentia.Services.Contracts;

namespace Quintessentia.Controllers
{
    /// <summary>
    /// Controller responsible for file download operations.
    /// </summary>
    public class DownloadController : BaseController
    {
        private readonly IEpisodeQueryService _episodeQueryService;
        private readonly ILogger<DownloadController> _logger;

        public DownloadController(
            IEpisodeQueryService episodeQueryService,
            ILogger<DownloadController> logger)
        {
            _episodeQueryService = episodeQueryService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Download(string episodeId, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = await _episodeQueryService.GetEpisodeStreamAsync(episodeId, cancellationToken);
                return File(stream, "audio/mpeg", $"{episodeId}.mp3");
            }
            catch (FileNotFoundException)
            {
                return NotFound("Episode not found.");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error downloading episode: {EpisodeId}", episodeId);
                return NotFound("Episode not found.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSummary(string episodeId, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = await _episodeQueryService.GetSummaryStreamAsync(episodeId, cancellationToken);
                return File(stream, "audio/mpeg", $"{episodeId}_summary.mp3");
            }
            catch (FileNotFoundException)
            {
                return CreateErrorResult("Summary not found.", StatusCodes.Status404NotFound);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error downloading summary: {EpisodeId}", episodeId);
                return CreateErrorResult("Summary not found.", StatusCodes.Status404NotFound);
            }
        }
    }
}
