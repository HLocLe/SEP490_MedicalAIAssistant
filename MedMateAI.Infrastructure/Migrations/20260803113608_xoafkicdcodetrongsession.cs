using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class xoafkicdcodetrongsession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SymptomAnalysisSession_IcdChapters_ChapterCode",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropIndex(
                name: "IX_SymptomAnalysisSession_ChapterCode",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropColumn(
                name: "ChapterCode",
                table: "SymptomAnalysisSession");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChapterCode",
                table: "SymptomAnalysisSession",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SymptomAnalysisSession_ChapterCode",
                table: "SymptomAnalysisSession",
                column: "ChapterCode");

            migrationBuilder.AddForeignKey(
                name: "FK_SymptomAnalysisSession_IcdChapters_ChapterCode",
                table: "SymptomAnalysisSession",
                column: "ChapterCode",
                principalTable: "IcdChapters",
                principalColumn: "ChapterCode",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
