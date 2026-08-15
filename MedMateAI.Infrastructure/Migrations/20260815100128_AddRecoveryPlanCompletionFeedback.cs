using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryPlanCompletionFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeedbackNote",
                table: "RecoveryPlan",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeedbackRating",
                table: "RecoveryPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackSubmittedAt",
                table: "RecoveryPlan",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecoveryPlan_FeedbackRating",
                table: "RecoveryPlan",
                sql: "\"FeedbackRating\" IS NULL OR (\"FeedbackRating\" >= 1 AND \"FeedbackRating\" <= 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RecoveryPlan_FeedbackRating",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "FeedbackNote",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "FeedbackRating",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "FeedbackSubmittedAt",
                table: "RecoveryPlan");
        }
    }
}
