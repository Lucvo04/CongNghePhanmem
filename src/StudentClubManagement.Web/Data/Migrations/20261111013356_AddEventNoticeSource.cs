using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentClubManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventNoticeSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Announcements",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_SourceKey",
                table: "Announcements",
                column: "SourceKey",
                unique: true,
                filter: "[SourceKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Announcements_Source",
                table: "Announcements",
                sql: "[SourceKey] IS NULL OR (LEN([SourceKey]) = 64 AND [EventId] IS NOT NULL AND [Status] IN (2,3))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM Announcements WHERE SourceKey IS NOT NULL) THROW 51000, 'Cannot discard event notification source history.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_Announcements_SourceKey",
                table: "Announcements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Announcements_Source",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Announcements");
        }
    }
}
