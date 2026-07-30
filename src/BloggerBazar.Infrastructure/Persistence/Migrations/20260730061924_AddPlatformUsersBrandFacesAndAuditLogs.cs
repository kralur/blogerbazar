using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformUsersBrandFacesAndAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorTelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "brand_face_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Age = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Languages = table.Column<string[]>(type: "text[]", nullable: false),
                    Categories = table.Column<string[]>(type: "text[]", nullable: false),
                    Experience = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Instagram = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Telegram = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CollaborationPrice = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsPromoted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_face_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByTelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ActorTelegramUserId_CreatedAtUtc",
                table: "audit_logs",
                columns: new[] { "ActorTelegramUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_TargetType_TargetId_CreatedAtUtc",
                table: "audit_logs",
                columns: new[] { "TargetType", "TargetId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_brand_face_profiles_City_IsPromoted",
                table: "brand_face_profiles",
                columns: new[] { "City", "IsPromoted" });

            migrationBuilder.CreateIndex(
                name: "IX_brand_face_profiles_TelegramUserId",
                table: "brand_face_profiles",
                column: "TelegramUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_users_Role_IsBlocked_IsDeleted",
                table: "platform_users",
                columns: new[] { "Role", "IsBlocked", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_users_TelegramUserId",
                table: "platform_users",
                column: "TelegramUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "brand_face_profiles");

            migrationBuilder.DropTable(
                name: "platform_users");
        }
    }
}
