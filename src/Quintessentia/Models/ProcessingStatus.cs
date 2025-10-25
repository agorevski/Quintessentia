namespace Quintessentia.Models
{
    public class ProcessingStatus
    {
        public string Stage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Progress { get; set; } // 0-100
        public bool IsComplete { get; set; }
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Optional data to pass along
        public string? EpisodeId { get; set; }
        public string? FilePath { get; set; }
        public bool? WasCached { get; set; }
        public int? TranscriptWordCount { get; set; }
        public int? SummaryWordCount { get; set; }
        public string? SummaryText { get; set; }
        public string? SummaryAudioPath { get; set; }
        public TimeSpan? ProcessingDuration { get; set; }
    }
}
