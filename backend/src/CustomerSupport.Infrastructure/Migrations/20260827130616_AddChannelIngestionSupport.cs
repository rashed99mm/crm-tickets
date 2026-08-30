using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelIngestionSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "TicketMessages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_Channel_ProviderMessageId",
                table: "TicketMessages",
                columns: new[] { "Channel", "ProviderMessageId" },
                unique: true,
                filter: "[ProviderMessageId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_Channel_ProviderMessageId",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "TicketMessages");
        }
    }
}
