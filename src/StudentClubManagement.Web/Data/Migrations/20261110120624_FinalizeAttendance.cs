using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentClubManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AttendanceFinalizedAt",
                table: "Events",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInFinalRoster",
                table: "Attendances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_FinalizedTime",
                table: "Events",
                sql: "[AttendanceFinalizedAt] IS NULL OR ([AttendanceFinalizedAt] >= [EndsAt] AND [PublishedAt] IS NOT NULL AND [Status] IN (2,3,4))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Attendances_FinalResult",
                table: "Attendances",
                sql: "[IsInFinalRoster] = 0 OR [Status] IN (1,2,3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM Events WHERE AttendanceFinalizedAt IS NOT NULL) OR EXISTS (SELECT 1 FROM Attendances WHERE IsInFinalRoster=1) THROW 51000, 'Cannot discard finalized attendance roster.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_FinalizedTime",
                table: "Events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Attendances_FinalResult",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "AttendanceFinalizedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsInFinalRoster",
                table: "Attendances");
        }
    }
}
