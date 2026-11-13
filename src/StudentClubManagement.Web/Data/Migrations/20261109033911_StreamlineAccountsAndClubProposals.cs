using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentClubManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class StreamlineAccountsAndClubProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse ambiguous conversions instead of silently choosing a role or losing authentication data.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT UserId FROM UserRoles GROUP BY UserId HAVING COUNT(*) > 1)
                    THROW 51000, 'Resolve accounts with multiple system roles before upgrading.', 1;
                IF EXISTS (SELECT 1 FROM Users WHERE UserName <> Email OR NormalizedUserName <> NormalizedEmail
                    OR EmailConfirmed = 1 OR PhoneNumberConfirmed = 1 OR TwoFactorEnabled = 1)
                    THROW 51000, 'Authentication data needs review before simplifying accounts.', 1;
                IF EXISTS (SELECT 1 FROM Roles WHERE Id NOT IN ('system-admin', 'system-student')
                    OR (Id = 'system-admin' AND Name <> 'Admin') OR (Id = 'system-student' AND Name <> 'Student'))
                    THROW 51000, 'Only the fixed Admin and Student roles are supported.', 1;
                """);
            migrationBuilder.AddColumn<string>(
                name: "RoleId", table: "Users", type: "nvarchar(450)", maxLength: 450, nullable: true);
            migrationBuilder.Sql("UPDATE u SET RoleId = r.RoleId FROM Users u JOIN UserRoles r ON r.UserId = u.Id;");
            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Status",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedUserName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumberConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Roles");

            migrationBuilder.CreateTable(
                name: "ClubProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicantId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClubId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubProposals", x => x.Id);
                    table.CheckConstraint("CK_ClubProposals_Club", "([Status]=2 AND [ClubId] IS NOT NULL) OR ([Status]<>2 AND [ClubId] IS NULL)");
                    table.CheckConstraint("CK_ClubProposals_Status", "[Status] IN (1,2,3,4)");
                    table.CheckConstraint("CK_ClubProposals_Text", "LEN(LTRIM(RTRIM([Name])))>0 AND LEN(LTRIM(RTRIM([Purpose])))>0");
                    table.ForeignKey(
                        name: "FK_ClubProposals_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClubProposals_Users_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Status",
                table: "Users",
                sql: "[Status] IN (1, 2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubProposals_ApplicantId",
                table: "ClubProposals",
                column: "ApplicantId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ClubProposals_ClubId",
                table: "ClubProposals",
                column: "ClubId",
                unique: true,
                filter: "[ClubId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClubProposals_Status_SubmittedAt",
                table: "ClubProposals",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ClubProposals) OR EXISTS (SELECT 1 FROM Users WHERE Status IN (3,4))
                    THROW 51000, 'Cannot downgrade while registration or club proposal data exists.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ClubProposals");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Status",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                table: "Roles");

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LockoutEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUserName",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "PhoneNumberConfirmed",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Roles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO UserRoles (UserId, RoleId) SELECT Id, RoleId FROM Users WHERE RoleId IS NOT NULL;
                UPDATE Users SET UserName = Email, NormalizedUserName = NormalizedEmail, LockoutEnabled = 1;
                UPDATE Roles SET NormalizedName = UPPER(Name), ConcurrencyStamp = CONVERT(nvarchar(36), NEWID());
                """);
            migrationBuilder.DropColumn(name: "RoleId", table: "Users");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "system-admin",
                columns: new[] { "ConcurrencyStamp", "NormalizedName" },
                values: new object[] { "seed-admin-v1", "ADMIN" });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "system-student",
                columns: new[] { "ConcurrencyStamp", "NormalizedName" },
                values: new object[] { "seed-student-v1", "STUDENT" });

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Status",
                table: "Users",
                sql: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");
        }
    }
}
