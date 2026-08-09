using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addfieldsinConsultantSessionChecklistdepartmentconsultantquestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AppointmentTime",
                table: "ConsultationSession",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "ConsultationSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReminderEnabled",
                table: "ConsultationSession",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSmsSentAt",
                table: "ConsultationSession",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChecklistItem",
                columns: table => new
                {
                    ChecklistItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItem", x => x.ChecklistItemId);
                    table.ForeignKey(
                        name: "FK_ChecklistItem_MedicalDepartment_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "MedicalDepartment",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChecklistItem_MedicalFacility_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "MedicalFacility",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSession_FacilityId",
                table: "ConsultationSession",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItem_DepartmentId",
                table: "ChecklistItem",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItem_FacilityId",
                table: "ChecklistItem",
                column: "FacilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationSession_MedicalFacility_FacilityId",
                table: "ConsultationSession",
                column: "FacilityId",
                principalTable: "MedicalFacility",
                principalColumn: "FacilityId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationSession_MedicalFacility_FacilityId",
                table: "ConsultationSession");

            migrationBuilder.DropTable(
                name: "ChecklistItem");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationSession_FacilityId",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "AppointmentTime",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "IsReminderEnabled",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "ReminderSmsSentAt",
                table: "ConsultationSession");
        }
    }
}
