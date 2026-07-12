using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertFeedbackReviewImageUrlToImageUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                table: "FeedbackReview",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.Sql("""
                UPDATE "FeedbackReview"
                SET "ImageUrls" = jsonb_build_object('main', "ImageUrl")
                WHERE "ImageUrl" IS NOT NULL
                  AND btrim("ImageUrl") <> '';
            """);

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "FeedbackReview");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "FeedbackReview",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "FeedbackReview"
                SET "ImageUrl" = "ImageUrls" ->> 'main'
                WHERE "ImageUrls" ? 'main';
            """);

            migrationBuilder.DropColumn(
                name: "ImageUrls",
                table: "FeedbackReview");
        }
    }
}
