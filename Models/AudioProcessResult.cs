namespace SpotifySummarizer.Models
{
    public class AudioProcessResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string EpisodeId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool WasCached { get; set; }
        
        // AI Processing fields
        public string? TranscriptPath { get; set; }
        public string? SummaryText { get; set; }
        public string? SummaryAudioPath { get; set; }
        public bool SummaryWasCached { get; set; }
        public TimeSpan? ProcessingDuration { get; set; }
        public int? TranscriptWordCount { get; set; }
        public int? SummaryWordCount { get; set; }
    }
}
