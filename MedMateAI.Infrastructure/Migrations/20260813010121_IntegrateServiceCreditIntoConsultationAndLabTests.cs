using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateServiceCreditIntoConsultationAndLabTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionId",
                table: "LabTestSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionUsageId",
                table: "LabTestSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionId",
                table: "ConsultationSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionUsageId",
                table: "ConsultationSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestSession_UserSubscriptionId",
                table: "LabTestSession",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestSession_UserSubscriptionUsageId",
                table: "LabTestSession",
                column: "UserSubscriptionUsageId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LabTestSession_ServiceCreditLinkage",
                table: "LabTestSession",
                sql: "(\"UserSubscriptionId\" IS NULL AND \"UserSubscriptionUsageId\" IS NULL) OR (\"UserSubscriptionId\" IS NOT NULL AND \"UserSubscriptionUsageId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSession_UserSubscriptionId",
                table: "ConsultationSession",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationSession_UserSubscriptionUsageId",
                table: "ConsultationSession",
                column: "UserSubscriptionUsageId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConsultationSession_ServiceCreditLinkage",
                table: "ConsultationSession",
                sql: "(\"UserSubscriptionId\" IS NULL AND \"UserSubscriptionUsageId\" IS NULL) OR (\"UserSubscriptionId\" IS NOT NULL AND \"UserSubscriptionUsageId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationSession_UserSubscriptionUsage_UserSubscriptionU~",
                table: "ConsultationSession",
                column: "UserSubscriptionUsageId",
                principalTable: "UserSubscriptionUsage",
                principalColumn: "UserSubscriptionUsageId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationSession_UserSubscription_UserSubscriptionId",
                table: "ConsultationSession",
                column: "UserSubscriptionId",
                principalTable: "UserSubscription",
                principalColumn: "UserSubscriptionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabTestSession_UserSubscriptionUsage_UserSubscriptionUsageId",
                table: "LabTestSession",
                column: "UserSubscriptionUsageId",
                principalTable: "UserSubscriptionUsage",
                principalColumn: "UserSubscriptionUsageId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabTestSession_UserSubscription_UserSubscriptionId",
                table: "LabTestSession",
                column: "UserSubscriptionId",
                principalTable: "UserSubscription",
                principalColumn: "UserSubscriptionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationSession_UserSubscriptionUsage_UserSubscriptionU~",
                table: "ConsultationSession");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationSession_UserSubscription_UserSubscriptionId",
                table: "ConsultationSession");

            migrationBuilder.DropForeignKey(
                name: "FK_LabTestSession_UserSubscriptionUsage_UserSubscriptionUsageId",
                table: "LabTestSession");

            migrationBuilder.DropForeignKey(
                name: "FK_LabTestSession_UserSubscription_UserSubscriptionId",
                table: "LabTestSession");

            migrationBuilder.DropIndex(
                name: "IX_LabTestSession_UserSubscriptionId",
                table: "LabTestSession");

            migrationBuilder.DropIndex(
                name: "IX_LabTestSession_UserSubscriptionUsageId",
                table: "LabTestSession");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LabTestSession_ServiceCreditLinkage",
                table: "LabTestSession");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationSession_UserSubscriptionId",
                table: "ConsultationSession");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationSession_UserSubscriptionUsageId",
                table: "ConsultationSession");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConsultationSession_ServiceCreditLinkage",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionId",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionUsageId",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionId",
                table: "ConsultationSession");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionUsageId",
                table: "ConsultationSession");
        }
    }
}
