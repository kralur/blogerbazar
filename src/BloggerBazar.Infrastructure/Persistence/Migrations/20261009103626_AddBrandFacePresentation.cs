using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandFacePresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Formats",
                table: "brand_face_profiles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "PhotoUrls",
                table: "brand_face_profiles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "ShowreelUrl",
                table: "brand_face_profiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Formats",
                table: "brand_face_profiles");

            migrationBuilder.DropColumn(
                name: "PhotoUrls",
                table: "brand_face_profiles");

            migrationBuilder.DropColumn(
                name: "ShowreelUrl",
                table: "brand_face_profiles");
        }
    }
}
