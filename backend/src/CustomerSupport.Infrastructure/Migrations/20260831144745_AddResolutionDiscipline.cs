using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResolutionDiscipline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReopenCount",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionCode",
                table: "Tickets",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNotes",
                table: "Tickets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReopenCount",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolutionCode",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResolutionNotes",
                table: "Tickets");
        }
    }
}
