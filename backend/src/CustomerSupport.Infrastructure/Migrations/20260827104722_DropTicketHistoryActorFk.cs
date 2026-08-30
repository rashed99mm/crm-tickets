using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropTicketHistoryActorFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketHistory_AspNetUsers_ActorId",
                table: "TicketHistory");

            migrationBuilder.DropIndex(
                name: "IX_TicketHistory_ActorId",
                table: "TicketHistory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TicketHistory_ActorId",
                table: "TicketHistory",
                column: "ActorId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketHistory_AspNetUsers_ActorId",
                table: "TicketHistory",
                column: "ActorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
