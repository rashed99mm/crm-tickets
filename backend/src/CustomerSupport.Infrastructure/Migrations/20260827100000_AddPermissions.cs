using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations;

public partial class AddPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Permissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table => table.PrimaryKey("PK_Permissions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "RolePermissions",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                table.ForeignKey("FK_RolePermissions_AspNetRoles_RoleId", x => x.RoleId, "AspNetRoles", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_RolePermissions_Permissions_PermissionId", x => x.PermissionId, "Permissions", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "UQ_Permissions_Name",
            table: "Permissions",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_PermissionId",
            table: "RolePermissions",
            column: "PermissionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RolePermissions");
        migrationBuilder.DropTable(name: "Permissions");
    }
}
