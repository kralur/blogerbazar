using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDealCampaignTermsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampaignBudgetFromSnapshot",
                table: "deals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CampaignBudgetToSnapshot",
                table: "deals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "CampaignCategoriesSnapshot",
                table: "deals",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CampaignCitySnapshot",
                table: "deals",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CampaignDeadlineSnapshot",
                table: "deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CampaignDescriptionSnapshot",
                table: "deals",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "CampaignRequirementsSnapshot",
                table: "deals",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "CampaignTermsSnapshotVersion",
                table: "deals",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CampaignTitleSnapshot",
                table: "deals",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampaignBudgetFromSnapshot",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CampaignBudgetToSnapshot",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CampaignCategoriesSnapshot",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CampaignCitySnapshot",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CampaignDeadlineSnapshot",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CampaignDescriptionSnapshot",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CampaignRequirementsSnapshot",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CampaignTermsSnapshotVersion",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "CampaignTitleSnapshot",
                table: "deals");
        }
    }
}
