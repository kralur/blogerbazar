using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations;

public partial class AddProfileSoftDelete : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CorrelationId",
            table: "audit_logs",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        foreach (var table in new[] { "blogger_profiles", "brand_face_profiles", "business_profiles" })
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: table,
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: table,
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        migrationBuilder.CreateIndex(name: "IX_blogger_profiles_IsDeleted", table: "blogger_profiles", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_brand_face_profiles_IsDeleted", table: "brand_face_profiles", column: "IsDeleted");
        migrationBuilder.CreateIndex(name: "IX_business_profiles_IsDeleted", table: "business_profiles", column: "IsDeleted");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_blogger_profiles_IsDeleted", table: "blogger_profiles");
        migrationBuilder.DropIndex(name: "IX_brand_face_profiles_IsDeleted", table: "brand_face_profiles");
        migrationBuilder.DropIndex(name: "IX_business_profiles_IsDeleted", table: "business_profiles");

        foreach (var table in new[] { "blogger_profiles", "brand_face_profiles", "business_profiles" })
        {
            migrationBuilder.DropColumn(name: "DeletedAtUtc", table: table);
            migrationBuilder.DropColumn(name: "IsDeleted", table: table);
        }

        migrationBuilder.DropColumn(name: "CorrelationId", table: "audit_logs");
    }
}
