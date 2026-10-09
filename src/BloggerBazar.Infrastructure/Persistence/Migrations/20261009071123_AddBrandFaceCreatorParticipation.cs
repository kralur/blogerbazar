using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandFaceCreatorParticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BrandFaceId",
                table: "reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BloggerId",
                table: "deals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BrandFaceId",
                table: "deals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BloggerId",
                table: "collaboration_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BrandFaceId",
                table: "collaboration_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BloggerId",
                table: "campaign_applications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BrandFaceId",
                table: "campaign_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_BrandFaceId",
                table: "reviews",
                column: "BrandFaceId");

            migrationBuilder.CreateIndex(
                name: "IX_deals_BrandFaceId_Status",
                table: "deals",
                columns: new[] { "BrandFaceId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_deals_single_creator",
                table: "deals",
                sql: "(\"BloggerId\" IS NULL) <> (\"BrandFaceId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_collaboration_requests_BrandFaceId_Status",
                table: "collaboration_requests",
                columns: new[] { "BrandFaceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_collaboration_requests_BusinessId_BrandFaceId",
                table: "collaboration_requests",
                columns: new[] { "BusinessId", "BrandFaceId" },
                unique: true,
                filter: "\"ExpiresAtUtc\" IS NOT NULL AND \"Status\" IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_collaboration_requests_single_creator",
                table: "collaboration_requests",
                sql: "(\"BloggerId\" IS NULL) <> (\"BrandFaceId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_applications_BrandFaceId_Status",
                table: "campaign_applications",
                columns: new[] { "BrandFaceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_applications_CampaignId_BrandFaceId",
                table: "campaign_applications",
                columns: new[] { "CampaignId", "BrandFaceId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_campaign_applications_single_creator",
                table: "campaign_applications",
                sql: "(\"BloggerId\" IS NULL) <> (\"BrandFaceId\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_campaign_applications_brand_face_profiles_BrandFaceId",
                table: "campaign_applications",
                column: "BrandFaceId",
                principalTable: "brand_face_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_collaboration_requests_brand_face_profiles_BrandFaceId",
                table: "collaboration_requests",
                column: "BrandFaceId",
                principalTable: "brand_face_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_deals_brand_face_profiles_BrandFaceId",
                table: "deals",
                column: "BrandFaceId",
                principalTable: "brand_face_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reviews_brand_face_profiles_BrandFaceId",
                table: "reviews",
                column: "BrandFaceId",
                principalTable: "brand_face_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_campaign_applications_brand_face_profiles_BrandFaceId",
                table: "campaign_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_collaboration_requests_brand_face_profiles_BrandFaceId",
                table: "collaboration_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_deals_brand_face_profiles_BrandFaceId",
                table: "deals");

            migrationBuilder.DropForeignKey(
                name: "FK_reviews_brand_face_profiles_BrandFaceId",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "IX_reviews_BrandFaceId",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "IX_deals_BrandFaceId_Status",
                table: "deals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_deals_single_creator",
                table: "deals");

            migrationBuilder.DropIndex(
                name: "IX_collaboration_requests_BrandFaceId_Status",
                table: "collaboration_requests");

            migrationBuilder.DropIndex(
                name: "IX_collaboration_requests_BusinessId_BrandFaceId",
                table: "collaboration_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_collaboration_requests_single_creator",
                table: "collaboration_requests");

            migrationBuilder.DropIndex(
                name: "IX_campaign_applications_BrandFaceId_Status",
                table: "campaign_applications");

            migrationBuilder.DropIndex(
                name: "IX_campaign_applications_CampaignId_BrandFaceId",
                table: "campaign_applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_campaign_applications_single_creator",
                table: "campaign_applications");

            migrationBuilder.DropColumn(
                name: "BrandFaceId",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "BrandFaceId",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "BrandFaceId",
                table: "collaboration_requests");

            migrationBuilder.DropColumn(
                name: "BrandFaceId",
                table: "campaign_applications");

            migrationBuilder.AlterColumn<Guid>(
                name: "BloggerId",
                table: "deals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BloggerId",
                table: "collaboration_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BloggerId",
                table: "campaign_applications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
