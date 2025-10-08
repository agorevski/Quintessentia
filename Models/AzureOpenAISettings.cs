namespace Quintessentia.Models
{
    public class AzureOpenAISettings
    {
        public string? Endpoint { get; set; }
        public string? Key { get; set; }
        public string? WhisperDeployment { get; set; }
        public string? GptDeployment { get; set; }
        public string? TtsDeployment { get; set; }
        
        // TTS specific settings
        public float? TtsSpeedRatio { get; set; }
        public string? TtsResponseFormat { get; set; }

        public bool HasAnyOverride()
        {
            return !string.IsNullOrWhiteSpace(Endpoint) ||
                   !string.IsNullOrWhiteSpace(Key) ||
                   !string.IsNullOrWhiteSpace(WhisperDeployment) ||
                   !string.IsNullOrWhiteSpace(GptDeployment) ||
                   !string.IsNullOrWhiteSpace(TtsDeployment) ||
                   TtsSpeedRatio.HasValue ||
                   !string.IsNullOrWhiteSpace(TtsResponseFormat);
        }
    }
}
