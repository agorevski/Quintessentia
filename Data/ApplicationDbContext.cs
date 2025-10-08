using Microsoft.EntityFrameworkCore;
using Quintessentia.Models;

namespace Quintessentia.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AudioEpisode> AudioEpisodes { get; set; }
        public DbSet<AudioSummary> AudioSummaries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure AudioEpisode
            modelBuilder.Entity<AudioEpisode>(entity =>
            {
                entity.ToTable("PodcastEpisodes");

                entity.HasIndex(e => e.CacheKey)
                    .IsUnique()
                    .HasDatabaseName("IX_PodcastEpisodes_CacheKey");

                entity.HasIndex(e => e.OriginalUrl)
                    .HasDatabaseName("IX_PodcastEpisodes_OriginalUrl");

                entity.HasOne(e => e.Summary)
                    .WithOne(s => s.Episode)
                    .HasForeignKey<AudioSummary>(s => s.EpisodeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure AudioSummary
            modelBuilder.Entity<AudioSummary>(entity =>
            {
                entity.ToTable("PodcastSummaries");

                entity.HasIndex(e => e.EpisodeId)
                    .IsUnique()
                    .HasDatabaseName("IX_PodcastSummaries_EpisodeId");
            });
        }
    }
}
