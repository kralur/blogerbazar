using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BloggerBazarDbContext))]
[Migration("20260727114500_AddUniquePendingContactUnlockOrder")]
public partial class AddUniquePendingContactUnlockOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_payment_orders_pending_contact_unlock",
            table: "payment_orders",
            columns: new[] { "PayerTelegramUserId", "TargetType", "TargetId" },
            unique: true,
            filter: "\"Status\" = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_payment_orders_pending_contact_unlock",
            table: "payment_orders");
    }
}
