using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLabTestOcrAndReferenceRangeEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentUrl",
                table: "LabTestSession",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacilityName",
                table: "LabTestSession",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatientAgeAtTest",
                table: "LabTestSession",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatientGenderAtTest",
                table: "LabTestSession",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "LabTestSession",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "LabTestSession",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TestDate",
                table: "LabTestSession",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabTestResultDetail"
                ALTER COLUMN "Status" TYPE integer
                USING (
                    CASE
                        WHEN "Status" IS NULL THEN 0
                        WHEN "Status" ~ '^[0-9]+$' THEN "Status"::integer
                        WHEN lower("Status") = 'unknown' THEN 0
                        WHEN lower("Status") = 'normal' THEN 1
                        WHEN lower("Status") = 'high' THEN 2
                        WHEN lower("Status") = 'low' THEN 3
                        WHEN lower("Status") = 'criticalhigh' THEN 4
                        WHEN lower("Status") = 'criticallow' THEN 5
                        ELSE 0
                    END
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabTestResultDetail"
                ALTER COLUMN "Status" SET NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabTestResultDetail"
                ALTER COLUMN "Status" SET DEFAULT 0;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "IndicatorId",
                table: "LabTestResultDetail",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AdviceCacheId",
                table: "LabTestResultDetail",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeviationPercent",
                table: "LabTestResultDetail",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMatched",
                table: "LabTestResultDetail",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "MatchConfidence",
                table: "LabTestResultDetail",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawExtractedName",
                table: "LabTestResultDetail",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawExtractedValue",
                table: "LabTestResultDetail",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ReferenceMaxUsed",
                table: "LabTestResultDetail",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ReferenceMinUsed",
                table: "LabTestResultDetail",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceUnitUsed",
                table: "LabTestResultDetail",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "LabIndicatorMaster",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LabIndicatorMaster",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.DropIndex(
                name: "IX_LabIndicatorAdviceCache_IndicatorId_Status",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabIndicatorAdviceCache"
                ALTER COLUMN "Status" TYPE integer
                USING (
                    CASE
                        WHEN "Status" IS NULL THEN 0
                        WHEN "Status" ~ '^[0-9]+$' THEN "Status"::integer
                        WHEN lower("Status") = 'unknown' THEN 0
                        WHEN lower("Status") = 'normal' THEN 1
                        WHEN lower("Status") = 'high' THEN 2
                        WHEN lower("Status") = 'low' THEN 3
                        WHEN lower("Status") = 'criticalhigh' THEN 4
                        WHEN lower("Status") = 'criticallow' THEN 5
                        ELSE 0
                    END
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabIndicatorAdviceCache"
                ALTER COLUMN "Status" SET NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabIndicatorAdviceCache"
                ALTER COLUMN "Status" SET DEFAULT 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorAdviceCache_IndicatorId_Status",
                table: "LabIndicatorAdviceCache",
                columns: new[] { "IndicatorId", "Status" },
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayTitle",
                table: "LabIndicatorAdviceCache",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeverityLevel",
                table: "LabIndicatorAdviceCache",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "LabIndicatorAdviceCache",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabIndicatorAlias",
                columns: table => new
                {
                    AliasId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AliasText = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabIndicatorAlias", x => x.AliasId);
                    table.ForeignKey(
                        name: "FK_LabIndicatorAlias_LabIndicatorMaster_IndicatorId",
                        column: x => x.IndicatorId,
                        principalTable: "LabIndicatorMaster",
                        principalColumn: "IndicatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabIndicatorReferenceRange",
                columns: table => new
                {
                    ReferenceRangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    AgeGroup = table.Column<int>(type: "integer", nullable: true),
                    ComparisonType = table.Column<int>(type: "integer", nullable: false),
                    MinValue = table.Column<double>(type: "double precision", nullable: true),
                    MaxValue = table.Column<double>(type: "double precision", nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabIndicatorReferenceRange", x => x.ReferenceRangeId);
                    table.ForeignKey(
                        name: "FK_LabIndicatorReferenceRange_LabIndicatorMaster_IndicatorId",
                        column: x => x.IndicatorId,
                        principalTable: "LabIndicatorMaster",
                        principalColumn: "IndicatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabTestOcrExtract",
                columns: table => new
                {
                    OcrExtractId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    ExtractedTestName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ExtractedValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExtractedUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExtractedReferenceText = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestOcrExtract", x => x.OcrExtractId);
                    table.ForeignKey(
                        name: "FK_LabTestOcrExtract_LabTestSession_TestSessionId",
                        column: x => x.TestSessionId,
                        principalTable: "LabTestSession",
                        principalColumn: "TestSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabTestResultDetail_AdviceCacheId",
                table: "LabTestResultDetail",
                column: "AdviceCacheId");

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorAlias_AliasText",
                table: "LabIndicatorAlias",
                column: "AliasText");

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorAlias_IndicatorId_AliasText",
                table: "LabIndicatorAlias",
                columns: new[] { "IndicatorId", "AliasText" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorReferenceRange_IndicatorId_Gender_AgeGroup",
                table: "LabIndicatorReferenceRange",
                columns: new[] { "IndicatorId", "Gender", "AgeGroup" });

            migrationBuilder.CreateIndex(
                name: "IX_LabTestOcrExtract_TestSessionId_RowIndex",
                table: "LabTestOcrExtract",
                columns: new[] { "TestSessionId", "RowIndex" });

            migrationBuilder.AddForeignKey(
                name: "FK_LabTestResultDetail_LabIndicatorAdviceCache_AdviceCacheId",
                table: "LabTestResultDetail",
                column: "AdviceCacheId",
                principalTable: "LabIndicatorAdviceCache",
                principalColumn: "CacheId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabTestResultDetail_LabIndicatorAdviceCache_AdviceCacheId",
                table: "LabTestResultDetail");

            migrationBuilder.DropTable(
                name: "LabIndicatorAlias");

            migrationBuilder.DropTable(
                name: "LabIndicatorReferenceRange");

            migrationBuilder.DropTable(
                name: "LabTestOcrExtract");

            migrationBuilder.DropIndex(
                name: "IX_LabTestResultDetail_AdviceCacheId",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "DocumentUrl",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "FacilityName",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "PatientAgeAtTest",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "PatientGenderAtTest",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "TestDate",
                table: "LabTestSession");

            migrationBuilder.DropColumn(
                name: "AdviceCacheId",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "DeviationPercent",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "IsMatched",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "MatchConfidence",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "RawExtractedName",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "RawExtractedValue",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "ReferenceMaxUsed",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "ReferenceMinUsed",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "ReferenceUnitUsed",
                table: "LabTestResultDetail");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "LabIndicatorMaster");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LabIndicatorMaster");

            migrationBuilder.DropColumn(
                name: "DisplayTitle",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.DropColumn(
                name: "SeverityLevel",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.DropIndex(
                name: "IX_LabIndicatorAdviceCache_IndicatorId_Status",
                table: "LabIndicatorAdviceCache");

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabTestResultDetail"
                ALTER COLUMN "Status" TYPE character varying(50)
                USING (
                    CASE "Status"
                        WHEN 0 THEN 'Unknown'
                        WHEN 1 THEN 'Normal'
                        WHEN 2 THEN 'High'
                        WHEN 3 THEN 'Low'
                        WHEN 4 THEN 'CriticalHigh'
                        WHEN 5 THEN 'CriticalLow'
                        ELSE 'Unknown'
                    END
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabTestResultDetail"
                ALTER COLUMN "Status" DROP NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabIndicatorAdviceCache"
                ALTER COLUMN "Status" TYPE character varying(50)
                USING (
                    CASE "Status"
                        WHEN 0 THEN 'Unknown'
                        WHEN 1 THEN 'Normal'
                        WHEN 2 THEN 'High'
                        WHEN 3 THEN 'Low'
                        WHEN 4 THEN 'CriticalHigh'
                        WHEN 5 THEN 'CriticalLow'
                        ELSE 'Unknown'
                    END
                );
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "LabIndicatorAdviceCache"
                ALTER COLUMN "Status" DROP NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LabIndicatorAdviceCache_IndicatorId_Status",
                table: "LabIndicatorAdviceCache",
                columns: new[] { "IndicatorId", "Status" },
                unique: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "IndicatorId",
                table: "LabTestResultDetail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
