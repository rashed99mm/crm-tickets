using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImpactUrgencyClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Impact",
                table: "Tickets",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Urgency",
                table: "Tickets",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Impact",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Urgency",
                table: "Tickets");
        }
    }
}
