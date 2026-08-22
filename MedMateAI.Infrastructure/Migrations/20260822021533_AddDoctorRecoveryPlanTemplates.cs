using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorRecoveryPlanTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecoveryPlanTemplate",
                columns: table => new
                {
                    RecoveryPlanTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiseaseGroup = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlanName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecheckInstruction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryPlanTemplate", x => x.RecoveryPlanTemplateId);
                    table.CheckConstraint("CK_RecoveryPlanTemplate_DurationDays", "\"DurationDays\" >= 1 AND \"DurationDays\" <= 365");
                    table.ForeignKey(
                        name: "FK_RecoveryPlanTemplate_Doctor_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctor",
                        principalColumn: "DoctorId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryPlanTemplatePhase",
                columns: table => new
                {
                    RecoveryPlanTemplatePhaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecoveryPlanTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhaseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDay = table.Column<int>(type: "integer", nullable: false),
                    EndDay = table.Column<int>(type: "integer", nullable: false),
                    SleepAndRestHoursPerDay = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Instruction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryPlanTemplatePhase", x => x.RecoveryPlanTemplatePhaseId);
                    table.CheckConstraint("CK_RecoveryPlanTemplatePhase_Days", "\"StartDay\" >= 1 AND \"EndDay\" >= \"StartDay\"");
                    table.CheckConstraint("CK_RecoveryPlanTemplatePhase_SleepAndRest", "\"SleepAndRestHoursPerDay\" IS NULL OR (\"SleepAndRestHoursPerDay\" >= 0 AND \"SleepAndRestHoursPerDay\" <= 24)");
                    table.CheckConstraint("CK_RecoveryPlanTemplatePhase_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_RecoveryPlanTemplatePhase_RecoveryPlanTemplate_RecoveryPlan~",
                        column: x => x.RecoveryPlanTemplateId,
                        principalTable: "RecoveryPlanTemplate",
                        principalColumn: "RecoveryPlanTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryPlanTemplateNutrientTarget",
                columns: table => new
                {
                    RecoveryPlanTemplateNutrientTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecoveryPlanTemplatePhaseId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_RecoveryPlanTemplateNutrientTarget", x => x.RecoveryPlanTemplateNutrientTargetId);
                    table.CheckConstraint("CK_RecoveryPlanTemplateNutrientTarget_Amount", "\"AmountPerDay\" > 0");
                    table.CheckConstraint("CK_RecoveryPlanTemplateNutrientTarget_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_RecoveryPlanTemplateNutrientTarget_RecoveryPlanTemplatePhas~",
                        column: x => x.RecoveryPlanTemplatePhaseId,
                        principalTable: "RecoveryPlanTemplatePhase",
                        principalColumn: "RecoveryPlanTemplatePhaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryPlanTemplateFoodSource",
                columns: table => new
                {
                    RecoveryPlanTemplateFoodSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecoveryPlanTemplateNutrientTargetId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_RecoveryPlanTemplateFoodSource", x => x.RecoveryPlanTemplateFoodSourceId);
                    table.CheckConstraint("CK_RecoveryPlanTemplateFoodSource_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_RecoveryPlanTemplateFoodSource_RecoveryPlanTemplateNutrient~",
                        column: x => x.RecoveryPlanTemplateNutrientTargetId,
                        principalTable: "RecoveryPlanTemplateNutrientTarget",
                        principalColumn: "RecoveryPlanTemplateNutrientTargetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanTemplate_DoctorId",
                table: "RecoveryPlanTemplate",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanTemplate_DoctorId_DiseaseGroup",
                table: "RecoveryPlanTemplate",
                columns: new[] { "DoctorId", "DiseaseGroup" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanTemplateFoodSource_RecoveryPlanTemplateNutrien~1",
                table: "RecoveryPlanTemplateFoodSource",
                columns: new[] { "RecoveryPlanTemplateNutrientTargetId", "SortOrder" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanTemplateFoodSource_RecoveryPlanTemplateNutrient~",
                table: "RecoveryPlanTemplateFoodSource",
                column: "RecoveryPlanTemplateNutrientTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanTemplateNutrientTarget_RecoveryPlanTemplatePha~1",
                table: "RecoveryPlanTemplateNutrientTarget",
                columns: new[] { "RecoveryPlanTemplatePhaseId", "SortOrder" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanTemplateNutrientTarget_RecoveryPlanTemplatePhas~",
                table: "RecoveryPlanTemplateNutrientTarget",
                column: "RecoveryPlanTemplatePhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanTemplatePhase_RecoveryPlanTemplateId",
                table: "RecoveryPlanTemplatePhase",
                column: "RecoveryPlanTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryPlanTemplatePhase_RecoveryPlanTemplateId_SortOrder",
                table: "RecoveryPlanTemplatePhase",
                columns: new[] { "RecoveryPlanTemplateId", "SortOrder" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecoveryPlanTemplateFoodSource");

            migrationBuilder.DropTable(
                name: "RecoveryPlanTemplateNutrientTarget");

            migrationBuilder.DropTable(
                name: "RecoveryPlanTemplatePhase");

            migrationBuilder.DropTable(
                name: "RecoveryPlanTemplate");
        }
    }
}
