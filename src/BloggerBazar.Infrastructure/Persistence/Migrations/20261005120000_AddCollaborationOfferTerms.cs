using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationOfferTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "collaboration_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "collaboration_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Format",
                table: "collaboration_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfferedBudget",
                table: "collaboration_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_collaboration_requests_BusinessId_BloggerId",
                table: "collaboration_requests",
                columns: new[] { "BusinessId", "BloggerId" },
                unique: true,
                filter: "\"ExpiresAtUtc\" IS NOT NULL AND \"Status\" IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_collaboration_requests_BusinessId_BloggerId",
                table: "collaboration_requests");

            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "collaboration_requests");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "collaboration_requests");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "collaboration_requests");

            migrationBuilder.DropColumn(
                name: "OfferedBudget",
                table: "collaboration_requests");
        }
    }
}
