using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedMateAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleCampaignAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AnnounceToUsers",
                table: "SaleCampaign",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnounceToUsers",
                table: "SaleCampaign");
        }
    }
}
