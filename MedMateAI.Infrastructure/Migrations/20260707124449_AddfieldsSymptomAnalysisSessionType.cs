using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddfieldsSymptomAnalysisSessionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionType",
                table: "SymptomAnalysisSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SymptomAnalysisSession_UserId_SessionType",
                table: "SymptomAnalysisSession",
                columns: new[] { "UserId", "SessionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SymptomAnalysisSession_UserId_SessionType",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropColumn(
                name: "SessionType",
                table: "SymptomAnalysisSession");
        }
    }
}
