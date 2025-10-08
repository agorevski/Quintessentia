namespace Quintessentia.Models
{
    public class AudioEpisode
    {
        public string CacheKey { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string BlobPath { get; set; } = string.Empty;
        public DateTime DownloadDate { get; set; } = DateTime.UtcNow;
        public long FileSize { get; set; }
    }
}
