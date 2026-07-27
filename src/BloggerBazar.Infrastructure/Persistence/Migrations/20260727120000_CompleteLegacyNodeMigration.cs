using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteLegacyNodeMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_deals_CampaignApplicationId",
                table: "deals");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignApplicationId",
                table: "deals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CollaborationRequestId",
                table: "deals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "blogger_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "blogger_profiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "blogger_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "blogger_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceFrom",
                table: "blogger_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceNote",
                table: "blogger_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceTo",
                table: "blogger_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subcategory",
                table: "blogger_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "collaboration_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BloggerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collaboration_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collaboration_requests_blogger_profiles_BloggerId",
                        column: x => x.BloggerId,
                        principalTable: "blogger_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_collaboration_requests_business_profiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "business_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Balance = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_accounts", x => x.Id);
                    table.UniqueConstraint("AK_credit_accounts_TelegramUserId", x => x.TelegramUserId);
                });

            migrationBuilder.CreateTable(
                name: "social_platforms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BloggerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Followers = table.Column<int>(type: "integer", nullable: true),
                    ScreenshotUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_social_platforms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_social_platforms_blogger_profiles_BloggerId",
                        column: x => x.BloggerId,
                        principalTable: "blogger_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_ledger_entries_credit_accounts_TelegramUserId",
                        column: x => x.TelegramUserId,
                        principalTable: "credit_accounts",
                        principalColumn: "TelegramUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deals_CampaignApplicationId",
                table: "deals",
                column: "CampaignApplicationId",
                unique: true,
                filter: "\"CampaignApplicationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_deals_CollaborationRequestId",
                table: "deals",
                column: "CollaborationRequestId",
                unique: true,
                filter: "\"CollaborationRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_collaboration_requests_BloggerId_Status",
                table: "collaboration_requests",
                columns: new[] { "BloggerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_collaboration_requests_BusinessId_Status",
                table: "collaboration_requests",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_accounts_TelegramUserId",
                table: "credit_accounts",
                column: "TelegramUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_ledger_entries_TelegramUserId",
                table: "credit_ledger_entries",
                column: "TelegramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_social_platforms_BloggerId",
                table: "social_platforms",
                column: "BloggerId");

            migrationBuilder.AddForeignKey(
                name: "FK_deals_collaboration_requests_CollaborationRequestId",
                table: "deals",
                column: "CollaborationRequestId",
                principalTable: "collaboration_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_deals_collaboration_requests_CollaborationRequestId",
                table: "deals");

            migrationBuilder.DropTable(
                name: "collaboration_requests");

            migrationBuilder.DropTable(
                name: "credit_ledger_entries");

            migrationBuilder.DropTable(
                name: "social_platforms");

            migrationBuilder.DropTable(
                name: "credit_accounts");

            migrationBuilder.DropIndex(
                name: "IX_deals_CampaignApplicationId",
                table: "deals");

            migrationBuilder.DropIndex(
                name: "IX_deals_CollaborationRequestId",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CollaborationRequestId",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "Age",
                table: "blogger_profiles");

            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "blogger_profiles");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "blogger_profiles");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "blogger_profiles");

            migrationBuilder.DropColumn(
                name: "PriceFrom",
                table: "blogger_profiles");

            migrationBuilder.DropColumn(
                name: "PriceNote",
                table: "blogger_profiles");

            migrationBuilder.DropColumn(
                name: "PriceTo",
                table: "blogger_profiles");

            migrationBuilder.DropColumn(
                name: "Subcategory",
                table: "blogger_profiles");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignApplicationId",
                table: "deals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_deals_CampaignApplicationId",
                table: "deals",
                column: "CampaignApplicationId",
                unique: true);
        }
    }
}
