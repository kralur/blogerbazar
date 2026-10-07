using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialPlatformReachAndEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AverageReach",
                table: "social_platforms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EngagementRate",
                table: "social_platforms",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageReach",
                table: "social_platforms");

            migrationBuilder.DropColumn(
                name: "EngagementRate",
                table: "social_platforms");
        }
    }
}
