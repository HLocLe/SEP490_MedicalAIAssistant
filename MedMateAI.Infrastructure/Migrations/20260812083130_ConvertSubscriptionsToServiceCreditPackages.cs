using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertSubscriptionsToServiceCreditPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptionUsage_UserSubscriptionId_QuotaId_CycleStart~",
                table: "UserSubscriptionUsage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSubscriptionUsage_Cycle",
                table: "UserSubscriptionUsage");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CycleEnd",
                table: "UserSubscriptionUsage",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.UpdateData(
                table: "Quota",
                keyColumn: "QuotaId",
                keyValue: new Guid("7c57cfd1-5bb6-4d4e-8959-9e87d240d481"),
                columns: new[] { "Code", "Description", "Name", "Unit" },
                values: new object[] { "SERVICE_CREDIT", "Shared credit for eligible MediMate AI services.", "Service Credit", "credit" });

            migrationBuilder.Sql(
                """
                INSERT INTO "UserSubscriptionUsage"
                    ("UserSubscriptionUsageId", "UserSubscriptionId", "QuotaId", "LimitValue",
                     "UsedCount", "ReservedCount", "CycleStart", "CycleEnd", "Version",
                     "CreatedAt", "UpdatedAt", "IsDeleted", "DeletedAt")
                SELECT
                    md5(subscription."UserSubscriptionId"::text || ':' || mapping."QuotaId"::text)::uuid,
                    subscription."UserSubscriptionId",
                    mapping."QuotaId",
                    mapping."LimitValue",
                    0,
                    0,
                    COALESCE(subscription."StartDate", subscription."CreatedAt"),
                    subscription."EndDate",
                    0,
                    subscription."CreatedAt",
                    NULL,
                    FALSE,
                    NULL
                FROM "UserSubscription" AS subscription
                INNER JOIN "SubscriptionPlanQuota" AS mapping
                    ON mapping."PlanId" = subscription."PlanId"
                   AND mapping."QuotaId" = '7c57cfd1-5bb6-4d4e-8959-9e87d240d481'::uuid
                   AND mapping."IsDeleted" = FALSE
                WHERE subscription."IsDeleted" = FALSE
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM "UserSubscriptionUsage" AS existing
                      WHERE existing."UserSubscriptionId" = subscription."UserSubscriptionId"
                        AND existing."QuotaId" = mapping."QuotaId"
                        AND existing."IsDeleted" = FALSE
                  );
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM "UserSubscriptionUsage"
                        WHERE "IsDeleted" = FALSE
                        GROUP BY "UserSubscriptionId", "QuotaId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce one service-credit snapshot per subscription: duplicate active usage rows exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "UserSubscriptionLog"
                    ("UserSubscriptionLogId", "UserSubscriptionId", "UserSubscriptionUsageId",
                     "QuotaId", "ActionType", "Quantity", "UsedCountBefore", "UsedCountAfter",
                     "ReservedCountBefore", "ReservedCountAfter", "ReferenceType", "ReferenceId",
                     "Reason", "IdempotencyKey", "PerformedByUserId", "CreatedAt")
                SELECT
                    md5('service-credit-grant:' || payment."PaymentId"::text)::uuid,
                    payment."UserSubscriptionId",
                    usage."UserSubscriptionUsageId",
                    usage."QuotaId",
                    'Grant',
                    usage."LimitValue",
                    usage."UsedCount",
                    usage."UsedCount",
                    usage."ReservedCount",
                    usage."ReservedCount",
                    'Payment',
                    payment."PaymentId",
                    'Purchased service credits granted.',
                    'credit:grant:payment:' || replace(payment."PaymentId"::text, '-', ''),
                    payment."UserId",
                    COALESCE(payment."PaidAt", payment."UpdatedAt", payment."CreatedAt")
                FROM "Payment" AS payment
                INNER JOIN "UserSubscriptionUsage" AS usage
                    ON usage."UserSubscriptionId" = payment."UserSubscriptionId"
                   AND usage."QuotaId" = '7c57cfd1-5bb6-4d4e-8959-9e87d240d481'::uuid
                   AND usage."IsDeleted" = FALSE
                WHERE payment."IsDeleted" = FALSE
                  AND payment."Status" = 'Paid'
                  AND usage."LimitValue" > 0
                ON CONFLICT ("IdempotencyKey")
                    WHERE "IdempotencyKey" IS NOT NULL
                DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionUsage_UserSubscriptionId_QuotaId",
                table: "UserSubscriptionUsage",
                columns: new[] { "UserSubscriptionId", "QuotaId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSubscriptionUsage_Cycle",
                table: "UserSubscriptionUsage",
                sql: "\"CycleEnd\" IS NULL OR \"CycleEnd\" > \"CycleStart\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "UserSubscriptionLog" AS log
                USING "Payment" AS payment
                WHERE log."UserSubscriptionLogId" =
                      md5('service-credit-grant:' || payment."PaymentId"::text)::uuid
                  AND log."ActionType" = 'Grant'
                  AND log."ReferenceType" = 'Payment'
                  AND log."ReferenceId" = payment."PaymentId"
                  AND log."IdempotencyKey" =
                      'credit:grant:payment:' || replace(payment."PaymentId"::text, '-', '');
                """);

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptionUsage_UserSubscriptionId_QuotaId",
                table: "UserSubscriptionUsage");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSubscriptionUsage_Cycle",
                table: "UserSubscriptionUsage");

            migrationBuilder.Sql(
                """
                UPDATE "UserSubscription" AS subscription
                SET "EndDate" = subscription."StartDate"
                    + make_interval(days => GREATEST(plan."DurationInDays", 1)),
                    "UpdatedAt" = CURRENT_TIMESTAMP
                FROM "SubscriptionPlan" AS plan
                WHERE subscription."PlanId" = plan."PlanId"
                  AND subscription."Status" = 'Active'
                  AND subscription."StartDate" IS NOT NULL
                  AND subscription."EndDate" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "UserSubscriptionUsage" AS usage
                SET "CycleEnd" = COALESCE(
                    subscription."EndDate",
                    usage."CycleStart" + make_interval(days => GREATEST(plan."DurationInDays", 1)))
                FROM "UserSubscription" AS subscription
                INNER JOIN "SubscriptionPlan" AS plan
                    ON plan."PlanId" = subscription."PlanId"
                WHERE usage."UserSubscriptionId" = subscription."UserSubscriptionId"
                  AND usage."CycleEnd" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CycleEnd",
                table: "UserSubscriptionUsage",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Quota",
                keyColumn: "QuotaId",
                keyValue: new Guid("7c57cfd1-5bb6-4d4e-8959-9e87d240d481"),
                columns: new[] { "Code", "Description", "Name", "Unit" },
                values: new object[] { "RECOVERY_PLAN_REQUEST", "Quota for requesting a doctor-created recovery plan.", "Recovery Plan Request", "request" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionUsage_UserSubscriptionId_QuotaId_CycleStart~",
                table: "UserSubscriptionUsage",
                columns: new[] { "UserSubscriptionId", "QuotaId", "CycleStart", "CycleEnd" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSubscriptionUsage_Cycle",
                table: "UserSubscriptionUsage",
                sql: "\"CycleEnd\" > \"CycleStart\"");
        }
    }
}
