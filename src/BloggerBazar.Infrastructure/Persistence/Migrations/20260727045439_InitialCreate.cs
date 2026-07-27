using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blogger_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    telegram_user_id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Categories = table.Column<string[]>(type: "text[]", nullable: false),
                    Bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TotalFollowers = table.Column<int>(type: "integer", nullable: false),
                    AverageReach = table.Column<int>(type: "integer", nullable: true),
                    EngagementRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    StoriesPrice = table.Column<int>(type: "integer", nullable: true),
                    ReelsPrice = table.Column<int>(type: "integer", nullable: true),
                    PostPrice = table.Column<int>(type: "integer", nullable: true),
                    IntegrationPrice = table.Column<int>(type: "integer", nullable: true),
                    BarterEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsPromoted = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blogger_profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blogger_profiles_IsPromoted",
                table: "blogger_profiles",
                column: "IsPromoted");

            migrationBuilder.CreateIndex(
                name: "IX_blogger_profiles_Status_City",
                table: "blogger_profiles",
                columns: new[] { "Status", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_blogger_profiles_telegram_user_id",
                table: "blogger_profiles",
                column: "telegram_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blogger_profiles");
        }
    }
}
