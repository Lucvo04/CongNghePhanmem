using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Clubs;

namespace StudentClubManagement.IntegrationTests;

public class MembershipTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person manager = null!, student = null!;
    private int clubId, memberId, managerId;
    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        manager = await UserAsync(); student = await UserAsync();
        await using var db = database.CreateContext();
        var club = new Club { Code = "MEM-" + Guid.NewGuid().ToString("N")[..16], Name = "CLB thành viên" };
        db.Clubs.Add(club); await db.SaveChangesAsync(); clubId = club.Id;
        var chief = new Membership { ClubId = clubId, UserId = manager.Id, Role = ClubRole.Manager };
        var member = new Membership { ClubId = clubId, UserId = student.Id };
        db.Memberships.AddRange(chief, member); await db.SaveChangesAsync(); memberId = member.Id; managerId = chief.Id;
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task ManagerCanSuspendRestoreAndEndWithHistoryAndNoAccountChanges()
    {
        using var client = await LoginAsync(manager);
        await using var beforeDb = database.CreateContext(); var before = await beforeDb.Users.AsNoTracking().SingleAsync(u => u.Id == student.Id);
        foreach (var status in new[] { MembershipStatus.Suspended, MembershipStatus.Active, MembershipStatus.Left })
        {
            var page = await client.GetAsync($"/Memberships/ChangeStatus/{memberId}"); Assert.True(page.Headers.CacheControl!.NoStore);
            var fields = Fields(await page.Content.ReadAsStringAsync(), status, "  Lý do kiểm thử <script>alert(1)</script>  ");
            fields["Role"] = "2"; fields["UserId"] = manager.Id; fields["EndedAt"] = "2000-01-01"; fields["SelfLeave"] = "true";
            var response = await client.PostAsync($"/Memberships/ChangeStatus/{memberId}", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var member = await MemberAsync(); Assert.Equal(status, member.Status); Assert.Equal(ClubRole.Member, member.Role); Assert.Equal(student.Id, member.UserId);
            Assert.Equal(status == MembershipStatus.Left, member.EndedAt.HasValue);
        }
        await using var db = database.CreateContext(); var after = await db.Users.AsNoTracking().SingleAsync(u => u.Id == student.Id);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp); Assert.Equal(before.Status, after.Status); Assert.Equal(before.PasswordHash, after.PasswordHash);
        var logs = await db.AuditLogs.Where(a => a.EntityType == "Membership" && a.EntityId == memberId.ToString()).ToListAsync();
        Assert.Equal(3, logs.Count); Assert.All(logs, a => { Assert.Equal(manager.Id, a.ActorId); Assert.Equal("Lý do kiểm thử <script>alert(1)</script>", a.Reason); Assert.Contains("Status", a.BeforeData); Assert.Contains("Role", a.AfterData); });
        using var owner = await LoginAsync(student); var history = await owner.GetStringAsync($"/Memberships/Details/{memberId}");
        Assert.Contains("3 lần thay đổi", WebUtility.HtmlDecode(history)); Assert.DoesNotContain("<script>alert(1)</script>", history);
        Assert.Equal("Conflict", (await ChangeAsync(manager.Id, MembershipStatus.Active)).Code);
    }

    [Fact]
    public async Task StudentCanLeaveOnlyOwnMembershipAndCannotForgeRestoreOrManagerPrivileges()
    {
        using var owner = await LoginAsync(student);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Memberships?clubId={clubId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Memberships/ChangeStatus/{memberId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Memberships/Details/{managerId}")).StatusCode);
        var mine = await owner.GetAsync("/Memberships/Mine"); Assert.True(mine.Headers.CacheControl!.NoStore);
        Assert.Contains($"/Memberships/Details/{memberId}", await mine.Content.ReadAsStringAsync());
        var fields = Fields(await owner.GetStringAsync($"/Memberships/Leave/{memberId}"), MembershipStatus.Active, "Thay đổi kế hoạch");
        fields["SelfLeave"] = "false";
        var response = await owner.PostAsync($"/Memberships/Leave/{memberId}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Equal(MembershipStatus.Left, (await MemberAsync()).Status);
        var denied = await owner.PostAsync($"/Memberships/Leave/{managerId}", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal("Forbidden", (await ChangeAsync(student.Id, MembershipStatus.Active, self: true)).Code);
        var history = await owner.GetStringAsync("/Memberships/Mine?status=all"); Assert.Contains($"/Memberships/Details/{memberId}", history);
        Assert.DoesNotContain($"/Memberships/Details/{memberId}", await owner.GetStringAsync("/Memberships/Mine"));
        await using var scope = app.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<JoinRequestService>().SubmitAsync(clubId, student.Id, "Xin gia nhập lại")).Succeeded);
    }

    [Theory]
    [InlineData(MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Left)]
    public async Task CurrentManagerMustBeTransferredFirstEvenForAdminOrSelf(MembershipStatus status)
    {
        var admin = await UserAsync(SystemRoles.Admin);
        Assert.Equal("Conflict", (await ChangeAsync(admin.Id, status, id: managerId)).Code);
        Assert.Equal("Conflict", (await ChangeAsync(manager.Id, MembershipStatus.Left, self: true, id: managerId)).Code);
        await using var db = database.CreateContext(); var chief = await db.Memberships.FindAsync(managerId);
        Assert.Equal(MembershipStatus.Active, chief!.Status); Assert.Null(chief.EndedAt);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityType == "Membership" && a.EntityId == managerId.ToString()));
    }

    [Fact]
    public async Task AdminCanManageButGuestAndOtherClubManagerCannot()
    {
        using var guest = app.BrowserClient(); Assert.Contains("/Account/Login", (await guest.GetAsync($"/Memberships/Details/{memberId}")).Headers.Location!.OriginalString);
        var outsider = await UserAsync();
        await using (var db = database.CreateContext())
        {
            var other = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "CLB khác" }; db.Clubs.Add(other); await db.SaveChangesAsync();
            db.Memberships.Add(new Membership { ClubId = other.Id, UserId = outsider.Id, Role = ClubRole.Manager }); await db.SaveChangesAsync();
        }
        using var client = await LoginAsync(outsider);
        foreach (var path in new[] { $"/Memberships?clubId={clubId}", $"/Memberships/Details/{memberId}", $"/Memberships/ChangeStatus/{memberId}", $"/Memberships/Leave/{memberId}" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        var token = Token(await client.GetStringAsync("/Account/Overview"));
        var denied = await client.PostAsync($"/Memberships/ChangeStatus/{memberId}", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["__RequestVerificationToken"] = token, ["RowVersion"] = Convert.ToBase64String((await MemberAsync()).RowVersion), ["NewStatus"] = "2", ["Reason"] = "Invalid scope" }));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); Assert.Equal("NotFound", (await ChangeAsync(outsider.Id, MembershipStatus.Suspended)).Code);
        var admin = await UserAsync(SystemRoles.Admin); Assert.True((await ChangeAsync(admin.Id, MembershipStatus.Suspended)).Succeeded);
    }

    [Theory]
    [InlineData("Transfer")]
    [InlineData("Disabled")]
    [InlineData("RoleRemoved")]
    public async Task RevokedManagerCannotUseOldForm(string change)
    {
        using var client = await LoginAsync(manager); var fields = Fields(await client.GetStringAsync($"/Memberships/ChangeStatus/{memberId}"), MembershipStatus.Suspended);
        await using (var db = database.CreateContext())
        {
            if (change == "Transfer") (await db.Memberships.FindAsync(managerId))!.Role = ClubRole.Member;
            if (change == "Disabled") (await db.Users.FindAsync(manager.Id))!.Status = UserStatus.Disabled;
            if (change == "RoleRemoved") (await db.Users.FindAsync(manager.Id))!.RoleId = null;
            await db.SaveChangesAsync();
        }
        Assert.Equal("NotFound", (await ChangeAsync(manager.Id, MembershipStatus.Suspended)).Code);
        var response = await client.PostAsync($"/Memberships/ChangeStatus/{memberId}", new FormUrlEncodedContent(fields));
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Redirect);
        Assert.Equal(MembershipStatus.Active, (await MemberAsync()).Status);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("NoStudent")]
    [InlineData("Admin")]
    [InlineData("ClubSuspended")]
    public async Task RestoreRequiresActiveStudentAndActiveClub(string change)
    {
        Assert.True((await ChangeAsync(manager.Id, MembershipStatus.Suspended)).Succeeded);
        await using (var db = database.CreateContext())
        {
            if (change == "Disabled") (await db.Users.FindAsync(student.Id))!.Status = UserStatus.Disabled;
            if (change == "NoStudent") (await db.Users.FindAsync(student.Id))!.RoleId = null;
            if (change == "Admin") (await db.Users.FindAsync(student.Id))!.RoleId = "system-admin";
            if (change == "ClubSuspended") (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Suspended;
            await db.SaveChangesAsync();
        }
        Assert.Equal("Conflict", (await ChangeAsync(manager.Id, MembershipStatus.Active)).Code);
        Assert.Equal(MembershipStatus.Suspended, (await MemberAsync()).Status);
        Assert.True((await ChangeAsync(manager.Id, MembershipStatus.Left)).Succeeded);
    }

    [Fact]
    public async Task ArchivedClubIsReadOnlyForManagerAndOwner()
    {
        await using (var db = database.CreateContext()) { (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Archived; await db.SaveChangesAsync(); }
        Assert.Equal("Conflict", (await ChangeAsync(manager.Id, MembershipStatus.Suspended)).Code);
        Assert.Equal("Conflict", (await ChangeAsync(student.Id, MembershipStatus.Left, self: true)).Code);
        using var owner = await LoginAsync(student); var html = await owner.GetStringAsync($"/Memberships/Details/{memberId}");
        Assert.DoesNotContain($"/Memberships/Leave/{memberId}", html);
    }

    [Fact]
    public async Task InvalidReasonInvalidStatusMissingCsrfAndGetDoNotMutate()
    {
        using var client = await LoginAsync(manager);
        var form = await client.GetStringAsync($"/Memberships/ChangeStatus/{memberId}");
        foreach (var reason in new[] { "", "    ", new string('x', 501) })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/Memberships/ChangeStatus/{memberId}", new FormUrlEncodedContent(Fields(form, MembershipStatus.Suspended, reason)))).StatusCode);
            Assert.Equal("Validation", (await ChangeAsync(manager.Id, MembershipStatus.Suspended, reason: reason)).Code);
        }
        Assert.Equal("Validation", (await ChangeAsync(manager.Id, (MembershipStatus)99)).Code);
        var fields = Fields(form, MembershipStatus.Suspended); fields.Remove("__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/Memberships/ChangeStatus/{memberId}", new FormUrlEncodedContent(fields))).StatusCode);
        using var owner = await LoginAsync(student);
        await owner.GetStringAsync($"/Memberships/Leave/{memberId}");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/Memberships/Leave/{memberId}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(MembershipStatus.Active, (await MemberAsync()).Status);
    }

    [Fact]
    public async Task StaleFormDuplicateAndConcurrentChangesHaveOneEffectiveChange()
    {
        using var client = await LoginAsync(manager); var fields = Fields(await client.GetStringAsync($"/Memberships/ChangeStatus/{memberId}"), MembershipStatus.Left);
        var stamp = fields["RowVersion"];
        var results = await Task.WhenAll(ChangeAsync(manager.Id, MembershipStatus.Suspended, stamp: stamp), ChangeAsync(student.Id, MembershipStatus.Left, stamp: stamp, self: true));
        Assert.Single(results, r => r.Succeeded);
        var post = await client.PostAsync($"/Memberships/ChangeStatus/{memberId}", new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);
        await using var db = database.CreateContext(); Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityType == "Membership" && a.EntityId == memberId.ToString()));
        Assert.Equal("Conflict", (await ChangeAsync(manager.Id, (await MemberAsync()).Status)).Code);
    }

    [Fact]
    public async Task AuditFailureRollsBackStatusAndEndDate()
    {
        await using var db = database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailMemberAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityType='Membership') THROW 51000, 'Test member audit failure',1; END");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => ChangeAsync(manager.Id, MembershipStatus.Left));
            var member = await MemberAsync(); Assert.Equal(MembershipStatus.Active, member.Status); Assert.Null(member.EndedAt);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailMemberAudit"); }
    }

    [Fact]
    public async Task SearchFilterPaginationAndHistoryAreScoped()
    {
        await using (var db = database.CreateContext())
        {
            (await db.Users.FindAsync(student.Id))!.FullName = "Tên riêng tìm kiếm";
            for (var i=0;i<11;i++) db.Memberships.Add(new Membership { ClubId = clubId, UserId = student.Id,
                Status = MembershipStatus.Left, JoinedAt = DateTimeOffset.UtcNow.AddDays(-3), EndedAt = DateTimeOffset.UtcNow.AddDays(-2) });
            await db.SaveChangesAsync();
        }
        using var client = await LoginAsync(manager);
        var search = WebUtility.HtmlDecode(await client.GetStringAsync($"/Memberships?clubId={clubId}&search={Uri.EscapeDataString("  Tên riêng  ")}"));
        Assert.Contains("1 đợt tham gia", search);
        var code = WebUtility.HtmlDecode(await client.GetStringAsync($"/Memberships?clubId={clubId}&search={student.Code}")); Assert.Contains("1 đợt tham gia", code);
        var all = WebUtility.HtmlDecode(await client.GetStringAsync($"/Memberships?clubId={clubId}&status=all&page=999")); Assert.Contains("13 đợt tham gia", all); Assert.Contains("Trang 2 / 2", all); Assert.Contains("status=all", all);
        foreach (var suffix in new[] { "status=bad", "status=99", "page=bad", "search="+new string('x',151) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/Memberships?clubId={clubId}&{suffix}")).StatusCode);
        for (var i=0;i<11;i++) Assert.True((await ChangeAsync(manager.Id, i%2==0 ? MembershipStatus.Suspended : MembershipStatus.Active)).Succeeded);
        var history = WebUtility.HtmlDecode(await client.GetStringAsync($"/Memberships/Details/{memberId}?page=999")); Assert.Contains("11 lần thay đổi", history); Assert.Contains("Trang 2 / 2", history);
    }

    [Fact]
    public async Task TransferAndSuspensionCannotLeaveNewManagerSuspended()
    {
        var admin = await UserAsync(SystemRoles.Admin);
        await using var db = database.CreateContext(); var club = await db.Clubs.AsNoTracking().SingleAsync(c => c.Id == clubId);
        async Task<ClubResult> Transfer()
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ClubManagementService>().TransferAsync(clubId,
                new TransferManagerViewModel { NewManagerUserId = student.Id, Reason = "Chuyển nhiệm kỳ", RowVersion = Convert.ToBase64String(club.RowVersion) }, admin.Id);
        }
        var transferTask = Transfer(); var suspendTask = ChangeAsync(admin.Id, MembershipStatus.Suspended);
        await Task.WhenAll(transferTask, suspendTask);
        Assert.NotEqual((await transferTask).Succeeded, (await suspendTask).Succeeded);
        var chief = await db.Memberships.AsNoTracking().SingleAsync(m => m.ClubId == clubId && m.Role == ClubRole.Manager && m.EndedAt == null);
        Assert.Equal(MembershipStatus.Active, chief.Status);
    }

    [Fact]
    public async Task SuspensionDoesNotRemoveMembershipInAnotherClubOrPendingRequestProtection()
    {
        await using var db = database.CreateContext(); var club = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "CLB thứ hai" };
        db.Clubs.Add(club); await db.SaveChangesAsync(); var other = new Membership { ClubId = club.Id, UserId = student.Id };
        db.Memberships.Add(other); await db.SaveChangesAsync();
        Assert.True((await ChangeAsync(manager.Id, MembershipStatus.Suspended)).Succeeded);
        await db.Entry(other).ReloadAsync(); Assert.Equal(MembershipStatus.Active, other.Status);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal("Conflict", (await scope.ServiceProvider.GetRequiredService<JoinRequestService>().SubmitAsync(clubId, student.Id, "Không được nộp trùng khi đình chỉ")).Code);
    }

    private async Task<MembershipResult> ChangeAsync(string actor, MembershipStatus status, string reason = "Lý do thử nghiệm", string? stamp = null, bool self = false, int? id = null)
    {
        var target = id ?? memberId; await using var db = database.CreateContext();
        stamp ??= Convert.ToBase64String((await db.Memberships.AsNoTracking().SingleAsync(m => m.Id == target)).RowVersion);
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MembershipService>().ChangeStatusAsync(target, actor, stamp, status, reason, self);
    }
    private async Task<Membership> MemberAsync() { await using var db = database.CreateContext(); return await db.Memberships.AsNoTracking().SingleAsync(m => m.Id == memberId); }
    private async Task<Person> UserAsync(string role = SystemRoles.Student)
    {
        await using var scope = app.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tag = Guid.NewGuid().ToString("N"); var email = $"member-{tag}@example.test"; var password = $"Test-Only!9-{tag}";
        var user = new ApplicationUser { Email = email, UserName = email, FullName = "Sinh viên " + tag[..6], StudentCode = tag[..24] };
        Assert.True((await users.CreateAsync(user, password)).Succeeded); Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return new(user.Id, email, password, user.StudentCode);
    }
    private async Task<HttpClient> LoginAsync(Person person)
    {
        var client = app.BrowserClient(); var token = Token(await client.GetStringAsync("/Account/Login"));
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Email"] = person.Email, ["Password"] = person.Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); return client;
    }
    private static Dictionary<string,string> Fields(string html, MembershipStatus status, string reason = "Lý do kiểm thử") => new()
    { ["__RequestVerificationToken"] = Token(html), ["RowVersion"] = Extract(html,"RowVersion"), ["NewStatus"] = ((int)status).ToString(), ["Reason"] = reason };
    private static string Token(string html) => Extract(html,"__RequestVerificationToken");
    private static string Extract(string html,string name) { var value=Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(value); return WebUtility.HtmlDecode(value); }
    private sealed record Person(string Id,string Email,string Password,string Code);
}
