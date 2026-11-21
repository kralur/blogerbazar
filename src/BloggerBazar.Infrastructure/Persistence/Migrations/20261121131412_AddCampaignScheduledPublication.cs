using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignScheduledPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledPublishAtUtc",
                table: "campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_ScheduledPublishAtUtc",
                table: "campaigns",
                column: "ScheduledPublishAtUtc",
                filter: "\"ScheduledPublishAtUtc\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_campaigns_ScheduledPublishAtUtc",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "ScheduledPublishAtUtc",
                table: "campaigns");
        }
    }
}
