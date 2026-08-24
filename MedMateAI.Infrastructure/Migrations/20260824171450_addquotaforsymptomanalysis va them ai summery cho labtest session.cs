using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addquotaforsymptomanalysisvathemaisummerycholabtestsession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TestDate",
                table: "LabTestSession");

            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionId",
                table: "SymptomAnalysisSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionUsageId",
                table: "SymptomAnalysisSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "LabTestSession",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SymptomAnalysisSession_UserSubscriptionId",
                table: "SymptomAnalysisSession",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SymptomAnalysisSession_UserSubscriptionUsageId",
                table: "SymptomAnalysisSession",
                column: "UserSubscriptionUsageId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SymptomAnalysisSession_ServiceCreditLinkage",
                table: "SymptomAnalysisSession",
                sql: "(\"UserSubscriptionId\" IS NULL AND \"UserSubscriptionUsageId\" IS NULL) OR (\"UserSubscriptionId\" IS NOT NULL AND \"UserSubscriptionUsageId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_SymptomAnalysisSession_UserSubscriptionUsage_UserSubscripti~",
                table: "SymptomAnalysisSession",
                column: "UserSubscriptionUsageId",
                principalTable: "UserSubscriptionUsage",
                principalColumn: "UserSubscriptionUsageId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SymptomAnalysisSession_UserSubscription_UserSubscriptionId",
                table: "SymptomAnalysisSession",
                column: "UserSubscriptionId",
                principalTable: "UserSubscription",
                principalColumn: "UserSubscriptionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SymptomAnalysisSession_UserSubscriptionUsage_UserSubscripti~",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropForeignKey(
                name: "FK_SymptomAnalysisSession_UserSubscription_UserSubscriptionId",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropIndex(
                name: "IX_SymptomAnalysisSession_UserSubscriptionId",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropIndex(
                name: "IX_SymptomAnalysisSession_UserSubscriptionUsageId",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SymptomAnalysisSession_ServiceCreditLinkage",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionId",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionUsageId",
                table: "SymptomAnalysisSession");

            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "LabTestSession");

            migrationBuilder.AddColumn<DateOnly>(
                name: "TestDate",
                table: "LabTestSession",
                type: "date",
                nullable: true);
        }
    }
}
