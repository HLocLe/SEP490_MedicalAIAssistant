using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddfieldsinDTOresponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Answer",
                table: "SessionClinicalQuestionAnswer");

            migrationBuilder.AddColumn<string>(
                name: "AnswerValues",
                table: "SessionClinicalQuestionAnswer",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerValues",
                table: "SessionClinicalQuestionAnswer");

            migrationBuilder.AddColumn<bool>(
                name: "Answer",
                table: "SessionClinicalQuestionAnswer",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
