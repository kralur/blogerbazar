using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialPlatformVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerificationSource",
                table: "social_platforms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAtUtc",
                table: "social_platforms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedFollowers",
                table: "social_platforms",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_social_platforms_VerifiedAtUtc",
                table: "social_platforms",
                column: "VerifiedAtUtc",
                filter: "\"VerifiedAtUtc\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_social_platforms_VerifiedAtUtc",
                table: "social_platforms");

            migrationBuilder.DropColumn(
                name: "VerificationSource",
                table: "social_platforms");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                table: "social_platforms");

            migrationBuilder.DropColumn(
                name: "VerifiedFollowers",
                table: "social_platforms");
        }
    }
}
