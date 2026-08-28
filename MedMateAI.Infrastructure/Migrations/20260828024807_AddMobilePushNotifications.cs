using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePushNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "Notification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "Notification",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderSubmittedAt",
                table: "Notification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PushDeviceId",
                table: "Notification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceiptAttemptCount",
                table: "Notification",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserPushDevice",
                columns: table => new
                {
                    UserPushDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpoPushToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPushDevice", x => x.UserPushDeviceId);
                    table.ForeignKey(
                        name: "FK_UserPushDevice_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Channel_Status_NextAttemptAt",
                table: "Notification",
                columns: new[] { "Channel", "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_PushDeviceId",
                table: "Notification",
                column: "PushDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPushDevice_ExpoPushToken",
                table: "UserPushDevice",
                column: "ExpoPushToken",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserPushDevice_UserId_InstallationId",
                table: "UserPushDevice",
                columns: new[] { "UserId", "InstallationId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserPushDevice_UserId_IsActive",
                table: "UserPushDevice",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_UserPushDevice_PushDeviceId",
                table: "Notification",
                column: "PushDeviceId",
                principalTable: "UserPushDevice",
                principalColumn: "UserPushDeviceId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notification_UserPushDevice_PushDeviceId",
                table: "Notification");

            migrationBuilder.DropTable(
                name: "UserPushDevice");

            migrationBuilder.DropIndex(
                name: "IX_Notification_Channel_Status_NextAttemptAt",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_Notification_PushDeviceId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ProviderSubmittedAt",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "PushDeviceId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ReceiptAttemptCount",
                table: "Notification");
        }
    }
}
