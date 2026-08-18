using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionImageUrlToRecoveryPlanRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrescriptionImageUrl",
                table: "RecoveryPlanRequest",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrescriptionImageUrl",
                table: "RecoveryPlanRequest");
        }
    }
}
