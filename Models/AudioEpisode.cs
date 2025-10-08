using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpotifySummarizer.Models
{
    public class AudioEpisode
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string CacheKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(2048)]
        public string OriginalUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(512)]
        public string BlobPath { get; set; } = string.Empty;

        public DateTime DownloadDate { get; set; } = DateTime.UtcNow;

        public long FileSize { get; set; }

        // Navigation property
        public AudioSummary? Summary { get; set; }
    }
}
