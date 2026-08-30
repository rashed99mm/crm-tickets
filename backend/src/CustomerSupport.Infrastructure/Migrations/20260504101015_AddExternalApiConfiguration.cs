using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalApiConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalApiConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AuthType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuthKeyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuthKeyLocation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AuthValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AuthTokenUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthClientId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthClientSecret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthScope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuthAutoRefresh = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalApiConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Status",
                table: "Notifications",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsActive",
                table: "AspNetUsers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApiConfigurations_Name",
                table: "ExternalApiConfigurations",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalApiConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_Status",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_IsActive",
                table: "AspNetUsers");
        }
    }
}
