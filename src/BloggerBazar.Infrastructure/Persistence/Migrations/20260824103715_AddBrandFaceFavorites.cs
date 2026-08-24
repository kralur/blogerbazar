using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandFaceFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brand_face_favorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandFaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_face_favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_brand_face_favorites_brand_face_profiles_BrandFaceId",
                        column: x => x.BrandFaceId,
                        principalTable: "brand_face_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_brand_face_favorites_platform_users_PlatformUserId",
                        column: x => x.PlatformUserId,
                        principalTable: "platform_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_brand_face_favorites_BrandFaceId",
                table: "brand_face_favorites",
                column: "BrandFaceId");

            migrationBuilder.CreateIndex(
                name: "IX_brand_face_favorites_PlatformUserId_BrandFaceId",
                table: "brand_face_favorites",
                columns: new[] { "PlatformUserId", "BrandFaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_brand_face_favorites_PlatformUserId_CreatedAtUtc_Id",
                table: "brand_face_favorites",
                columns: new[] { "PlatformUserId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brand_face_favorites");
        }
    }
}
