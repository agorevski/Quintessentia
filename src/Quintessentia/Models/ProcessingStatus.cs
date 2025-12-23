using System.Text.Json.Serialization;

namespace Quintessentia.Models
{
    /// <summary>
    /// Represents the current status of audio processing.
    /// </summary>
    public class ProcessingStatus
    {
        private ProcessingStage _stage = ProcessingStage.Downloading;

        /// <summary>
        /// Gets or sets the current processing stage.
        /// </summary>
        [JsonIgnore]
        public ProcessingStage StageEnum
        {
            get => _stage;
            set => _stage = value;
        }

        /// <summary>
        /// Gets or sets the stage as a string for JSON serialization (backwards compatible).
        /// </summary>
        public string Stage
        {
            get => _stage.ToStageString();
            set => _stage = ProcessingStageExtensions.ParseStage(value);
        }

        /// <summary>
        /// Gets or sets the user-friendly message describing the current stage.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the progress percentage (0-100).
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// Gets or sets whether processing is complete.
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// Gets or sets whether an error occurred.
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// Gets or sets the error message if an error occurred.
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Gets or sets the episode identifier.
        /// </summary>
        public string? EpisodeId { get; set; }

        /// <summary>
        /// Gets or sets the file path (internal use only).
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets whether the episode was retrieved from cache.
        /// </summary>
        public bool? WasCached { get; set; }

        /// <summary>
        /// Gets or sets the word count of the transcript.
        /// </summary>
        public int? TranscriptWordCount { get; set; }

        /// <summary>
        /// Gets or sets the word count of the summary.
        /// </summary>
        public int? SummaryWordCount { get; set; }

        /// <summary>
        /// Gets or sets the summary text.
        /// </summary>
        public string? SummaryText { get; set; }

        /// <summary>
        /// Gets or sets the path to the summary audio file.
        /// </summary>
        public string? SummaryAudioPath { get; set; }

        /// <summary>
        /// Gets or sets the total processing duration.
        /// </summary>
        public TimeSpan? ProcessingDuration { get; set; }

        /// <summary>
        /// Creates a new ProcessingStatus with the specified stage.
        /// </summary>
        public static ProcessingStatus Create(ProcessingStage stage, string message, int progress) =>
            new() { StageEnum = stage, Message = message, Progress = progress };

        /// <summary>
        /// Creates an error status.
        /// </summary>
        public static ProcessingStatus CreateError(string message, string? errorMessage = null) =>
            new() { StageEnum = ProcessingStage.Error, Message = message, IsError = true, ErrorMessage = errorMessage ?? message };
    }
}
