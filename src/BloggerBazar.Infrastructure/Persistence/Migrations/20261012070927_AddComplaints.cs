using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "complaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterTelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    ReporterRole = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByTelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_complaints_ReporterTelegramUserId_CreatedAtUtc",
                table: "complaints",
                columns: new[] { "ReporterTelegramUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_complaints_ReporterTelegramUserId_TargetType_TargetId",
                table: "complaints",
                columns: new[] { "ReporterTelegramUserId", "TargetType", "TargetId" },
                unique: true,
                filter: "\"Status\" = 'open'");

            migrationBuilder.CreateIndex(
                name: "IX_complaints_Status_CreatedAtUtc",
                table: "complaints",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "complaints");
        }
    }
}
