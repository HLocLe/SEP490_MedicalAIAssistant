using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterLabIndicatorUniqueIndexesByIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LabIndicatorMaster_Symbol",
                table: "LabIndicatorMaster");

            migrationBuilder.DropIndex(
                name: "IX_LabIndicatorAlias_IndicatorId_AliasText",
                table: "LabIndicatorAlias");

            migrationBuilder.DropIndex(
                name: "IX_LabIndicatorAdviceCache_IndicatorId_Status",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.DropColumn(
                name: "DoctorQuestions",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.DropColumn(
                name: "FollowUpSuggestion",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.CreateTable(
                name: "DepartmentConsultationQuestion",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentConsultationQuestion", x => x.QuestionId);
                    table.ForeignKey(
                        name: "FK_DepartmentConsultationQuestion_MedicalDepartment_Department~",
                        column: x => x.DepartmentId,
                        principalTable: "MedicalDepartment",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorMaster_Symbol",
                table: "LabIndicatorMaster",
                column: "Symbol",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorAlias_IndicatorId_AliasText",
                table: "LabIndicatorAlias",
                columns: new[] { "IndicatorId", "AliasText" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorAdviceCache_IndicatorId_Status",
                table: "LabIndicatorAdviceCache",
                columns: new[] { "IndicatorId", "Status" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentConsultationQuestion_DepartmentId_Category_SortOr~",
                table: "DepartmentConsultationQuestion",
                columns: new[] { "DepartmentId", "Category", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentConsultationQuestion_DepartmentId_QuestionText",
                table: "DepartmentConsultationQuestion",
                columns: new[] { "DepartmentId", "QuestionText" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentConsultationQuestion");

            migrationBuilder.DropIndex(
                name: "IX_LabIndicatorMaster_Symbol",
                table: "LabIndicatorMaster");

            migrationBuilder.DropIndex(
                name: "IX_LabIndicatorAlias_IndicatorId_AliasText",
                table: "LabIndicatorAlias");

            migrationBuilder.DropIndex(
                name: "IX_LabIndicatorAdviceCache_IndicatorId_Status",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.AddColumn<string>(
                name: "DoctorQuestions",
                table: "LabIndicatorAdviceCache",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpSuggestion",
                table: "LabIndicatorAdviceCache",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorMaster_Symbol",
                table: "LabIndicatorMaster",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorAlias_IndicatorId_AliasText",
                table: "LabIndicatorAlias",
                columns: new[] { "IndicatorId", "AliasText" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorAdviceCache_IndicatorId_Status",
                table: "LabIndicatorAdviceCache",
                columns: new[] { "IndicatorId", "Status" },
                unique: true);
        }
    }
}
