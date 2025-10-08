using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpotifySummarizer.Models
{
    public class PodcastSummary
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int EpisodeId { get; set; }

        [MaxLength(512)]
        public string? TranscriptBlobPath { get; set; }

        [MaxLength(512)]
        public string? SummaryTextBlobPath { get; set; }

        [MaxLength(512)]
        public string? SummaryAudioBlobPath { get; set; }

        public int? TranscriptWordCount { get; set; }

        public int? SummaryWordCount { get; set; }

        public DateTime ProcessedDate { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey(nameof(EpisodeId))]
        public PodcastEpisode Episode { get; set; } = null!;
    }
}
