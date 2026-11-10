using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTeamInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reviews_DealId_ReviewerTelegramUserId",
                table: "reviews");

            migrationBuilder.AddColumn<bool>(
                name: "TeamMode",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "business_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByTelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedByTelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByTelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_invitations_business_profiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "business_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reviews_DealId_TargetType",
                table: "reviews",
                columns: new[] { "DealId", "TargetType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_invitations_BusinessId_Status",
                table: "business_invitations",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_business_invitations_CodeHash",
                table: "business_invitations",
                column: "CodeHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_invitations");

            migrationBuilder.DropIndex(
                name: "IX_reviews_DealId_TargetType",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "TeamMode",
                table: "business_profiles");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_DealId_ReviewerTelegramUserId",
                table: "reviews",
                columns: new[] { "DealId", "ReviewerTelegramUserId" },
                unique: true);
        }
    }
}
