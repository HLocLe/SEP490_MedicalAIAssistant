using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MergeRecoveryPlanSleepAndRestHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RecoveryPlanPhase_Rest",
                table: "RecoveryPlanPhase");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecoveryPlanPhase_Sleep",
                table: "RecoveryPlanPhase");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecoveryPlanPhase_TotalHours",
                table: "RecoveryPlanPhase");

            migrationBuilder.AddColumn<decimal>(
                name: "SleepAndRestHoursPerDay",
                table: "RecoveryPlanPhase",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "RecoveryPlanPhase"
                SET "SleepAndRestHoursPerDay" =
                    CASE
                        WHEN "SleepHoursPerDay" IS NULL
                         AND "RestHoursPerDay" IS NULL
                        THEN NULL
                        ELSE COALESCE("SleepHoursPerDay", 0)
                           + COALESCE("RestHoursPerDay", 0)
                    END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecoveryPlanPhase_SleepAndRest",
                table: "RecoveryPlanPhase",
                sql: "\"SleepAndRestHoursPerDay\" IS NULL OR (\"SleepAndRestHoursPerDay\" >= 0 AND \"SleepAndRestHoursPerDay\" <= 24)");

            migrationBuilder.DropColumn(
                name: "SleepHoursPerDay",
                table: "RecoveryPlanPhase");

            migrationBuilder.DropColumn(
                name: "RestHoursPerDay",
                table: "RecoveryPlanPhase");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RecoveryPlanPhase_SleepAndRest",
                table: "RecoveryPlanPhase");

            migrationBuilder.AddColumn<decimal>(
                name: "SleepHoursPerDay",
                table: "RecoveryPlanPhase",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RestHoursPerDay",
                table: "RecoveryPlanPhase",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "RecoveryPlanPhase"
                SET
                    "SleepHoursPerDay" =
                        CASE
                            WHEN "SleepAndRestHoursPerDay" IS NULL THEN NULL
                            ELSE "SleepAndRestHoursPerDay"
                        END,
                    "RestHoursPerDay" =
                        CASE
                            WHEN "SleepAndRestHoursPerDay" IS NULL THEN NULL
                            ELSE 0
                        END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecoveryPlanPhase_Rest",
                table: "RecoveryPlanPhase",
                sql: "\"RestHoursPerDay\" IS NULL OR (\"RestHoursPerDay\" >= 0 AND \"RestHoursPerDay\" <= 24)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecoveryPlanPhase_Sleep",
                table: "RecoveryPlanPhase",
                sql: "\"SleepHoursPerDay\" IS NULL OR (\"SleepHoursPerDay\" >= 0 AND \"SleepHoursPerDay\" <= 24)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecoveryPlanPhase_TotalHours",
                table: "RecoveryPlanPhase",
                sql: "\"SleepHoursPerDay\" IS NULL OR \"RestHoursPerDay\" IS NULL OR \"SleepHoursPerDay\" + \"RestHoursPerDay\" <= 24");

            migrationBuilder.DropColumn(
                name: "SleepAndRestHoursPerDay",
                table: "RecoveryPlanPhase");
        }
    }
}
