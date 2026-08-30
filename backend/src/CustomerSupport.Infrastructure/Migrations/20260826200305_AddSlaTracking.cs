using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionDueAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDueAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SLAEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TargetAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BreachedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedSeconds = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SLAEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SLAEvents_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SLAPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ResponseTargetHours = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ResolutionTargetHours = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SLAPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SLAEvents_Ticket_TargetType",
                table: "SLAEvents",
                columns: new[] { "TicketId", "TargetType" });

            migrationBuilder.CreateIndex(
                name: "IX_SLAPolicies_Priority_Active",
                table: "SLAPolicies",
                columns: new[] { "Priority", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SLAEvents");

            migrationBuilder.DropTable(
                name: "SLAPolicies");

            migrationBuilder.DropColumn(
                name: "ResolutionDueAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ResponseDueAt",
                table: "Tickets");
        }
    }
}
