using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloggerBazar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogTrigramSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_brand_face_profiles_categories_gin",
                table: "brand_face_profiles",
                column: "Categories")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_brand_face_profiles_city_trgm",
                table: "brand_face_profiles",
                column: "City")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_brand_face_profiles_name_trgm",
                table: "brand_face_profiles",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_blogger_profiles_categories_gin",
                table: "blogger_profiles",
                column: "Categories")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_blogger_profiles_city_trgm",
                table: "blogger_profiles",
                column: "City")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_blogger_profiles_name_trgm",
                table: "blogger_profiles",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_brand_face_profiles_categories_gin",
                table: "brand_face_profiles");

            migrationBuilder.DropIndex(
                name: "IX_brand_face_profiles_city_trgm",
                table: "brand_face_profiles");

            migrationBuilder.DropIndex(
                name: "IX_brand_face_profiles_name_trgm",
                table: "brand_face_profiles");

            migrationBuilder.DropIndex(
                name: "IX_blogger_profiles_categories_gin",
                table: "blogger_profiles");

            migrationBuilder.DropIndex(
                name: "IX_blogger_profiles_city_trgm",
                table: "blogger_profiles");

            migrationBuilder.DropIndex(
                name: "IX_blogger_profiles_name_trgm",
                table: "blogger_profiles");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
