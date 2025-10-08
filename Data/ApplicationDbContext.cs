using Microsoft.EntityFrameworkCore;
using SpotifySummarizer.Models;

namespace SpotifySummarizer.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<PodcastEpisode> PodcastEpisodes { get; set; }
        public DbSet<PodcastSummary> PodcastSummaries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure PodcastEpisode
            modelBuilder.Entity<PodcastEpisode>(entity =>
            {
                entity.ToTable("PodcastEpisodes");

                entity.HasIndex(e => e.CacheKey)
                    .IsUnique()
                    .HasDatabaseName("IX_PodcastEpisodes_CacheKey");

                entity.HasIndex(e => e.OriginalUrl)
                    .HasDatabaseName("IX_PodcastEpisodes_OriginalUrl");

                entity.HasOne(e => e.Summary)
                    .WithOne(s => s.Episode)
                    .HasForeignKey<PodcastSummary>(s => s.EpisodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure PodcastSummary
            modelBuilder.Entity<PodcastSummary>(entity =>
            {
                entity.ToTable("PodcastSummaries");

                entity.HasIndex(e => e.EpisodeId)
                    .IsUnique()
                    .HasDatabaseName("IX_PodcastSummaries_EpisodeId");
            });
        }
    }
}
