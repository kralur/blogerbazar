using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDealResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgreedPrice",
                table: "deals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgreedPriceSetAtUtc",
                table: "deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "deal_publications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: true),
                    CreatedByTelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ViewsUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedByTelegramUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deal_publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_deal_publications_deals_DealId",
                        column: x => x.DealId,
                        principalTable: "deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deal_publications_DealId_Url",
                table: "deal_publications",
                columns: new[] { "DealId", "Url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deal_publications");

            migrationBuilder.DropColumn(
                name: "AgreedPrice",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "AgreedPriceSetAtUtc",
                table: "deals");
        }
    }
}
