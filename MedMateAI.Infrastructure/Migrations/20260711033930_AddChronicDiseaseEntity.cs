using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChronicDiseaseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChronicDiseaseNote",
                table: "PatientProfile");

            migrationBuilder.CreateTable(
                name: "PatientChronicDisease",
                columns: table => new
                {
                    PatientChronicDiseaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiseaseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ToDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientChronicDisease", x => x.PatientChronicDiseaseId);
                    table.ForeignKey(
                        name: "FK_PatientChronicDisease_PatientProfile_PatientProfileId",
                        column: x => x.PatientProfileId,
                        principalTable: "PatientProfile",
                        principalColumn: "PatientProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientChronicDisease_PatientProfileId",
                table: "PatientChronicDisease",
                column: "PatientProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientChronicDisease");

            migrationBuilder.AddColumn<string>(
                name: "ChronicDiseaseNote",
                table: "PatientProfile",
                type: "text",
                nullable: true);
        }
    }
}
