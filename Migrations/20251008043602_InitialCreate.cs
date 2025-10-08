using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpotifySummarizer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PodcastEpisodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CacheKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OriginalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DownloadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PodcastEpisodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PodcastSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EpisodeId = table.Column<int>(type: "int", nullable: false),
                    TranscriptBlobPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SummaryTextBlobPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SummaryAudioBlobPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TranscriptWordCount = table.Column<int>(type: "int", nullable: true),
                    SummaryWordCount = table.Column<int>(type: "int", nullable: true),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PodcastSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PodcastSummaries_PodcastEpisodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "PodcastEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PodcastEpisodes_CacheKey",
                table: "PodcastEpisodes",
                column: "CacheKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PodcastEpisodes_OriginalUrl",
                table: "PodcastEpisodes",
                column: "OriginalUrl");

            migrationBuilder.CreateIndex(
                name: "IX_PodcastSummaries_EpisodeId",
                table: "PodcastSummaries",
                column: "EpisodeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PodcastSummaries");

            migrationBuilder.DropTable(
                name: "PodcastEpisodes");
        }
    }
}
