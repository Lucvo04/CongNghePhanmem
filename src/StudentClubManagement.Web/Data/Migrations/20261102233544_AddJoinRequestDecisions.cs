using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentClubManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJoinRequestDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_JoinRequests_Period",
                table: "JoinRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JoinRequests_Status",
                table: "JoinRequests");

            migrationBuilder.AddColumn<string>(
                name: "DecidedById",
                table: "JoinRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "JoinRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MembershipId",
                table: "JoinRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_DecidedById",
                table: "JoinRequests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_MembershipId",
                table: "JoinRequests",
                column: "MembershipId",
                unique: true,
                filter: "[MembershipId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JoinRequests_Decision",
                table: "JoinRequests",
                sql: "([Status] IN (1,2) AND [DecidedById] IS NULL AND [DecisionReason] IS NULL AND [MembershipId] IS NULL) OR ([Status] = 3 AND [DecidedById] IS NOT NULL AND [DecisionReason] IS NULL AND [MembershipId] IS NOT NULL) OR ([Status] = 4 AND [DecidedById] IS NOT NULL AND [MembershipId] IS NULL AND [DecisionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([DecisionReason]))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JoinRequests_Period",
                table: "JoinRequests",
                sql: "([Status] = 1 AND [ClosedAt] IS NULL) OR ([Status] IN (2,3,4) AND [ClosedAt] >= [SubmittedAt] AND [ClosedAt] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JoinRequests_Status",
                table: "JoinRequests",
                sql: "[Status] IN (1, 2, 3, 4)");

            migrationBuilder.AddForeignKey(
                name: "FK_JoinRequests_Memberships_MembershipId",
                table: "JoinRequests",
                column: "MembershipId",
                principalTable: "Memberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JoinRequests_Users_DecidedById",
                table: "JoinRequests",
                column: "DecidedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không làm mất người quyết định/lý do/liên kết thành viên khi quay về PB07.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [JoinRequests] WITH (TABLOCKX, HOLDLOCK) WHERE [Status] IN (3,4)) THROW 51001, 'Cannot roll back reviewed join requests. Keep decision history.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_JoinRequests_Memberships_MembershipId",
                table: "JoinRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_JoinRequests_Users_DecidedById",
                table: "JoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_JoinRequests_DecidedById",
                table: "JoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_JoinRequests_MembershipId",
                table: "JoinRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JoinRequests_Decision",
                table: "JoinRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JoinRequests_Period",
                table: "JoinRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JoinRequests_Status",
                table: "JoinRequests");

            migrationBuilder.DropColumn(
                name: "DecidedById",
                table: "JoinRequests");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "JoinRequests");

            migrationBuilder.DropColumn(
                name: "MembershipId",
                table: "JoinRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JoinRequests_Period",
                table: "JoinRequests",
                sql: "([Status] = 1 AND [ClosedAt] IS NULL) OR ([Status] = 2 AND [ClosedAt] >= [SubmittedAt] AND [ClosedAt] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JoinRequests_Status",
                table: "JoinRequests",
                sql: "[Status] IN (1, 2)");
        }
    }
}
