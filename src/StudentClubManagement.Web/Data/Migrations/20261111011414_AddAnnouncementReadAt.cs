using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentClubManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementReadAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadAt",
                table: "AnnouncementRecipients",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM AnnouncementRecipients WHERE ReadAt IS NOT NULL) THROW 51000, 'Cannot discard announcement read history.', 1;");
            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "AnnouncementRecipients");
        }
    }
}
