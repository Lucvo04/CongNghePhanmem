using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;

namespace StudentClubManagement.IntegrationTests;

public class JoinRequestReviewTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person manager = null!, student = null!;
    private int clubId, requestId;
    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        manager = await UserAsync(); student = await UserAsync();
        await using var db = database.CreateContext();
        var club = new Club { Code = "REVIEW-" + Guid.NewGuid().ToString("N")[..16], Name = "CLB xét duyệt" };
        db.Clubs.Add(club); await db.SaveChangesAsync(); clubId = club.Id;
        db.Memberships.Add(new Membership { ClubId = clubId, UserId = manager.Id, Role = ClubRole.Manager });
        var request = new JoinRequest { ClubId = clubId, UserId = student.Id, Reason = "Mong muốn cùng học hỏi <script>alert(1)</script>" };
        db.JoinRequests.Add(request); await db.SaveChangesAsync(); requestId = request.Id;
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagerOrAdminApprovesAtomicallyAndStudentSeesResult(bool admin)
    {
        var actor = admin ? await UserAsync(SystemRoles.Admin) : manager;
        using var client = await LoginAsync(actor);
        var page = await client.GetAsync($"/JoinRequestReview/Decide/{requestId}");
        Assert.True(page.Headers.CacheControl!.NoStore);
        var html = await page.Content.ReadAsStringAsync(); Assert.DoesNotContain("<script>alert(1)</script>", html);
        var fields = Fields(html, "3", "Must not be retained"); fields["UserId"] = actor.Id; fields["MembershipId"] = "9999";
        var response = await client.PostAsync($"/JoinRequestReview/Decide/{requestId}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = database.CreateContext(); var request = await db.JoinRequests.FindAsync(requestId);
        Assert.Equal(JoinRequestStatus.Approved, request!.Status); Assert.Equal(actor.Id, request.DecidedById);
        Assert.NotNull(request.ClosedAt); Assert.Null(request.DecisionReason);
        var member = await db.Memberships.SingleAsync(m => m.UserId == student.Id && m.ClubId == clubId);
        Assert.Equal(member.Id, request.MembershipId); Assert.Equal(ClubRole.Member, member.Role); Assert.Equal(MembershipStatus.Active, member.Status);
        Assert.Null(member.EndedAt); Assert.Equal(request.ClosedAt, member.JoinedAt);
        Assert.Single(await db.AuditLogs.Where(a => a.EntityId == requestId.ToString() && a.Action == "JoinRequestApproved").ToListAsync());
        using var owner = await LoginAsync(student);
        var history = WebUtility.HtmlDecode(await owner.GetStringAsync("/JoinRequests"));
        Assert.Contains("Đã chấp nhận", history); Assert.Contains("Người xử lý:", history);
        Assert.DoesNotContain($"/JoinRequests/Withdraw/{requestId}", history);
    }

    [Fact]
    public async Task RejectRequiresReasonKeepsNoMemberAndAllowsNewApplication()
    {
        using var client = await LoginAsync(manager);
        var form = await client.GetStringAsync($"/JoinRequestReview/Decide/{requestId}");
        foreach (var reason in new[] { "", "   ", new string('x', 501) })
        {
            var response = await client.PostAsync($"/JoinRequestReview/Decide/{requestId}", new FormUrlEncodedContent(Fields(form, "4", reason)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(JoinRequestStatus.Pending, (await RequestAsync()).Status);
        }
        const string rejection = "Cần bổ sung mục tiêu <script>alert(2)</script>";
        var done = await client.PostAsync($"/JoinRequestReview/Decide/{requestId}", new FormUrlEncodedContent(Fields(form, "4", "  " + rejection + "  ")));
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        var request = await RequestAsync(); Assert.Equal(rejection, request.DecisionReason); Assert.Null(request.MembershipId);
        await using var db = database.CreateContext(); Assert.False(await db.Memberships.AnyAsync(m => m.UserId == student.Id && m.ClubId == clubId));
        using var owner = await LoginAsync(student); var history = await owner.GetStringAsync("/JoinRequests");
        Assert.Contains("Đã từ chối", WebUtility.HtmlDecode(history)); Assert.DoesNotContain("<script>alert(2)</script>", history);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<JoinRequestService>().SubmitAsync(clubId, student.Id, "Bổ sung mục tiêu")).Succeeded);
    }

    [Fact]
    public async Task GuestUnrelatedStudentAndOtherClubManagerCannotReviewOrLeakRequests()
    {
        using var guest = app.BrowserClient();
        Assert.Contains("/Account/Login", (await guest.GetAsync($"/JoinRequestReview/Decide/{requestId}")).Headers.Location!.OriginalString);
        var outsider = await UserAsync();
        await using (var db = database.CreateContext())
        {
            var other = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "Other club" };
            db.Clubs.Add(other); await db.SaveChangesAsync();
            db.Memberships.Add(new Membership { ClubId = other.Id, UserId = outsider.Id, Role = ClubRole.Manager }); await db.SaveChangesAsync();
        }
        foreach (var person in new[] { student, outsider })
        {
            using var client = await LoginAsync(person);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/JoinRequestReview?clubId={clubId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/JoinRequestReview/Decide/{requestId}")).StatusCode);
            var token = Token(await client.GetStringAsync("/Account/Overview"));
            var response = await client.PostAsync($"/JoinRequestReview/Decide/{requestId}", new FormUrlEncodedContent(new Dictionary<string,string>
                { ["__RequestVerificationToken"] = token, ["Decision"] = "3", ["RowVersion"] = Convert.ToBase64String((await RequestAsync()).RowVersion) }));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("NotFound", (await DecideAsync(person.Id)).Code);
        }
    }

    [Fact]
    public async Task GetAndMissingCsrfCannotMutate()
    {
        using var client = await LoginAsync(manager);
        await client.GetStringAsync($"/JoinRequestReview/Decide/{requestId}");
        Assert.Equal(JoinRequestStatus.Pending, (await RequestAsync()).Status);
        var response = await client.PostAsync($"/JoinRequestReview/Decide/{requestId}", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Decision"] = "3", ["RowVersion"] = Convert.ToBase64String((await RequestAsync()).RowVersion) }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(JoinRequestStatus.Pending, (await RequestAsync()).Status);
    }

    [Theory]
    [InlineData("Transfer")]
    [InlineData("Disabled")]
    [InlineData("RoleRemoved")]
    public async Task OldFormLosesAuthorityImmediately(string change)
    {
        using var client = await LoginAsync(manager); var fields = Fields(await client.GetStringAsync($"/JoinRequestReview/Decide/{requestId}"), "3");
        await using (var db = database.CreateContext())
        {
            if (change == "Transfer") (await db.Memberships.SingleAsync(m => m.UserId == manager.Id && m.ClubId == clubId)).Role = ClubRole.Member;
            if (change == "Disabled") (await db.Users.FindAsync(manager.Id))!.Status = UserStatus.Disabled;
            if (change == "RoleRemoved") (await db.Users.FindAsync(manager.Id))!.RoleId = null;
            await db.SaveChangesAsync();
        }
        Assert.Equal("NotFound", (await DecideAsync(manager.Id)).Code);
        var response = await client.PostAsync($"/JoinRequestReview/Decide/{requestId}", new FormUrlEncodedContent(fields));
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Redirect);
        Assert.Equal(JoinRequestStatus.Pending, (await RequestAsync()).Status);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("Admin")]
    [InlineData("NoStudent")]
    [InlineData("ActiveMember")]
    [InlineData("SuspendedMember")]
    public async Task ApplicantEligibilityIsCheckedAgainBeforeApproval(string change)
    {
        await using (var db = database.CreateContext())
        {
            if (change == "Disabled") (await db.Users.FindAsync(student.Id))!.Status = UserStatus.Disabled;
            if (change == "Admin") (await db.Users.FindAsync(student.Id))!.RoleId = "system-admin";
            if (change == "NoStudent") (await db.Users.FindAsync(student.Id))!.RoleId = null;
            if (change.EndsWith("Member")) db.Memberships.Add(new Membership { ClubId = clubId, UserId = student.Id,
                Status = change == "ActiveMember" ? MembershipStatus.Active : MembershipStatus.Suspended });
            await db.SaveChangesAsync();
        }
        Assert.Equal("Conflict", (await DecideAsync(manager.Id)).Code);
        Assert.Equal(JoinRequestStatus.Pending, (await RequestAsync()).Status);
        Assert.True((await DecideAsync(manager.Id, JoinRequestStatus.Rejected, "Không còn đủ điều kiện")).Succeeded);
    }

    [Theory]
    [InlineData(ClubStatus.Suspended, "Success")]
    [InlineData(ClubStatus.Archived, "Conflict")]
    public async Task InactiveClubCannotApproveAndArchivedClubCannotReject(ClubStatus status, string rejectCode)
    {
        await using (var db = database.CreateContext()) { (await db.Clubs.FindAsync(clubId))!.Status = status; await db.SaveChangesAsync(); }
        Assert.Equal("Conflict", (await DecideAsync(manager.Id)).Code);
        Assert.Equal(rejectCode, (await DecideAsync(manager.Id, JoinRequestStatus.Rejected, "CLB tạm dừng")).Code);
    }

    [Fact]
    public async Task StaleStampRepeatedDecisionAndInvalidDecisionDoNotMutate()
    {
        var stamp = Convert.ToBase64String((await RequestAsync()).RowVersion);
        await using (var db = database.CreateContext()) { (await db.JoinRequests.FindAsync(requestId))!.Reason = "Updated reason"; await db.SaveChangesAsync(); }
        Assert.Equal("Conflict", (await DecideAsync(manager.Id, stamp: stamp)).Code);
        Assert.Equal("Validation", (await DecideAsync(manager.Id, JoinRequestStatus.Withdrawn)).Code);
        Assert.True((await DecideAsync(manager.Id)).Succeeded);
        Assert.Equal("Conflict", (await DecideAsync(manager.Id)).Code);
    }

    [Fact]
    public async Task TwoApprovalsOrApprovalVersusWithdrawalHaveExactlyOneWinner()
    {
        var stamp = Convert.ToBase64String((await RequestAsync()).RowVersion);
        var first = await Task.WhenAll(DecideAsync(manager.Id, stamp: stamp), DecideAsync(manager.Id, stamp: stamp));
        Assert.Single(first, r => r.Succeeded);
        await using (var db = database.CreateContext())
        {
            Assert.Equal(1, await db.Memberships.CountAsync(m => m.ClubId == clubId && m.UserId == student.Id));
            var nextUser = await UserAsync(); student = nextUser;
            var next = new JoinRequest { ClubId = clubId, UserId = student.Id, Reason = "Race withdrawal" };
            db.JoinRequests.Add(next); await db.SaveChangesAsync(); requestId = next.Id;
        }
        stamp = Convert.ToBase64String((await RequestAsync()).RowVersion);
        async Task<JoinRequestResult> Withdraw() { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<JoinRequestService>().WithdrawAsync(requestId, student.Id, stamp); }
        var race = await Task.WhenAll(DecideAsync(manager.Id, stamp: stamp), Withdraw());
        Assert.Single(race, r => r.Succeeded);
        var final = await RequestAsync();
        await using var after = database.CreateContext();
        Assert.Equal(final.Status == JoinRequestStatus.Approved ? 1 : 0, await after.Memberships.CountAsync(m => m.ClubId == clubId && m.UserId == student.Id));
    }

    [Fact]
    public async Task AuditFailureRollsBackDecisionAndMembership()
    {
        await using var db = database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailReviewAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action IN ('JoinRequestApproved','JoinRequestRejected')) THROW 51000, 'Test audit failure', 1; END");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => DecideAsync(manager.Id));
            Assert.Equal(JoinRequestStatus.Pending, (await RequestAsync()).Status);
            Assert.False(await db.Memberships.AnyAsync(m => m.ClubId == clubId && m.UserId == student.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => DecideAsync(manager.Id, JoinRequestStatus.Rejected, "Test rejection"));
            Assert.Equal(JoinRequestStatus.Pending, (await RequestAsync()).Status);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailReviewAudit"); }
    }

    [Fact]
    public async Task SelfReviewDeniedAndLeftMemberGetsNewMembership()
    {
        await using (var db = database.CreateContext())
        {
            (await db.JoinRequests.FindAsync(requestId))!.UserId = manager.Id; await db.SaveChangesAsync();
        }
        Assert.Equal("Forbidden", (await DecideAsync(manager.Id)).Code);
        await using (var db = database.CreateContext())
        {
            (await db.JoinRequests.FindAsync(requestId))!.UserId = student.Id;
            db.Memberships.Add(new Membership { UserId = student.Id, ClubId = clubId, Status = MembershipStatus.Left,
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-2), EndedAt = DateTimeOffset.UtcNow.AddDays(-1) }); await db.SaveChangesAsync();
        }
        Assert.True((await DecideAsync(manager.Id)).Succeeded);
        await using var after = database.CreateContext();
        Assert.Equal(2, await after.Memberships.CountAsync(m => m.UserId == student.Id && m.ClubId == clubId));
    }

    [Fact]
    public async Task FiltersPaginationAndSqlDecisionConstraintsAreEnforced()
    {
        await using (var db = database.CreateContext())
        {
            for (var i = 0; i < 11; i++) db.JoinRequests.Add(new JoinRequest { UserId = student.Id, ClubId = clubId, Reason = "History",
                Status = JoinRequestStatus.Withdrawn, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-2), ClosedAt = DateTimeOffset.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
            var row = await db.JoinRequests.FindAsync(requestId); row!.Status = JoinRequestStatus.Rejected; row.ClosedAt = DateTimeOffset.UtcNow;
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        using var client = await LoginAsync(manager);
        var pending = WebUtility.HtmlDecode(await client.GetStringAsync($"/JoinRequestReview?clubId={clubId}")); Assert.Contains("1 đơn", pending);
        var all = WebUtility.HtmlDecode(await client.GetStringAsync($"/JoinRequestReview?clubId={clubId}&status=all&page=999"));
        Assert.Contains("12 đơn", all); Assert.Contains("Trang 2 / 2", all); Assert.Contains("status=all", all);
        foreach (var suffix in new[] { "status=99", "status=bad", "page=invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/JoinRequestReview?clubId={clubId}&{suffix}")).StatusCode);
    }

    [Fact]
    public async Task OldHttpFormCannotApproveWithdrawnRequest()
    {
        using var client = await LoginAsync(manager);
        var fields = Fields(await client.GetStringAsync($"/JoinRequestReview/Decide/{requestId}"), "3");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<JoinRequestService>()
                .WithdrawAsync(requestId, student.Id, fields["RowVersion"])).Succeeded);
        }
        var response = await client.PostAsync($"/JoinRequestReview/Decide/{requestId}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Đã rút", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        await using var db = database.CreateContext();
        Assert.False(await db.Memberships.AnyAsync(m => m.ClubId == clubId && m.UserId == student.Id));
    }

    [Fact]
    public async Task RollbackMigrationCannotDiscardReviewedHistory()
    {
        Assert.True((await DecideAsync(manager.Id)).Succeeded);
        await using var db = database.CreateContext();
        var error = await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync("20261102122134_AddJoinRequests"));
        // EF commits each migration separately; restore newer migrations after the guarded downgrade.
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Contains("Cannot roll back reviewed join requests", error.Message);
        var request = await RequestAsync(); Assert.Equal(JoinRequestStatus.Approved, request.Status);
        Assert.Equal(manager.Id, request.DecidedById); Assert.NotNull(await db.Memberships.FindAsync(request.MembershipId));
    }

    private async Task<JoinRequestResult> DecideAsync(string actor, JoinRequestStatus decision = JoinRequestStatus.Approved, string? reason = null, string? stamp = null)
    {
        stamp ??= Convert.ToBase64String((await RequestAsync()).RowVersion);
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<JoinRequestReviewService>().DecideAsync(requestId, actor, stamp, decision, reason);
    }
    private async Task<JoinRequest> RequestAsync() { await using var db = database.CreateContext(); return await db.JoinRequests.AsNoTracking().SingleAsync(r => r.Id == requestId); }
    private async Task<Person> UserAsync(string role = SystemRoles.Student)
    {
        await using var scope = app.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tag = Guid.NewGuid().ToString("N"); var email = $"review-{tag}@example.test"; var password = $"Test-Only!9-{tag}";
        var user = new ApplicationUser { Email = email, UserName = email, FullName = "Sinh viên kiểm thử " + tag[..6], StudentCode = tag[..24] };
        Assert.True((await users.CreateAsync(user, password)).Succeeded); Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return new(user.Id, email, password);
    }
    private async Task<HttpClient> LoginAsync(Person person)
    {
        var client = app.BrowserClient(); var token = Token(await client.GetStringAsync("/Account/Login"));
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Email"] = person.Email, ["Password"] = person.Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); return client;
    }
    private static Dictionary<string,string> Fields(string html, string decision, string reason = "") => new()
    {
        ["__RequestVerificationToken"] = Token(html), ["RowVersion"] = Extract(html, "RowVersion"), ["Decision"] = decision, ["DecisionReason"] = reason
    };
    private static string Token(string html) => Extract(html, "__RequestVerificationToken");
    private static string Extract(string html, string name) { var value = Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(value); return WebUtility.HtmlDecode(value); }
    private sealed record Person(string Id, string Email, string Password);
}
