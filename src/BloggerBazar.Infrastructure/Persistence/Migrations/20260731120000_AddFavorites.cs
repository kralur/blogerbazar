using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations;

public partial class AddFavorites : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "favorites",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PlatformUserId = table.Column<Guid>(type: "uuid", nullable: false),
                BloggerId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_favorites", item => item.Id);
                table.ForeignKey(
                    name: "FK_favorites_blogger_profiles_BloggerId",
                    column: item => item.BloggerId,
                    principalTable: "blogger_profiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_favorites_platform_users_PlatformUserId",
                    column: item => item.PlatformUserId,
                    principalTable: "platform_users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_favorites_BloggerId", table: "favorites", column: "BloggerId");
        migrationBuilder.CreateIndex(name: "IX_favorites_PlatformUserId_BloggerId", table: "favorites", columns: new[] { "PlatformUserId", "BloggerId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_favorites_PlatformUserId_CreatedAtUtc", table: "favorites", columns: new[] { "PlatformUserId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "favorites");
}
