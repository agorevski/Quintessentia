namespace Quintessentia.Constants
{
    /// <summary>
    /// Constants for audio processing configuration.
    /// </summary>
    public static class AudioProcessingConstants
    {
        /// <summary>
        /// Maximum audio file size in bytes before chunking is required.
        /// Azure OpenAI Whisper has a 25MB limit; we use 5MB for faster processing.
        /// </summary>
        public const long MaxAudioFileSizeBytes = 5 * 1024 * 1024; // 5MB

        /// <summary>
        /// Overlap in seconds between audio chunks to avoid losing words at boundaries.
        /// </summary>
        public const int ChunkOverlapSeconds = 1;

        /// <summary>
        /// Delay in milliseconds for mock service operations during development.
        /// </summary>
        public const int MockDelayMs = 2000;
    }

    /// <summary>
    /// Progress percentages for each stage of audio processing.
    /// </summary>
    public static class ProcessingProgress
    {
        /// <summary>Initial stage - starting download.</summary>
        public const int Downloading = 10;

        /// <summary>Episode downloaded or retrieved from cache.</summary>
        public const int Downloaded = 20;

        /// <summary>Transcription in progress.</summary>
        public const int Transcribing = 25;

        /// <summary>Transcription complete.</summary>
        public const int Transcribed = 40;

        /// <summary>Summarization in progress.</summary>
        public const int Summarizing = 50;

        /// <summary>Summarization complete.</summary>
        public const int Summarized = 70;

        /// <summary>Speech generation in progress.</summary>
        public const int GeneratingSpeech = 80;

        /// <summary>All processing complete.</summary>
        public const int Complete = 100;

        /// <summary>Error state.</summary>
        public const int Error = 0;
    }
}
