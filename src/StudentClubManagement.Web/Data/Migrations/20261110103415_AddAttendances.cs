using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentClubManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    RegistrationId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MarkedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MarkedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.RegistrationId);
                    table.CheckConstraint("CK_Attendances_Status", "[Status] IN (0,1,2,3)");
                    table.ForeignKey(
                        name: "FK_Attendances_EventRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "EventRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendances_Users_MarkedById",
                        column: x => x.MarkedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_MarkedById",
                table: "Attendances",
                column: "MarkedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM Attendances) THROW 51000, 'Cannot discard attendance history.', 1;");
            migrationBuilder.DropTable(
                name: "Attendances");
        }
    }
}
