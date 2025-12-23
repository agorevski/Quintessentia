using Microsoft.AspNetCore.Mvc;

namespace Quintessentia.Controllers
{
    /// <summary>
    /// Thin facade controller that delegates to focused controllers.
    /// Maintained for backwards URL compatibility with existing views and clients.
    /// Actual business logic is in ProcessingController, DownloadController, StreamController, and ResultController.
    /// </summary>
    public class AudioController : BaseController
    {
        private readonly ProcessingController _processingController;
        private readonly DownloadController _downloadController;
        private readonly StreamController _streamController;
        private readonly ResultController _resultController;

        public AudioController(
            ProcessingController processingController,
            DownloadController downloadController,
            StreamController streamController,
            ResultController resultController)
        {
            _processingController = processingController;
            _downloadController = downloadController;
            _streamController = streamController;
            _resultController = resultController;
        }

        [HttpPost]
        public Task<IActionResult> Process(string audioUrl, CancellationToken cancellationToken)
        {
            _processingController.ControllerContext = ControllerContext;
            return _processingController.Process(audioUrl, cancellationToken);
        }

        [HttpGet]
        public Task<IActionResult> Download(string episodeId, CancellationToken cancellationToken)
        {
            _downloadController.ControllerContext = ControllerContext;
            return _downloadController.Download(episodeId, cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> ProcessAndSummarize(
            string audioUrl,
            string? settingsEndpoint = null,
            string? settingsKey = null,
            string? settingsWhisperDeployment = null,
            string? settingsGptDeployment = null,
            string? settingsTtsDeployment = null,
            float? settingsTtsSpeedRatio = null,
            string? settingsTtsResponseFormat = null,
            bool? settingsEnableAutoplay = null)
        {
            _processingController.ControllerContext = ControllerContext;
            return _processingController.ProcessAndSummarize(
                audioUrl, settingsEndpoint, settingsKey, settingsWhisperDeployment,
                settingsGptDeployment, settingsTtsDeployment, settingsTtsSpeedRatio,
                settingsTtsResponseFormat, settingsEnableAutoplay);
        }

        [HttpGet]
        public Task<IActionResult> Result(string episodeId, CancellationToken cancellationToken)
        {
            _resultController.ControllerContext = ControllerContext;
            return _resultController.Result(episodeId, cancellationToken);
        }

        [HttpGet]
        public Task<IActionResult> DownloadSummary(string episodeId, CancellationToken cancellationToken)
        {
            _downloadController.ControllerContext = ControllerContext;
            return _downloadController.DownloadSummary(episodeId, cancellationToken);
        }

        [HttpGet]
        public Task ProcessAndSummarizeStream(
            string audioUrl,
            string? settingsEndpoint = null,
            string? settingsKey = null,
            string? settingsWhisperDeployment = null,
            string? settingsGptDeployment = null,
            string? settingsTtsDeployment = null,
            float? settingsTtsSpeedRatio = null,
            string? settingsTtsResponseFormat = null,
            bool? settingsEnableAutoplay = null)
        {
            _streamController.ControllerContext = ControllerContext;
            return _streamController.ProcessAndSummarizeStream(
                audioUrl, settingsEndpoint, settingsKey, settingsWhisperDeployment,
                settingsGptDeployment, settingsTtsDeployment, settingsTtsSpeedRatio,
                settingsTtsResponseFormat, settingsEnableAutoplay);
        }
    }
}
