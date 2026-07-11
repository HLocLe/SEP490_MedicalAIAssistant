using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateConsultanSessionFieldsAndConsultantQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationSession_Doctor_DoctorId",
                table: "ConsultationSession");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationSession_MedicalFacility_FacilityId",
                table: "ConsultationSession");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationSession_SymptomAnalysisSession_SymptomAnalysisS~",
                table: "ConsultationSession");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationSession_DoctorId",
                table: "ConsultationSession");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationSession_FacilityId",
                table: "ConsultationSession");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationSession_SymptomAnalysisSessionId",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "CurrentSymptoms",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "SymptomAnalysisSessionId",
                table: "ConsultationSession");

            migrationBuilder.RenameColumn(
                name: "VisitReason",
                table: "ConsultationSession",
                newName: "UserSymptoms");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ConsultationSession",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Processing",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "ConsultationQuestion",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ConsultationQuestion",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "ConsultationQuestion");

            migrationBuilder.RenameColumn(
                name: "UserSymptoms",
                table: "ConsultationSession",
                newName: "VisitReason");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ConsultationSession",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "Processing");

            migrationBuilder.AddColumn<string>(
                name: "CurrentSymptoms",
                table: "ConsultationSession",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DoctorId",
                table: "ConsultationSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "ConsultationSession",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SymptomAnalysisSessionId",
                table: "ConsultationSession",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "ConsultationQuestion",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSession_DoctorId",
                table: "ConsultationSession",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSession_FacilityId",
                table: "ConsultationSession",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSession_SymptomAnalysisSessionId",
                table: "ConsultationSession",
                column: "SymptomAnalysisSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationSession_Doctor_DoctorId",
                table: "ConsultationSession",
                column: "DoctorId",
                principalTable: "Doctor",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationSession_MedicalFacility_FacilityId",
                table: "ConsultationSession",
                column: "FacilityId",
                principalTable: "MedicalFacility",
                principalColumn: "FacilityId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationSession_SymptomAnalysisSession_SymptomAnalysisS~",
                table: "ConsultationSession",
                column: "SymptomAnalysisSessionId",
                principalTable: "SymptomAnalysisSession",
                principalColumn: "SymptomAnalysisSessionId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
