namespace Quintessentia.Models
{
    public class AudioSummary
    {
        public string CacheKey { get; set; } = string.Empty;
        public string? TranscriptBlobPath { get; set; }
        public string? SummaryTextBlobPath { get; set; }
        public string? SummaryAudioBlobPath { get; set; }
        public int? TranscriptWordCount { get; set; }
        public int? SummaryWordCount { get; set; }
        public DateTime ProcessedDate { get; set; } = DateTime.UtcNow;
    }
}
