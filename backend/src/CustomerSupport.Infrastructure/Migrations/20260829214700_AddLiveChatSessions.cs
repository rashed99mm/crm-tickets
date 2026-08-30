using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    public partial class AddLiveChatSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiveChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClaimedByAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveChatSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveChatMessages_LiveChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "LiveChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatMessages_SessionId_SentAt",
                table: "LiveChatMessages",
                columns: new[] { "SessionId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatSessions_SessionTokenHash",
                table: "LiveChatSessions",
                column: "SessionTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatSessions_Status_CreatedAt",
                table: "LiveChatSessions",
                columns: new[] { "Status", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LiveChatMessages");
            migrationBuilder.DropTable(name: "LiveChatSessions");
        }
    }
}
