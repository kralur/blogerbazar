using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformUserVerifiedPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PhoneVerifiedAtUtc",
                table: "platform_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedPhone",
                table: "platform_users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAtUtc",
                table: "platform_users");

            migrationBuilder.DropColumn(
                name: "VerifiedPhone",
                table: "platform_users");
        }
    }
}
