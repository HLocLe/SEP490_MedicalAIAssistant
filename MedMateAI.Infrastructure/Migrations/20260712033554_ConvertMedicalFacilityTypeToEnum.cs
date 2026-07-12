using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertMedicalFacilityTypeToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "MedicalFacility"
                SET "FacilityType" = 'Hospital';
            """);

            migrationBuilder.AlterColumn<string>(
                name: "FacilityType",
                table: "MedicalFacility",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Hospital",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FacilityType",
                table: "MedicalFacility",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Hospital");
        }
    }
}
