namespace Quintessentia.Models
{
    /// <summary>
    /// Represents the stages of audio processing pipeline.
    /// </summary>
    public enum ProcessingStage
    {
        /// <summary>Initial stage - downloading audio file.</summary>
        Downloading,

        /// <summary>Audio file downloaded or retrieved from cache.</summary>
        Downloaded,

        /// <summary>Transcribing audio to text using Whisper.</summary>
        Transcribing,

        /// <summary>Transcription complete.</summary>
        Transcribed,

        /// <summary>Summarizing transcript using GPT.</summary>
        Summarizing,

        /// <summary>Summarization complete.</summary>
        Summarized,

        /// <summary>Generating speech from summary using TTS.</summary>
        GeneratingSpeech,

        /// <summary>All processing complete.</summary>
        Complete,

        /// <summary>Error occurred during processing.</summary>
        Error
    }

    /// <summary>
    /// Extension methods for ProcessingStage enum.
    /// </summary>
    public static class ProcessingStageExtensions
    {
        /// <summary>
        /// Converts ProcessingStage enum to its string representation for JSON serialization.
        /// </summary>
        public static string ToStageString(this ProcessingStage stage)
        {
            return stage switch
            {
                ProcessingStage.Downloading => "downloading",
                ProcessingStage.Downloaded => "downloaded",
                ProcessingStage.Transcribing => "transcribing",
                ProcessingStage.Transcribed => "transcribed",
                ProcessingStage.Summarizing => "summarizing",
                ProcessingStage.Summarized => "summarized",
                ProcessingStage.GeneratingSpeech => "generating-speech",
                ProcessingStage.Complete => "complete",
                ProcessingStage.Error => "error",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parses a stage string to ProcessingStage enum.
        /// </summary>
        public static ProcessingStage ParseStage(string stage)
        {
            return stage?.ToLowerInvariant() switch
            {
                "downloading" => ProcessingStage.Downloading,
                "downloaded" => ProcessingStage.Downloaded,
                "transcribing" => ProcessingStage.Transcribing,
                "transcribed" => ProcessingStage.Transcribed,
                "summarizing" => ProcessingStage.Summarizing,
                "summarized" => ProcessingStage.Summarized,
                "generating-speech" => ProcessingStage.GeneratingSpeech,
                "complete" => ProcessingStage.Complete,
                "error" => ProcessingStage.Error,
                _ => ProcessingStage.Error
            };
        }
    }
}
