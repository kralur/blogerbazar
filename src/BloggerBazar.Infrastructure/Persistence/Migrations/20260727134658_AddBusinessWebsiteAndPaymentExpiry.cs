using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessWebsiteAndPaymentExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "payment_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE payment_orders SET \"ExpiresAtUtc\" = \"CreatedAtUtc\" + INTERVAL '30 minutes' WHERE \"ExpiresAtUtc\" IS NULL;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "payment_orders",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "business_profiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_Status_ExpiresAtUtc",
                table: "payment_orders",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_orders_Status_ExpiresAtUtc",
                table: "payment_orders");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "payment_orders");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "business_profiles");
        }
    }
}
