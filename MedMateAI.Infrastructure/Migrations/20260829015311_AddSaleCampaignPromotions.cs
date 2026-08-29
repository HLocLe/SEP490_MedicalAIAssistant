using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleCampaignPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaleCampaign",
                columns: table => new
                {
                    SaleCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BadgeText = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EligibilityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "All"),
                    MaxRedemptions = table.Column<int>(type: "integer", nullable: true),
                    MaxRedemptionsPerUser = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleCampaign", x => x.SaleCampaignId);
                    table.CheckConstraint("CK_SaleCampaign_Limits", "\"MaxRedemptions\" IS NULL OR \"MaxRedemptionsPerUser\" IS NULL OR \"MaxRedemptionsPerUser\" <= \"MaxRedemptions\"");
                    table.CheckConstraint("CK_SaleCampaign_MaxRedemptions", "\"MaxRedemptions\" IS NULL OR \"MaxRedemptions\" >= 1");
                    table.CheckConstraint("CK_SaleCampaign_MaxRedemptionsPerUser", "\"MaxRedemptionsPerUser\" IS NULL OR \"MaxRedemptionsPerUser\" >= 1");
                    table.CheckConstraint("CK_SaleCampaign_Priority", "\"Priority\" >= 0 AND \"Priority\" <= 1000");
                    table.CheckConstraint("CK_SaleCampaign_Window", "\"EndAt\" > \"StartAt\"");
                });

            migrationBuilder.CreateTable(
                name: "SaleCampaignPlan",
                columns: table => new
                {
                    SaleCampaignPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BonusCredit = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleCampaignPlan", x => x.SaleCampaignPlanId);
                    table.CheckConstraint("CK_SaleCampaignPlan_Benefit", "\"SalePrice\" IS NOT NULL OR \"BonusCredit\" > 0");
                    table.CheckConstraint("CK_SaleCampaignPlan_BonusCredit", "\"BonusCredit\" >= 0");
                    table.CheckConstraint("CK_SaleCampaignPlan_SalePrice", "\"SalePrice\" IS NULL OR \"SalePrice\" > 0");
                    table.ForeignKey(
                        name: "FK_SaleCampaignPlan_SaleCampaign_SaleCampaignId",
                        column: x => x.SaleCampaignId,
                        principalTable: "SaleCampaign",
                        principalColumn: "SaleCampaignId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleCampaignPlan_SubscriptionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlan",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleRedemption",
                columns: table => new
                {
                    SaleRedemptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleCampaignPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignNameSnapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BadgeTextSnapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EligibilityTypeSnapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseCredit = table.Column<int>(type: "integer", nullable: false),
                    BonusCredit = table.Column<int>(type: "integer", nullable: false),
                    GrantedCredit = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Reserved"),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleRedemption", x => x.SaleRedemptionId);
                    table.CheckConstraint("CK_SaleRedemption_Credits", "\"BaseCredit\" > 0 AND \"BonusCredit\" >= 0 AND \"GrantedCredit\" = \"BaseCredit\" + \"BonusCredit\"");
                    table.CheckConstraint("CK_SaleRedemption_Prices", "\"OriginalPrice\" > 0 AND \"FinalPrice\" > 0 AND \"FinalPrice\" <= \"OriginalPrice\"");
                    table.ForeignKey(
                        name: "FK_SaleRedemption_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleRedemption_Payment_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payment",
                        principalColumn: "PaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleRedemption_SaleCampaignPlan_SaleCampaignPlanId",
                        column: x => x.SaleCampaignPlanId,
                        principalTable: "SaleCampaignPlan",
                        principalColumn: "SaleCampaignPlanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleRedemption_SaleCampaign_SaleCampaignId",
                        column: x => x.SaleCampaignId,
                        principalTable: "SaleCampaign",
                        principalColumn: "SaleCampaignId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleRedemption_SubscriptionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlan",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleRedemption_UserSubscription_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscription",
                        principalColumn: "UserSubscriptionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleCampaign_IsActive_StartAt_EndAt_Priority",
                table: "SaleCampaign",
                columns: new[] { "IsActive", "StartAt", "EndAt", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleCampaignPlan_PlanId_IsActive",
                table: "SaleCampaignPlan",
                columns: new[] { "PlanId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleCampaignPlan_SaleCampaignId_PlanId",
                table: "SaleCampaignPlan",
                columns: new[] { "SaleCampaignId", "PlanId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRedemption_PaymentId",
                table: "SaleRedemption",
                column: "PaymentId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRedemption_PlanId",
                table: "SaleRedemption",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRedemption_SaleCampaignId_Status",
                table: "SaleRedemption",
                columns: new[] { "SaleCampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleRedemption_SaleCampaignId_UserId_Status",
                table: "SaleRedemption",
                columns: new[] { "SaleCampaignId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleRedemption_SaleCampaignPlanId",
                table: "SaleRedemption",
                column: "SaleCampaignPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRedemption_UserId",
                table: "SaleRedemption",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRedemption_UserSubscriptionId",
                table: "SaleRedemption",
                column: "UserSubscriptionId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_SaleRedemption_FirstPurchase_User",
                table: "SaleRedemption",
                columns: new[] { "UserId", "EligibilityTypeSnapshot" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"EligibilityTypeSnapshot\" = 'FirstPurchase' AND \"Status\" IN ('Reserved', 'Completed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleRedemption");

            migrationBuilder.DropTable(
                name: "SaleCampaignPlan");

            migrationBuilder.DropTable(
                name: "SaleCampaign");
        }
    }
}
