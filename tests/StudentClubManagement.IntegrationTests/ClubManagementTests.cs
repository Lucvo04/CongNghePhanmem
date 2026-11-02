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

public class ClubManagementTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    public Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        using var scope = app.Services.CreateScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        return Task.CompletedTask;
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task AnonymousAndStudentsCannotAdministerClubs()
    {
        using var anonymous = app.BrowserClient();
        foreach (var path in new[] { "/ClubManagement", "/ClubManagement/Edit/1", "/Admin/Clubs/Create", "/Admin/Clubs/Transfer/1" })
            Assert.Contains("/Account/Login", (await anonymous.GetAsync(path)).Headers.Location!.OriginalString);
        using var student = await ClientAsync(await UserAsync());
        foreach (var path in new[] { "/Admin/Clubs/Create", "/Admin/Clubs/Transfer/1" })
        {
            Assert.Contains("/Account/AccessDenied", (await student.GetAsync(path)).Headers.Location!.OriginalString);
            Assert.Contains("/Account/AccessDenied", (await student.PostAsync(path, new FormUrlEncodedContent([]))).Headers.Location!.OriginalString);
        }
    }

    [Fact]
    public async Task CreatePersistsExactlyOneActiveManagerAndAuditIgnoringOverpostedFields()
    {
        var admin = await UserAsync(SystemRoles.Admin); var manager = await UserAsync();
        using var client = await ClientAsync(admin);
        var form = NewClub(manager); form["Code"] = " " + form["Code"].ToLowerInvariant() + " ";
        form["Status"] = "3"; form["ManagerUserId"] = admin.Id; form["Id"] = "999999";
        var response = await PostAsync(client, "/Admin/Clubs/Create", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = database.CreateContext();
        var club = await db.Clubs.Include(c => c.Memberships).SingleAsync(c => c.Code == form["Code"].Trim());
        Assert.Equal(form["Code"].Trim().ToUpperInvariant(), club.Code);
        Assert.Equal(ClubStatus.Active, club.Status);
        var member = Assert.Single(club.Memberships);
        Assert.Equal(manager.Id, member.UserId); Assert.Equal(ClubRole.Manager, member.Role);
        Assert.Equal(MembershipStatus.Active, member.Status); Assert.Null(member.EndedAt);
        var audit = await db.AuditLogs.SingleAsync(log => log.EntityType == nameof(Club) && log.EntityId == club.Id.ToString());
        Assert.Equal("ClubCreated", audit.Action); Assert.Equal(admin.Id, audit.ActorId);
        using var managerClient = await ClientAsync(manager);
        Assert.Equal(HttpStatusCode.OK, (await managerClient.GetAsync($"/ClubManagement/Edit/{club.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("Code", "bad code", "Mã CLB dùng")]
    [InlineData("Name", "   ", "Vui lòng nhập tên")]
    [InlineData("ManagerStudentCode", "UNKNOWN", "Không tìm thấy sinh viên")]
    public async Task CreateValidatesWithoutLeavingPartialClub(string field, string value, string message)
    {
        using var admin = await ClientAsync(await UserAsync(SystemRoles.Admin));
        var form = NewClub(await UserAsync()); form[field] = value;
        var response = await PostAsync(admin, "/Admin/Clubs/Create", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, await HtmlAsync(response));
        await using var db = database.CreateContext();
        Assert.False(await db.Clubs.AnyAsync(club => club.Code == form["Code"]));
    }

    [Theory]
    [InlineData(SystemRoles.Student, UserStatus.Disabled)]
    [InlineData(SystemRoles.Admin, UserStatus.Active)]
    public async Task CannotAppointDisabledOrAdminAccount(string role, UserStatus status)
    {
        var candidate = await UserAsync(role);
        await using (var db = database.CreateContext())
        {
            (await db.Users.FindAsync(candidate.Id))!.Status = status; await db.SaveChangesAsync();
        }
        using var admin = await ClientAsync(await UserAsync(SystemRoles.Admin));
        var response = await PostAsync(admin, "/Admin/Clubs/Create", NewClub(candidate));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("Không tìm thấy sinh viên", await HtmlAsync(response));
    }

    [Fact]
    public async Task CodeIsUniqueIgnoringCaseAndConcurrentRequestsCannotDuplicateIt()
    {
        using var admin1 = await ClientAsync(await UserAsync(SystemRoles.Admin));
        using var admin2 = await ClientAsync(await UserAsync(SystemRoles.Admin));
        var first = NewClub(await UserAsync()); var second = NewClub(await UserAsync()); second["Code"] = first["Code"].ToLowerInvariant();
        var responses = await Task.WhenAll(PostAsync(admin1, "/Admin/Clubs/Create", first), PostAsync(admin2, "/Admin/Clubs/Create", second));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Redirect);
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Conflict }));
        await using var db = database.CreateContext();
        var club = Assert.Single(await db.Clubs.Where(club => club.Code == first["Code"]).ToListAsync());
        Assert.Equal(1, await db.Memberships.CountAsync(member => member.ClubId == club.Id));
        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.EntityType == nameof(Club) && log.EntityId == club.Id.ToString()));
    }

    [Fact]
    public async Task ClubManagerCannotReadOrEditAnotherClubOrChangeProtectedFields()
    {
        var admin = await UserAsync(SystemRoles.Admin); var managerA = await UserAsync(); var managerB = await UserAsync();
        var a = await ClubAsync(admin, managerA); var b = await ClubAsync(admin, managerB);
        using var clientA = await ClientAsync(managerA);
        var list = await clientA.GetStringAsync("/ClubManagement");
        Assert.Contains($"data-club-id=\"{a.Id}\"", list); Assert.DoesNotContain($"data-club-id=\"{b.Id}\"", list);
        Assert.Contains("AccessDenied", (await clientA.GetAsync($"/ClubManagement/Edit/{b.Id}")).Headers.Location!.OriginalString);
        var form = EditForm(a); form["__RequestVerificationToken"] = Hidden(await clientA.GetStringAsync($"/ClubManagement/Edit/{a.Id}"), "__RequestVerificationToken");
        var forbidden = await clientA.PostAsync($"/ClubManagement/Edit/{b.Id}", new FormUrlEncodedContent(form));
        Assert.Contains("AccessDenied", forbidden.Headers.Location!.OriginalString);
        form["Code"] = "HIJACK"; form["Status"] = "3"; form["ClubId"] = b.Id.ToString(); form["Name"] = "Tên được cập nhật";
        Assert.Equal(HttpStatusCode.Redirect, (await clientA.PostAsync($"/ClubManagement/Edit/{a.Id}", new FormUrlEncodedContent(form))).StatusCode);
        var updated = await ReadClubAsync(a.Id);
        Assert.Equal(a.Code, updated.Code); Assert.Equal(ClubStatus.Active, updated.Status); Assert.Equal("Tên được cập nhật", updated.Name);
        Assert.Equal(b.Name, (await ReadClubAsync(b.Id)).Name);
        using var member = await ClientAsync(await UserAsync());
        Assert.Contains("AccessDenied", (await member.GetAsync($"/ClubManagement/Edit/{a.Id}")).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task EditChecksVersionAndHandlesMaximumUnicodeAndHtmlAsText()
    {
        var admin = await UserAsync(SystemRoles.Admin); var manager = await UserAsync(); var club = await ClubAsync(admin, manager);
        using var client = await ClientAsync(manager);
        var form = EditForm(club); form["Name"] = new string('Á', 150); form["Category"] = new string('界', 100); form["Description"] = new string('文', 2000);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, $"/ClubManagement/Edit/{club.Id}", form)).StatusCode);
        var stale = await PostAsync(client, $"/ClubManagement/Edit/{club.Id}", EditForm(club));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var updated = await ReadClubAsync(club.Id); Assert.Equal(form["Description"], updated.Description);
        var xss = EditForm(updated); xss["Description"] = "</textarea><script>alert(1)</script>";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, $"/ClubManagement/Edit/{club.Id}", xss)).StatusCode);
        var html = await client.GetStringAsync($"/ClubManagement/Edit/{club.Id}");
        Assert.DoesNotContain("</textarea><script>", html); Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task TransferPreservesMembershipsAndRevokesOldManagersExistingSession()
    {
        var adminUser = await UserAsync(SystemRoles.Admin); var old = await UserAsync(); var next = await UserAsync();
        var club = await ClubAsync(adminUser, old); await MemberAsync(club.Id, next.Id);
        using var admin = await ClientAsync(adminUser); using var oldClient = await ClientAsync(old); using var nextClient = await ClientAsync(next);
        var oldForm = EditForm(club); oldForm["__RequestVerificationToken"] = Hidden(await oldClient.GetStringAsync($"/ClubManagement/Edit/{club.Id}"), "__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.Redirect, (await TransferAsync(admin, club, next.Id)).StatusCode);
        Assert.Contains("AccessDenied", (await oldClient.GetAsync($"/ClubManagement/Edit/{club.Id}")).Headers.Location!.OriginalString);
        Assert.Contains("AccessDenied", (await oldClient.PostAsync($"/ClubManagement/Edit/{club.Id}", new FormUrlEncodedContent(oldForm))).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await nextClient.GetAsync($"/ClubManagement/Edit/{club.Id}")).StatusCode);
        await using var db = database.CreateContext();
        var members = await db.Memberships.Where(member => member.ClubId == club.Id).ToListAsync();
        Assert.Equal(2, members.Count); Assert.All(members, member => { Assert.Equal(MembershipStatus.Active, member.Status); Assert.Null(member.EndedAt); });
        Assert.Equal(ClubRole.Member, members.Single(member => member.UserId == old.Id).Role);
        Assert.Equal(next.Id, Assert.Single(members, member => member.Role == ClubRole.Manager).UserId);
        var audit = await db.AuditLogs.SingleAsync(log => log.EntityId == club.Id.ToString() && log.Action == "ClubManagerTransferred");
        Assert.Equal(adminUser.Id, audit.ActorId); Assert.Equal("Bàn giao nhiệm kỳ", audit.Reason);
        Assert.Contains(old.Id, audit.BeforeData!); Assert.Contains(next.Id, audit.AfterData);
        var transferredClub = await ReadClubAsync(club.Id);
        Assert.False(club.RowVersion.SequenceEqual(transferredClub.RowVersion));
    }

    [Theory]
    [InlineData("outsider")]
    [InlineData("suspended")]
    [InlineData("left")]
    [InlineData("disabled")]
    [InlineData("dual-role")]
    [InlineData("other-club")]
    public async Task TransferRejectsIneligiblePeopleAndNeverCreatesMembership(string condition)
    {
        var actor = await UserAsync(SystemRoles.Admin); var old = await UserAsync(); var next = await UserAsync();
        var club = await ClubAsync(actor, old);
        if (condition is "suspended" or "left" or "disabled" or "dual-role") await MemberAsync(club.Id, next.Id);
        if (condition == "other-club") await MemberAsync((await ClubAsync(actor, await UserAsync())).Id, next.Id);
        await using (var db = database.CreateContext())
        {
            if (condition == "disabled") (await db.Users.FindAsync(next.Id))!.Status = UserStatus.Disabled;
            if (condition is "suspended" or "left")
            {
                var member = await db.Memberships.SingleAsync(m => m.ClubId == club.Id && m.UserId == next.Id);
                member.Status = condition == "left" ? MembershipStatus.Left : MembershipStatus.Suspended;
                if (condition == "left") member.EndedAt = DateTimeOffset.UtcNow;
            }
            if (condition == "dual-role") db.UserRoles.Add(new IdentityUserRole<string> { UserId = next.Id, RoleId = "system-admin" });
            await db.SaveChangesAsync();
        }
        using var admin = await ClientAsync(actor);
        var get = await admin.GetStringAsync($"/Admin/Clubs/Transfer/{club.Id}");
        Assert.DoesNotContain($"value=\"{next.Id}\"", get);
        var response = await TransferAsync(admin, club, next.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("Chỉ được chọn thành viên", await HtmlAsync(response));
        await using var after = database.CreateContext();
        Assert.Equal(old.Id, (await after.Memberships.SingleAsync(m => m.ClubId == club.Id && m.Role == ClubRole.Manager)).UserId);
        if (condition is "outsider" or "other-club") Assert.False(await after.Memberships.AnyAsync(m => m.ClubId == club.Id && m.UserId == next.Id));
    }

    [Fact]
    public async Task FormsRequireAntiforgeryAndAreNotCached()
    {
        var adminUser = await UserAsync(SystemRoles.Admin); var club = await ClubAsync(adminUser, await UserAsync());
        using var admin = await ClientAsync(adminUser);
        foreach (var path in new[] { "/Admin/Clubs/Create", $"/ClubManagement/Edit/{club.Id}", $"/Admin/Clubs/Transfer/{club.Id}" })
        {
            Assert.True((await admin.GetAsync(path)).Headers.CacheControl!.NoStore);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(path, new FormUrlEncodedContent([]))).StatusCode);
        }
    }

    [Theory]
    [InlineData("Reason", "")]
    [InlineData("NewManagerUserId", "")]
    [InlineData("RowVersion", "")]
    [InlineData("RowVersion", "forged-version")]
    public async Task TransferValidatesPostedFields(string field, string value)
    {
        var actor = await UserAsync(SystemRoles.Admin); var next = await UserAsync(); var club = await ClubAsync(actor, await UserAsync());
        await MemberAsync(club.Id, next.Id); using var admin = await ClientAsync(actor);
        var form = TransferForm(club, next.Id); form[field] = value;
        var response = await PostAsync(admin, $"/Admin/Clubs/Transfer/{club.Id}", form);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        await using var db = database.CreateContext();
        Assert.Equal(ClubRole.Member, (await db.Memberships.SingleAsync(m => m.ClubId == club.Id && m.UserId == next.Id)).Role);
    }

    [Fact]
    public async Task AccountCannotBeDisabledUntilEveryClubHasBeenTransferred()
    {
        var actor = await UserAsync(SystemRoles.Admin); var old = await UserAsync(); var next = await UserAsync();
        var a = await ClubAsync(actor, old); var b = await ClubAsync(actor, old);
        await MemberAsync(a.Id, next.Id); await MemberAsync(b.Id, next.Id); using var admin = await ClientAsync(actor);
        Assert.Contains("chuyển giao tất cả CLB", await HtmlAsync(await DisableAsync(admin, old.Id)));
        Assert.Equal(HttpStatusCode.Redirect, (await TransferAsync(admin, a, next.Id)).StatusCode);
        Assert.Contains("chuyển giao tất cả CLB", await HtmlAsync(await DisableAsync(admin, old.Id)));
        Assert.Equal(HttpStatusCode.Redirect, (await TransferAsync(admin, b, next.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await DisableAsync(admin, old.Id)).StatusCode);
        await using var db = database.CreateContext();
        Assert.Equal(UserStatus.Disabled, (await db.Users.FindAsync(old.Id))!.Status);
    }

    [Fact]
    public async Task SimultaneousTransfersAllowOnlyOneWinnerAndKeepExactlyOneManager()
    {
        var actor = await UserAsync(SystemRoles.Admin); var club = await ClubAsync(actor, await UserAsync());
        var a = await UserAsync(); var b = await UserAsync(); await MemberAsync(club.Id, a.Id); await MemberAsync(club.Id, b.Id);
        using var first = await ClientAsync(actor); using var second = await ClientAsync(actor);
        var responses = await Task.WhenAll(TransferAsync(first, club, a.Id), TransferAsync(second, club, b.Id));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Redirect); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Memberships.CountAsync(m => m.ClubId == club.Id && m.Role == ClubRole.Manager && m.EndedAt == null));
        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.EntityId == club.Id.ToString() && log.Action == "ClubManagerTransferred"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssignmentRacingWithDisableNeverLeavesDisabledManager(bool transfer)
    {
        var actor = await UserAsync(SystemRoles.Admin); var target = await UserAsync();
        Club? club = null; var form = NewClub(target);
        if (transfer) { club = await ClubAsync(actor, await UserAsync()); await MemberAsync(club.Id, target.Id); }
        using var first = await ClientAsync(actor); using var second = await ClientAsync(actor);
        var assignment = transfer ? TransferAsync(first, club!, target.Id) : PostAsync(first, "/Admin/Clubs/Create", form);
        var responses = await Task.WhenAll(assignment, DisableAsync(second, target.Id));
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.OK, HttpStatusCode.Conflict }));
        await using var db = database.CreateContext();
        var user = (await db.Users.FindAsync(target.Id))!;
        var isManager = await db.Memberships.AnyAsync(m => m.UserId == target.Id && m.Role == ClubRole.Manager && m.EndedAt == null);
        Assert.True(!isManager || user.Status == UserStatus.Active);
        Assert.True(isManager || user.Status == UserStatus.Disabled);
    }

    [Theory]
    [InlineData("ClubCreated")]
    [InlineData("ClubProfileUpdated")]
    [InlineData("ClubManagerTransferred")]
    public async Task AuditFailureRollsBackEntireBusinessOperation(string action)
    {
        var actor = await UserAsync(SystemRoles.Admin); var manager = await UserAsync(); var next = await UserAsync();
        var club = await ClubAsync(actor, manager); await MemberAsync(club.Id, next.Id);
        var create = new CreateClubViewModel { Code = "ROLLBACK-" + Guid.NewGuid().ToString("N")[..12], Name = "Không được lưu", ManagerStudentCode = manager.Code };
        // Chỉ tạo constraint trong database thuộc fixture; mô phỏng audit không ghi được sau các bước nghiệp vụ.
        await using var db = database.CreateContext(); database.AssertOwnsContext(db);
        var allowed = new[] { "ClubCreated", "ClubProfileUpdated", "ClubManagerTransferred" };
        Assert.Contains(action, allowed);
        var cutoff = await db.AuditLogs.MaxAsync(log => log.Id);
        // DDL không nhận parameter ở CHECK: action thuộc allowlist cố định, cutoff là số từ SQL.
        var constraintSql = string.Concat("ALTER TABLE [AuditLogs] ADD CONSTRAINT [CK_Test_RejectClubAudit] CHECK ([Action] <> N'",
            action, "' OR [Id] <= ", cutoff.ToString(System.Globalization.CultureInfo.InvariantCulture), ");");
        await db.Database.ExecuteSqlRawAsync(constraintSql);
        try
        {
            await using var scope = app.Services.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<ClubManagementService>();
            await Assert.ThrowsAsync<DbUpdateException>(() => action switch
            {
                "ClubCreated" => service.CreateAsync(create, actor.Id),
                "ClubProfileUpdated" => service.EditAsync(club.Id, new() { Name = "Không được lưu", RowVersion = Convert.ToBase64String(club.RowVersion) }, actor.Id),
                _ => service.TransferAsync(club.Id, new() { NewManagerUserId = next.Id, Reason = "Không được lưu", RowVersion = Convert.ToBase64String(club.RowVersion) }, actor.Id)
            });
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE [AuditLogs] DROP CONSTRAINT [CK_Test_RejectClubAudit];"); }
        Assert.False(await db.Clubs.AnyAsync(c => c.Code == create.Code));
        var after = await ReadClubAsync(club.Id); Assert.Equal(club.Name, after.Name); Assert.Equal(club.RowVersion, after.RowVersion);
        Assert.Equal(manager.Id, (await db.Memberships.SingleAsync(m => m.ClubId == club.Id && m.Role == ClubRole.Manager)).UserId);
    }

    private async Task<TestUser> UserAsync(string role = SystemRoles.Student)
    {
        await using var scope = app.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var code = Guid.NewGuid().ToString("N")[..24].ToUpperInvariant(); var email = $"club-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Email = email, UserName = email, FullName = "Sinh viên kiểm thử CLB", StudentCode = code };
        var password = "Club-Test-Password!9";
        Assert.True((await users.CreateAsync(user, password)).Succeeded); Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return new(user.Id, code, email, password);
    }
    private async Task<HttpClient> ClientAsync(TestUser user)
    {
        var client = app.BrowserClient(); var token = Hidden(await client.GetStringAsync("/Account/Login"), "__RequestVerificationToken");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Email"] = user.Email, ["Password"] = user.Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); return client;
    }
    private async Task<Club> ClubAsync(TestUser actor, TestUser manager)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ClubManagementService>().CreateAsync(new()
        { Code = "CLB-" + Guid.NewGuid().ToString("N")[..16], Name = "Câu lạc bộ kiểm thử", Category = "Học thuật", ManagerStudentCode = manager.Code }, actor.Id);
        Assert.True(result.Succeeded, result.Message); return await ReadClubAsync(result.ClubId!.Value);
    }
    private async Task MemberAsync(int clubId, string userId)
    {
        await using var db = database.CreateContext(); db.Memberships.Add(new() { ClubId = clubId, UserId = userId }); await db.SaveChangesAsync();
    }
    private async Task<Club> ReadClubAsync(int id)
    {
        await using var db = database.CreateContext(); return await db.Clubs.AsNoTracking().SingleAsync(c => c.Id == id);
    }
    private static Dictionary<string, string> NewClub(TestUser manager) => new()
    { ["Code"] = "CLB-" + Guid.NewGuid().ToString("N")[..16], ["Name"] = "Câu lạc bộ mới", ["Category"] = "Học thuật", ["ManagerStudentCode"] = manager.Code };
    private static Dictionary<string, string> EditForm(Club club) => new()
    { ["Name"] = club.Name, ["Category"] = club.Category ?? "", ["Description"] = club.Description ?? "", ["RowVersion"] = Convert.ToBase64String(club.RowVersion) };
    private static Dictionary<string, string> TransferForm(Club club, string targetId) => new()
    { ["NewManagerUserId"] = targetId, ["RowVersion"] = Convert.ToBase64String(club.RowVersion), ["Reason"] = "Bàn giao nhiệm kỳ" };
    private static Task<HttpResponseMessage> TransferAsync(HttpClient client, Club club, string id) => PostAsync(client, $"/Admin/Clubs/Transfer/{club.Id}", TransferForm(club, id));
    private static async Task<HttpResponseMessage> DisableAsync(HttpClient client, string id)
    {
        var path = $"/Admin/Students/ChangeStatus/{id}"; var html = await client.GetStringAsync(path);
        return await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken"), ["ConcurrencyStamp"] = Hidden(html, "ConcurrencyStamp"), ["NewStatus"] = "2", ["Reason"] = "Kiểm thử" }));
    }
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = Hidden(await client.GetStringAsync(path), "__RequestVerificationToken");
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }
    private static async Task<string> HtmlAsync(HttpResponseMessage response) => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    private static string Hidden(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, "Thiếu " + name); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private sealed record TestUser(string Id, string Code, string Email, string Password);
}
