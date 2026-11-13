using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentClubManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJoinRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinRequests", x => x.Id);
                    table.CheckConstraint("CK_JoinRequests_Period", "([Status] = 1 AND [ClosedAt] IS NULL) OR ([Status] = 2 AND [ClosedAt] >= [SubmittedAt] AND [ClosedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_JoinRequests_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.CheckConstraint("CK_JoinRequests_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_JoinRequests_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ClubId_Status_SubmittedAt",
                table: "JoinRequests",
                columns: new[] { "ClubId", "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_UserId_SubmittedAt",
                table: "JoinRequests",
                columns: new[] { "UserId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_JoinRequests_Pending",
                table: "JoinRequests",
                columns: new[] { "UserId", "ClubId" },
                unique: true,
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinRequests");
        }
    }
}
