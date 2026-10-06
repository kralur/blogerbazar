using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewPublicationAndDealReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            // Reviews written before blind reviews were already public from the moment they were created.
            migrationBuilder.Sql("UPDATE reviews SET \"PublishedAtUtc\" = \"CreatedAtUtc\";");

            migrationBuilder.CreateTable(
                name: "deal_reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RecipientRole = table.Column<int>(type: "integer", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deal_reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_deal_reminders_deals_DealId",
                        column: x => x.DealId,
                        principalTable: "deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deal_reminders_DealId_Kind_RecipientRole",
                table: "deal_reminders",
                columns: new[] { "DealId", "Kind", "RecipientRole" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deal_reminders");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                table: "reviews");
        }
    }
}
