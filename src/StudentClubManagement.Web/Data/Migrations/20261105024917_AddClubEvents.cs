using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentClubManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RegistrationDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint("CK_Events_Audience", "[Audience] IN (1,2)");
                    table.CheckConstraint("CK_Events_Capacity", "[Capacity] > 0");
                    table.CheckConstraint("CK_Events_Period", "[StartsAt] < [EndsAt] AND [RegistrationDeadline] <= [StartsAt]");
                    table.CheckConstraint("CK_Events_Publication", "([Status] = 1 AND [PublishedAt] IS NULL) OR ([Status] IN (2,4) AND [PublishedAt] IS NOT NULL) OR [Status] = 3");
                    table.CheckConstraint("CK_Events_Status", "[Status] IN (1,2,3,4)");
                    table.CheckConstraint("CK_Events_Text", "LEN(LTRIM(RTRIM([Title]))) > 0 AND LEN(LTRIM(RTRIM([Location]))) > 0");
                    table.ForeignKey(
                        name: "FK_Events_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_ClubId_StartsAt",
                table: "Events",
                columns: new[] { "ClubId", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Events]) THROW 51000, 'Cannot remove Events migration while event history exists.', 1;");
            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
