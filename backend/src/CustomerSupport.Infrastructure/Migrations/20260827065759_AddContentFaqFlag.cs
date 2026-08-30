using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentFaqFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: this migration was generated while a concurrent session's uncommitted
            // Permission/RolePermission entities (FEAT-19 permissions, US-804/805) were sitting
            // untracked in the working tree — EF's migration scaffolding diffs the whole shared
            // model, so it swept their schema into this migration too. Their tables are
            // deliberately NOT created here; this migration is scoped to FEAT-11's IsFaq column
            // only. Their own session will pick up Permissions/RolePermissions in its own
            // migration once it's ready, diffed against the snapshot below (which also excludes
            // them for the same reason).
            migrationBuilder.AddColumn<bool>(
                name: "IsFaq",
                table: "Contents",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFaq",
                table: "Contents");
        }
    }
}
