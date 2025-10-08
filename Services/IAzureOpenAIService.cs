namespace SpotifySummarizer.Services
{
    public interface IAzureOpenAIService
    {
        /// <summary>
        /// Transcribes audio file to text using Azure OpenAI Whisper
        /// </summary>
        /// <param name="audioFilePath">Path to the audio file</param>
        /// <returns>Transcribed text</returns>
        Task<string> TranscribeAudioAsync(string audioFilePath);

        /// <summary>
        /// Summarizes an audio transcript to approximately 5 minutes worth of content
        /// </summary>
        /// <param name="transcript">The full transcript to summarize</param>
        /// <returns>Summarized text optimized for speech (target ~750 words)</returns>
        Task<string> SummarizeTranscriptAsync(string transcript);

        /// <summary>
        /// Generates speech audio from text using Azure OpenAI TTS
        /// </summary>
        /// <param name="text">Text to convert to speech</param>
        /// <param name="outputFilePath">Path where the MP3 file should be saved</param>
        /// <returns>Path to the generated audio file</returns>
        Task<string> GenerateSpeechAsync(string text, string outputFilePath);
    }
}
