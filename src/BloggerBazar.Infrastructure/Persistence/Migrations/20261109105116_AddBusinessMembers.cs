using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    InvitedByTelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_members_business_profiles_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "business_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_members_active_owner",
                table: "business_members",
                column: "BusinessId",
                unique: true,
                filter: "\"RemovedAtUtc\" IS NULL AND \"Role\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_business_members_active_person",
                table: "business_members",
                column: "TelegramUserId",
                unique: true,
                filter: "\"RemovedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_business_members_BusinessId",
                table: "business_members",
                column: "BusinessId");

            // D64: every existing company gets its owner as its team's Owner. Exact data (the owner is the profile's
            // telegram_user_id), nothing guessed; deleted companies too, so a returning owner finds their team row.
            migrationBuilder.Sql(@"
                INSERT INTO business_members (""Id"", ""BusinessId"", ""TelegramUserId"", ""Role"", ""InvitedByTelegramUserId"", ""JoinedAtUtc"", ""RemovedAtUtc"")
                SELECT gen_random_uuid(), b.""Id"", b.telegram_user_id, 0, NULL, b.""CreatedAtUtc"", NULL
                FROM business_profiles b;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_members");
        }
    }
}
