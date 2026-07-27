using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryPlanQuotaAndSchedulingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notification_FollowUpReminder_ReminderId",
                table: "Notification");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoveryPlan_TreatmentJourney_TreatmentJourneyId",
                table: "RecoveryPlan");

            migrationBuilder.AddColumn<bool>(
                name: "IsReminderEnabled",
                table: "UserMedication",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "UserMedication",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PatientReported");

            migrationBuilder.AlterColumn<Guid>(
                name: "TreatmentJourneyId",
                table: "RecoveryPlan",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "RecoveryPlan",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "RecoveryPlan",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "RecoveryPlan",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalSnapshotJson",
                table: "RecoveryPlan",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "RecoveryPlan",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DoctorId",
                table: "RecoveryPlan",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "RecoveryPlan",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecheckInstruction",
                table: "RecoveryPlan",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecoveryPlanRequestId",
                table: "RecoveryPlan",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RecoveryPlan",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "RecoveryPlan",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "RecoveryPlan",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "RecoveryPlan" AS rp
                SET "UserId" = tj."UserId"
                FROM "TreatmentJourney" AS tj
                WHERE rp."TreatmentJourneyId" = tj."TreatmentJourneyId"
                  AND rp."UserId" IS NULL;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "RecoveryPlan" WHERE "UserId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot migrate RecoveryPlan.UserId: one or more legacy recovery plans have no resolvable TreatmentJourney user.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "RecoveryPlan",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notification",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Notification",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReminderId",
                table: "Notification",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notification",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "Notification",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "Notification",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DedupeKey",
                table: "Notification",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Notification",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationType",
                table: "Notification",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "FOLLOW_UP_REMINDER");

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "Notification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "Notification",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "Notification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAcceptingRecoveryPlanRequests",
                table: "Doctor",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxConcurrentRecoveryPlanRequests",
                table: "Doctor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Asia/Ho_Chi_Minh");

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.OutboxMessageId);
                });

            migrationBuilder.CreateTable(
                name: "Quota",
                columns: table => new
                {
                    QuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quota", x => x.QuotaId);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryPlanPhase",
                columns: table => new
                {
                    RecoveryPlanPhaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecoveryPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhaseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDay = table.Column<int>(type: "integer", nullable: false),
                    EndDay = table.Column<int>(type: "integer", nullable: false),
                    SleepHoursPerDay = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    RestHoursPerDay = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Instruction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryPlanPhase", x => x.RecoveryPlanPhaseId);
                    table.CheckConstraint("CK_RecoveryPlanPhase_Days", "\"StartDay\" >= 1 AND \"EndDay\" >= \"StartDay\"");
                    table.CheckConstraint("CK_RecoveryPlanPhase_Rest", "\"RestHoursPerDay\" IS NULL OR (\"RestHoursPerDay\" >= 0 AND \"RestHoursPerDay\" <= 24)");
                    table.CheckConstraint("CK_RecoveryPlanPhase_Sleep", "\"SleepHoursPerDay\" IS NULL OR (\"SleepHoursPerDay\" >= 0 AND \"SleepHoursPerDay\" <= 24)");
                    table.CheckConstraint("CK_RecoveryPlanPhase_TotalHours", "\"SleepHoursPerDay\" IS NULL OR \"RestHoursPerDay\" IS NULL OR \"SleepHoursPerDay\" + \"RestHoursPerDay\" <= 24");
                    table.ForeignKey(
                        name: "FK_RecoveryPlanPhase_RecoveryPlan_RecoveryPlanId",
                        column: x => x.RecoveryPlanId,
                        principalTable: "RecoveryPlan",
                        principalColumn: "RecoveryPlanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMedicationReminderTime",
                columns: table => new
                {
                    UserMedicationReminderTimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserMedicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeOfDay = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMedicationReminderTime", x => x.UserMedicationReminderTimeId);
                    table.ForeignKey(
                        name: "FK_UserMedicationReminderTime_UserMedication_UserMedicationId",
                        column: x => x.UserMedicationId,
                        principalTable: "UserMedication",
                        principalColumn: "UserMedicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanQuota",
                columns: table => new
                {
                    SubscriptionPlanQuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LimitValue = table.Column<int>(type: "integer", nullable: false),
                    ResetPeriod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanQuota", x => x.SubscriptionPlanQuotaId);
                    table.CheckConstraint("CK_SubscriptionPlanQuota_LimitValue", "\"LimitValue\" >= 0");
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanQuota_Quota_QuotaId",
                        column: x => x.QuotaId,
                        principalTable: "Quota",
                        principalColumn: "QuotaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanQuota_SubscriptionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlan",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptionUsage",
                columns: table => new
                {
                    UserSubscriptionUsageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LimitValue = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    ReservedCount = table.Column<int>(type: "integer", nullable: false),
                    CycleStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CycleEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptionUsage", x => x.UserSubscriptionUsageId);
                    table.CheckConstraint("CK_UserSubscriptionUsage_Cycle", "\"CycleEnd\" > \"CycleStart\"");
                    table.CheckConstraint("CK_UserSubscriptionUsage_LimitValue", "\"LimitValue\" >= 0");
                    table.CheckConstraint("CK_UserSubscriptionUsage_ReservedCount", "\"ReservedCount\" >= 0");
                    table.CheckConstraint("CK_UserSubscriptionUsage_Total", "\"UsedCount\" + \"ReservedCount\" <= \"LimitValue\"");
                    table.CheckConstraint("CK_UserSubscriptionUsage_UsedCount", "\"UsedCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_UserSubscriptionUsage_Quota_QuotaId",
                        column: x => x.QuotaId,
                        principalTable: "Quota",
                        principalColumn: "QuotaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptionUsage_UserSubscription_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscription",
                        principalColumn: "UserSubscriptionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryPlanNutrientTarget",
                columns: table => new
                {
                    RecoveryPlanNutrientTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecoveryPlanPhaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    NutrientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AmountPerDay = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Instruction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryPlanNutrientTarget", x => x.RecoveryPlanNutrientTargetId);
                    table.CheckConstraint("CK_RecoveryPlanNutrientTarget_Amount", "\"AmountPerDay\" > 0");
                    table.ForeignKey(
                        name: "FK_RecoveryPlanNutrientTarget_RecoveryPlanPhase_RecoveryPlanPh~",
                        column: x => x.RecoveryPlanPhaseId,
                        principalTable: "RecoveryPlanPhase",
                        principalColumn: "RecoveryPlanPhaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryPlanRequest",
                columns: table => new
                {
                    RecoveryPlanRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiseaseGroup = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TreatmentJourneyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryLabTestSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSubscriptionUsageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignmentExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryPlanRequest", x => x.RecoveryPlanRequestId);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequest_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequest_Doctor_AssignedDoctorId",
                        column: x => x.AssignedDoctorId,
                        principalTable: "Doctor",
                        principalColumn: "DoctorId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequest_LabTestSession_PrimaryLabTestSessionId",
                        column: x => x.PrimaryLabTestSessionId,
                        principalTable: "LabTestSession",
                        principalColumn: "TestSessionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequest_TreatmentJourney_TreatmentJourneyId",
                        column: x => x.TreatmentJourneyId,
                        principalTable: "TreatmentJourney",
                        principalColumn: "TreatmentJourneyId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequest_UserSubscriptionUsage_UserSubscriptionU~",
                        column: x => x.UserSubscriptionUsageId,
                        principalTable: "UserSubscriptionUsage",
                        principalColumn: "UserSubscriptionUsageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequest_UserSubscription_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscription",
                        principalColumn: "UserSubscriptionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptionLog",
                columns: table => new
                {
                    UserSubscriptionLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSubscriptionUsageId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UsedCountBefore = table.Column<int>(type: "integer", nullable: false),
                    UsedCountAfter = table.Column<int>(type: "integer", nullable: false),
                    ReservedCountBefore = table.Column<int>(type: "integer", nullable: false),
                    ReservedCountAfter = table.Column<int>(type: "integer", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptionLog", x => x.UserSubscriptionLogId);
                    table.CheckConstraint("CK_UserSubscriptionLog_Counts", "\"UsedCountBefore\" >= 0 AND \"UsedCountAfter\" >= 0 AND \"ReservedCountBefore\" >= 0 AND \"ReservedCountAfter\" >= 0");
                    table.CheckConstraint("CK_UserSubscriptionLog_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_UserSubscriptionLog_AspNetUsers_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserSubscriptionLog_Quota_QuotaId",
                        column: x => x.QuotaId,
                        principalTable: "Quota",
                        principalColumn: "QuotaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptionLog_UserSubscriptionUsage_UserSubscriptionU~",
                        column: x => x.UserSubscriptionUsageId,
                        principalTable: "UserSubscriptionUsage",
                        principalColumn: "UserSubscriptionUsageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptionLog_UserSubscription_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscription",
                        principalColumn: "UserSubscriptionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryPlanFoodSource",
                columns: table => new
                {
                    RecoveryPlanFoodSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecoveryPlanNutrientTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SuggestedServing = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryPlanFoodSource", x => x.RecoveryPlanFoodSourceId);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanFoodSource_RecoveryPlanNutrientTarget_RecoveryP~",
                        column: x => x.RecoveryPlanNutrientTargetId,
                        principalTable: "RecoveryPlanNutrientTarget",
                        principalColumn: "RecoveryPlanNutrientTargetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryPlanRequestEvent",
                columns: table => new
                {
                    RecoveryPlanRequestEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecoveryPlanRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryPlanRequestEvent", x => x.RecoveryPlanRequestEventId);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequestEvent_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequestEvent_Doctor_ActorDoctorId",
                        column: x => x.ActorDoctorId,
                        principalTable: "Doctor",
                        principalColumn: "DoctorId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecoveryPlanRequestEvent_RecoveryPlanRequest_RecoveryPlanRe~",
                        column: x => x.RecoveryPlanRequestId,
                        principalTable: "RecoveryPlanRequest",
                        principalColumn: "RecoveryPlanRequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Quota",
                columns: new[] { "QuotaId", "Code", "CreatedAt", "DeletedAt", "Description", "IsActive", "IsDeleted", "Name", "Unit", "UpdatedAt" },
                values: new object[] { new Guid("7c57cfd1-5bb6-4d4e-8959-9e87d240d481"), "RECOVERY_PLAN_REQUEST", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Quota for requesting a doctor-created recovery plan.", true, false, "Recovery Plan Request", "request", null });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlan_DoctorId",
                table: "RecoveryPlan",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlan_RecoveryPlanRequestId",
                table: "RecoveryPlan",
                column: "RecoveryPlanRequestId",
                unique: true,
                filter: "\"RecoveryPlanRequestId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlan_UserId",
                table: "RecoveryPlan",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_DedupeKey",
                table: "Notification",
                column: "DedupeKey",
                unique: true,
                filter: "\"DedupeKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_ReferenceType_ReferenceId",
                table: "Notification",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Status_ScheduledAt",
                table: "Notification",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Doctor_MaxConcurrentRecoveryPlanRequests",
                table: "Doctor",
                sql: "\"MaxConcurrentRecoveryPlanRequests\" IS NULL OR \"MaxConcurrentRecoveryPlanRequests\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_AggregateType_AggregateId",
                table: "OutboxMessage",
                columns: new[] { "AggregateType", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Status_NextAttemptAt",
                table: "OutboxMessage",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Quota_Code",
                table: "Quota",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanFoodSource_RecoveryPlanNutrientTargetId",
                table: "RecoveryPlanFoodSource",
                column: "RecoveryPlanNutrientTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanFoodSource_RecoveryPlanNutrientTargetId_SortOrd~",
                table: "RecoveryPlanFoodSource",
                columns: new[] { "RecoveryPlanNutrientTargetId", "SortOrder" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanNutrientTarget_RecoveryPlanPhaseId",
                table: "RecoveryPlanNutrientTarget",
                column: "RecoveryPlanPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanNutrientTarget_RecoveryPlanPhaseId_SortOrder",
                table: "RecoveryPlanNutrientTarget",
                columns: new[] { "RecoveryPlanPhaseId", "SortOrder" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanPhase_RecoveryPlanId",
                table: "RecoveryPlanPhase",
                column: "RecoveryPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanPhase_RecoveryPlanId_SortOrder",
                table: "RecoveryPlanPhase",
                columns: new[] { "RecoveryPlanId", "SortOrder" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequest_AssignedDoctorId",
                table: "RecoveryPlanRequest",
                column: "AssignedDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequest_AssignmentExpiresAt",
                table: "RecoveryPlanRequest",
                column: "AssignmentExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequest_PrimaryLabTestSessionId",
                table: "RecoveryPlanRequest",
                column: "PrimaryLabTestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequest_Status_RequestedAt",
                table: "RecoveryPlanRequest",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequest_TreatmentJourneyId",
                table: "RecoveryPlanRequest",
                column: "TreatmentJourneyId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequest_UserId",
                table: "RecoveryPlanRequest",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequest_UserSubscriptionId",
                table: "RecoveryPlanRequest",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequest_UserSubscriptionUsageId_Status",
                table: "RecoveryPlanRequest",
                columns: new[] { "UserSubscriptionUsageId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequestEvent_ActorDoctorId",
                table: "RecoveryPlanRequestEvent",
                column: "ActorDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequestEvent_ActorUserId",
                table: "RecoveryPlanRequestEvent",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequestEvent_EventType",
                table: "RecoveryPlanRequestEvent",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanRequestEvent_RecoveryPlanRequestId_CreatedAt",
                table: "RecoveryPlanRequestEvent",
                columns: new[] { "RecoveryPlanRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanQuota_PlanId_QuotaId",
                table: "SubscriptionPlanQuota",
                columns: new[] { "PlanId", "QuotaId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanQuota_QuotaId",
                table: "SubscriptionPlanQuota",
                column: "QuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMedicationReminderTime_UserMedicationId",
                table: "UserMedicationReminderTime",
                column: "UserMedicationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMedicationReminderTime_UserMedicationId_TimeOfDay",
                table: "UserMedicationReminderTime",
                columns: new[] { "UserMedicationId", "TimeOfDay" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionLog_IdempotencyKey",
                table: "UserSubscriptionLog",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionLog_PerformedByUserId",
                table: "UserSubscriptionLog",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionLog_QuotaId",
                table: "UserSubscriptionLog",
                column: "QuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionLog_ReferenceType_ReferenceId",
                table: "UserSubscriptionLog",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionLog_UserSubscriptionId",
                table: "UserSubscriptionLog",
                column: "UserSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionLog_UserSubscriptionUsageId_CreatedAt",
                table: "UserSubscriptionLog",
                columns: new[] { "UserSubscriptionUsageId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionUsage_QuotaId",
                table: "UserSubscriptionUsage",
                column: "QuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionUsage_UserSubscriptionId_CycleEnd",
                table: "UserSubscriptionUsage",
                columns: new[] { "UserSubscriptionId", "CycleEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptionUsage_UserSubscriptionId_QuotaId_CycleStart~",
                table: "UserSubscriptionUsage",
                columns: new[] { "UserSubscriptionId", "QuotaId", "CycleStart", "CycleEnd" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_FollowUpReminder_ReminderId",
                table: "Notification",
                column: "ReminderId",
                principalTable: "FollowUpReminder",
                principalColumn: "ReminderId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoveryPlan_AspNetUsers_UserId",
                table: "RecoveryPlan",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoveryPlan_Doctor_DoctorId",
                table: "RecoveryPlan",
                column: "DoctorId",
                principalTable: "Doctor",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoveryPlan_RecoveryPlanRequest_RecoveryPlanRequestId",
                table: "RecoveryPlan",
                column: "RecoveryPlanRequestId",
                principalTable: "RecoveryPlanRequest",
                principalColumn: "RecoveryPlanRequestId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoveryPlan_TreatmentJourney_TreatmentJourneyId",
                table: "RecoveryPlan",
                column: "TreatmentJourneyId",
                principalTable: "TreatmentJourney",
                principalColumn: "TreatmentJourneyId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notification_FollowUpReminder_ReminderId",
                table: "Notification");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoveryPlan_AspNetUsers_UserId",
                table: "RecoveryPlan");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoveryPlan_Doctor_DoctorId",
                table: "RecoveryPlan");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoveryPlan_RecoveryPlanRequest_RecoveryPlanRequestId",
                table: "RecoveryPlan");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoveryPlan_TreatmentJourney_TreatmentJourneyId",
                table: "RecoveryPlan");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Notification" WHERE "ReminderId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back: Notification.ReminderId contains NULL values.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "RecoveryPlan" WHERE "TreatmentJourneyId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back: RecoveryPlan.TreatmentJourneyId contains NULL values.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "RecoveryPlanFoodSource");

            migrationBuilder.DropTable(
                name: "RecoveryPlanRequestEvent");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanQuota");

            migrationBuilder.DropTable(
                name: "UserMedicationReminderTime");

            migrationBuilder.DropTable(
                name: "UserSubscriptionLog");

            migrationBuilder.DropTable(
                name: "RecoveryPlanNutrientTarget");

            migrationBuilder.DropTable(
                name: "RecoveryPlanRequest");

            migrationBuilder.DropTable(
                name: "RecoveryPlanPhase");

            migrationBuilder.DropTable(
                name: "UserSubscriptionUsage");

            migrationBuilder.DropTable(
                name: "Quota");

            migrationBuilder.DropIndex(
                name: "IX_RecoveryPlan_DoctorId",
                table: "RecoveryPlan");

            migrationBuilder.DropIndex(
                name: "IX_RecoveryPlan_RecoveryPlanRequestId",
                table: "RecoveryPlan");

            migrationBuilder.DropIndex(
                name: "IX_RecoveryPlan_UserId",
                table: "RecoveryPlan");

            migrationBuilder.DropIndex(
                name: "IX_Notification_DedupeKey",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_Notification_ReferenceType_ReferenceId",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_Notification_Status_ScheduledAt",
                table: "Notification");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Doctor_MaxConcurrentRecoveryPlanRequests",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "IsReminderEnabled",
                table: "UserMedication");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "UserMedication");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "ClinicalSnapshotJson",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "RecheckInstruction",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "RecoveryPlanRequestId",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "RecoveryPlan");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "DedupeKey",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "NotificationType",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "IsAcceptingRecoveryPlanRequests",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "MaxConcurrentRecoveryPlanRequests",
                table: "Doctor");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<Guid>(
                name: "TreatmentJourneyId",
                table: "RecoveryPlan",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "StartDate",
                table: "RecoveryPlan",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EndDate",
                table: "RecoveryPlan",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notification",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Notification",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReminderId",
                table: "Notification",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notification",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "Notification",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_FollowUpReminder_ReminderId",
                table: "Notification",
                column: "ReminderId",
                principalTable: "FollowUpReminder",
                principalColumn: "ReminderId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoveryPlan_TreatmentJourney_TreatmentJourneyId",
                table: "RecoveryPlan",
                column: "TreatmentJourneyId",
                principalTable: "TreatmentJourney",
                principalColumn: "TreatmentJourneyId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
