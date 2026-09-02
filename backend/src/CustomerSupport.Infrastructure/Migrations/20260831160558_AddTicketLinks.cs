using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketLinks_Tickets_SourceTicketId",
                        column: x => x.SourceTicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketLinks_Tickets_TargetTicketId",
                        column: x => x.TargetTicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketLinks_TargetTicketId",
                table: "TicketLinks",
                column: "TargetTicketId");

            migrationBuilder.CreateIndex(
                name: "UX_TicketLinks_Source_Target_Type",
                table: "TicketLinks",
                columns: new[] { "SourceTicketId", "TargetTicketId", "LinkType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketLinks");
        }
    }
}
