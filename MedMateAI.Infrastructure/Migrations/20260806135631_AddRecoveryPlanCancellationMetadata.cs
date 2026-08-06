using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryPlanCancellationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "RecoveryPlan",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReasonCode",
                table: "RecoveryPlan",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "RecoveryPlan",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledByUserId",
                table: "RecoveryPlan",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlan_CancelledByUserId",
                table: "RecoveryPlan",
                column: "CancelledByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecoveryPlan_AspNetUsers_CancelledByUserId",
                table: "RecoveryPlan",
                column: "CancelledByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecoveryPlan_AspNetUsers_CancelledByUserId",
                table: "RecoveryPlan");

            migrationBuilder.DropIndex(
                name: "IX_RecoveryPlan_CancelledByUserId",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "CancellationReasonCode",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "RecoveryPlan");
        }
    }
}
