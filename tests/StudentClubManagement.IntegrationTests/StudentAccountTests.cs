using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;

namespace StudentClubManagement.IntegrationTests;

public class StudentAccountTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;

    public Task InitializeAsync()
    {
        app = new ClubWebFactory(database.ConnectionString);
        using var scope = app.Services.CreateScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await app.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousAndStudentsCannotReadOrPostAdminEndpoints(bool signedIn)
    {
        using var client = app.BrowserClient();
        if (signedIn) await LoginAsync(client, await SeedUserAsync());
        foreach (var path in new[] { "/Admin/Students", "/Admin/Students/Create", "/Admin/Students/ChangeStatus/unknown" })
        {
            var responses = new List<HttpResponseMessage> { await client.GetAsync(path) };
            if (path != "/Admin/Students") responses.Add(await client.PostAsync(path, new FormUrlEncodedContent([])));
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Contains(signedIn ? "/Account/AccessDenied" : "/Account/Login", response.Headers.Location!.OriginalString);
            }
        }
    }

    [Fact]
    public async Task AdminFormsRequireAntiforgeryAndDoNotCachePersonalData()
    {
        using var client = await AdminClientAsync();
        var student = await SeedUserAsync();
        foreach (var path in new[] { "/Admin/Students", "/Admin/Students/Create", $"/Admin/Students/ChangeStatus/{student.Id}" })
        {
            var get = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            Assert.True(get.Headers.CacheControl!.NoStore);
        }
        foreach (var path in new[] { "/Admin/Students/Create", $"/Admin/Students/ChangeStatus/{student.Id}" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, new FormUrlEncodedContent(NewForm()))).StatusCode);
        Assert.Equal(UserStatus.Active, (await ReadUserAsync(student.Id)).Status);
    }

    [Fact]
    public async Task CreateNormalizesFieldsKeepsLeadingZeroHashesPasswordAndIgnoresOverpostedPrivileges()
    {
        var admin = await SeedUserAsync(SystemRoles.Admin);
        using var client = app.BrowserClient();
        await LoginAsync(client, admin);
        var form = NewForm();
        form["FullName"] = "  Sinh viên kiểm thử  ";
        var code = "00" + Guid.NewGuid().ToString("N")[..20];
        form["StudentCode"] = " " + code + " ";
        var email = form["Email"];
        form["Email"] = " " + email.ToUpperInvariant() + " ";
        form["ClassName"] = "  CNTT-01  "; form["Faculty"] = "  Công nghệ thông tin  ";
        form["Role"] = "Admin"; form["Status"] = "2"; form["Id"] = admin.Id;
        Assert.Equal(HttpStatusCode.Redirect, (await CreateAsync(client, form)).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await db.Users.SingleAsync(user => user.Email == email);
        Assert.NotEqual(admin.Id, created.Id);
        Assert.Equal(code.ToUpperInvariant(), created.StudentCode);
        Assert.Equal("Sinh viên kiểm thử", created.FullName);
        Assert.Equal("CNTT-01", created.ClassName); Assert.Equal("Công nghệ thông tin", created.Faculty);
        Assert.Equal(UserStatus.Active, created.Status);
        Assert.Equal(new[] { SystemRoles.Student }, await manager.GetRolesAsync(created));
        Assert.NotEqual(form["Password"], created.PasswordHash);
        Assert.True(await manager.CheckPasswordAsync(created, form["Password"]));
        var audit = await db.AuditLogs.SingleAsync(log => log.EntityId == created.Id);
        Assert.Equal(admin.Id, audit.ActorId); Assert.Equal("StudentCreated", audit.Action); Assert.Null(audit.BeforeData);
        Assert.DoesNotContain(form["Password"], audit.AfterData);
        Assert.DoesNotContain(created.PasswordHash!, audit.AfterData);
        Assert.Equal("{\"Status\":\"Active\",\"Role\":\"Student\"}", audit.AfterData);
        using var studentClient = app.BrowserClient();
        await LoginAsync(studentClient, new(created.Id, email, form["Password"]));
        Assert.Equal(HttpStatusCode.OK, (await studentClient.GetAsync("/Account/Overview")).StatusCode);
        Assert.Contains("AccessDenied", (await studentClient.GetAsync("/Admin/Students")).Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("StudentCode", "", "Vui lòng nhập mã sinh viên")]
    [InlineData("FullName", "   ", "Vui lòng nhập họ và tên")]
    [InlineData("Email", "invalid-email", "Email chưa đúng định dạng")]
    [InlineData("StudentCode", "code with space", "không chứa khoảng trắng")]
    [InlineData("StudentCode", "1234567890123456789012345", "tối đa 24 ký tự")]
    [InlineData("ConfirmPassword", "Mismatch!9", "Mật khẩu xác nhận chưa khớp")]
    [InlineData("Password", "aaaaaaaaaaaa", "chữ hoa")]
    [InlineData("Password", "Short!9", "ít nhất 10 ký tự")]
    public async Task ServerRejectsInvalidCreationWithoutEchoingPasswords(string field, string value, string expected)
    {
        using var client = await AdminClientAsync();
        var form = NewForm(); form[field] = value;
        if (field == "Password") form["ConfirmPassword"] = value;
        var response = await CreateAsync(client, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(expected, html);
        Assert.DoesNotContain(form["Password"], html); Assert.DoesNotContain(form["ConfirmPassword"], html);
        await using var db = database.CreateContext();
        Assert.False(await db.Users.AnyAsync(user => user.Email == form["Email"]));
    }

    [Theory]
    [InlineData("Email", "Email đã được sử dụng")]
    [InlineData("StudentCode", "Mã sinh viên đã được sử dụng")]
    public async Task DuplicateEmailOrCodeIsRejectedIgnoringCase(string field, string expected)
    {
        using var client = await AdminClientAsync();
        var first = NewForm();
        Assert.Equal(HttpStatusCode.Redirect, (await CreateAsync(client, first)).StatusCode);
        var duplicate = NewForm(); duplicate[field] = first[field].ToUpperInvariant();
        var response = await CreateAsync(client, duplicate);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expected, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(user => user.Email == first["Email"]));
        Assert.False(await db.Users.AnyAsync(user => user.StudentCode == duplicate["StudentCode"] && user.Email == duplicate["Email"]));
    }

    [Theory]
    [InlineData("Email")]
    [InlineData("StudentCode")]
    public async Task ConcurrentDuplicatesCreateExactlyOneAccountAndAudit(string field)
    {
        using var client1 = await AdminClientAsync(); using var client2 = await AdminClientAsync();
        var first = NewForm(); var second = NewForm(); second[field] = first[field];
        var responses = await Task.WhenAll(CreateAsync(client1, first), CreateAsync(client2, second));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Redirect);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        await using var db = database.CreateContext();
        var matches = await db.Users.Where(user => user.Email == first["Email"] || user.Email == second["Email"]).ToListAsync();
        Assert.Single(matches);
        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.EntityId == matches[0].Id));
        Assert.Equal(1, await db.UserRoles.CountAsync(role => role.UserId == matches[0].Id));
    }

    [Fact]
    public async Task DisableRevokesActiveAndDormantCookiesAndEnableRequiresFreshLogin()
    {
        var student = await SeedUserAsync();
        using var admin = await AdminClientAsync();
        using var active = app.BrowserClient(); using var dormant = app.BrowserClient();
        await LoginAsync(active, student); await LoginAsync(dormant, student);
        var original = await ReadUserAsync(student.Id);
        Assert.Equal(HttpStatusCode.Redirect, (await ChangeAsync(admin, student.Id)).StatusCode);
        var disabled = await ReadUserAsync(student.Id);
        Assert.Equal(UserStatus.Disabled, disabled.Status); Assert.NotEqual(original.SecurityStamp, disabled.SecurityStamp);
        Assert.Contains("/Account/Login", (await active.GetAsync("/Account/Overview")).Headers.Location!.OriginalString);
        using var fresh = app.BrowserClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginResponseAsync(fresh, student)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ChangeAsync(admin, student.Id)).StatusCode);
        var enabled = await ReadUserAsync(student.Id);
        Assert.Equal(UserStatus.Active, enabled.Status); Assert.NotEqual(disabled.SecurityStamp, enabled.SecurityStamp);
        Assert.Equal(original.PasswordHash, enabled.PasswordHash);
        Assert.Contains("/Account/Login", (await dormant.GetAsync("/Account/Overview")).Headers.Location!.OriginalString);
        await LoginAsync(fresh, student);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/Account/Overview")).StatusCode);
        await using var db = database.CreateContext();
        var audits = await db.AuditLogs.Where(log => log.EntityId == student.Id).OrderBy(log => log.Id).ToListAsync();
        Assert.Equal(new[] { "StudentDisabled", "StudentEnabled" }, audits.Select(log => log.Action));
        Assert.All(audits, log => Assert.Equal("Kiểm thử thay đổi trạng thái", log.Reason));
        Assert.Equal("{\"Status\":\"Active\"}", audits[0].BeforeData);
        Assert.Equal("{\"Status\":\"Disabled\"}", audits[0].AfterData);
    }

    [Fact]
    public async Task EnableDoesNotResetPasswordLockout()
    {
        var student = await SeedUserAsync(); using var admin = await AdminClientAsync();
        await using (var db = database.CreateContext())
        {
            var user = await db.Users.FindAsync(student.Id);
            user!.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15); user.AccessFailedCount = 5;
            await db.SaveChangesAsync();
        }
        var original = await ReadUserAsync(student.Id);
        Assert.Equal(HttpStatusCode.Redirect, (await ChangeAsync(admin, student.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ChangeAsync(admin, student.Id)).StatusCode);
        var enabled = await ReadUserAsync(student.Id);
        Assert.Equal(original.LockoutEnd, enabled.LockoutEnd); Assert.Equal(5, enabled.AccessFailedCount);
    }

    [Fact]
    public async Task StaleConfirmationCannotOverwriteOrRepeatChange()
    {
        var student = await SeedUserAsync(); using var admin = await AdminClientAsync();
        var path = $"/Admin/Students/ChangeStatus/{student.Id}";
        var oldForm = StatusForm(await admin.GetStringAsync(path));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync(path, new FormUrlEncodedContent(oldForm))).StatusCode);
        var stale = await admin.PostAsync(path, new FormUrlEncodedContent(oldForm));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("Tải lại thông tin", WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync()));
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.EntityId == student.Id));
    }

    [Theory]
    [InlineData("Reason", "", "Vui lòng nhập lý do")]
    [InlineData("NewStatus", "999", "validation-summary-errors")]
    [InlineData("ConcurrencyStamp", "", "Thiếu phiên bản tài khoản")]
    public async Task StatusChangesValidateInputOnServer(string field, string value, string expected)
    {
        var student = await SeedUserAsync(); using var admin = await AdminClientAsync();
        var path = $"/Admin/Students/ChangeStatus/{student.Id}";
        var form = StatusForm(await admin.GetStringAsync(path)); form[field] = value;
        var response = await admin.PostAsync(path, new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expected, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(UserStatus.Active, (await ReadUserAsync(student.Id)).Status);
    }

    [Fact]
    public async Task AdminSelfOtherAdminDualRoleAndUnknownTargetsAreProtectedEvenWithForgedPost()
    {
        var owner = await SeedUserAsync(SystemRoles.Admin); var other = await SeedUserAsync(SystemRoles.Admin);
        var dual = await SeedUserAsync(SystemRoles.Admin);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.AddToRoleAsync((await manager.FindByIdAsync(dual.Id))!, SystemRoles.Student)).Succeeded);
        }
        using var admin = app.BrowserClient(); await LoginAsync(admin, owner);
        var token = Hidden(await admin.GetStringAsync("/Admin/Students/Create"), "__RequestVerificationToken");
        foreach (var target in new[] { owner, other, dual, new TestUser("missing", "", "") })
        {
            var path = $"/Admin/Students/ChangeStatus/{target.Id}";
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(path)).StatusCode);
            var post = await admin.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["NewStatus"] = "2", ["Reason"] = "Giả mạo",
                ["ConcurrencyStamp"] = target.Id == "missing" ? "invalid" : (await ReadUserAsync(target.Id)).ConcurrencyStamp!
            }));
            Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
            if (target.Id != "missing") Assert.Equal(UserStatus.Active, (await ReadUserAsync(target.Id)).Status);
        }
        Assert.DoesNotContain($"data-student-id=\"{dual.Id}\"", await admin.GetStringAsync("/Admin/Students?search=" + dual.Email));
    }

    [Fact]
    public async Task AuditFailureRollsBackUserRoleAndStatusStamp()
    {
        var form = NewForm();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<StudentAccountService>();
            await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(new(form["FullName"], form["StudentCode"],
                form["Email"], null, null, form["Password"]), "nonexistent-actor"));
        }
        await using (var db = database.CreateContext())
            Assert.False(await db.Users.AnyAsync(user => user.Email == form["Email"]));
        var student = await SeedUserAsync(); var original = await ReadUserAsync(student.Id);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<StudentAccountService>();
            await Assert.ThrowsAsync<DbUpdateException>(() => service.ChangeStatusAsync(student.Id, original.ConcurrencyStamp!,
                UserStatus.Disabled, "Thử rollback", "nonexistent-actor"));
        }
        var after = await ReadUserAsync(student.Id);
        Assert.Equal(original.Status, after.Status); Assert.Equal(original.SecurityStamp, after.SecurityStamp);
        Assert.Equal(original.ConcurrencyStamp, after.ConcurrencyStamp);
    }

    [Fact]
    public async Task SearchFilterPaginationAndEncodedOutputWorkTogether()
    {
        using var admin = await AdminClientAsync(); var prefix = Guid.NewGuid().ToString("N")[..12];
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            for (var index = 0; index < 12; index++)
            {
                var email = $"page-{prefix}-{index}@example.test";
                var user = new ApplicationUser { Email = email, UserName = email, FullName = "<script>alert(1)</script>",
                    StudentCode = prefix + index.ToString("D2"), Status = index == 11 ? UserStatus.Disabled : UserStatus.Active };
                Assert.True((await manager.CreateAsync(user)).Succeeded);
                Assert.True((await manager.AddToRoleAsync(user, SystemRoles.Student)).Succeeded);
            }
        }
        var page1 = await admin.GetStringAsync($"/Admin/Students?search={prefix}&status=1&page=1");
        var page2 = await admin.GetStringAsync($"/Admin/Students?search={prefix}&status=1&page=999");
        Assert.Equal(10, Regex.Matches(page1, "data-student-id=").Count);
        Assert.Single(Regex.Matches(page2, "data-student-id=").Cast<Match>());
        Assert.Contains("status=1", page1); Assert.Contains("page=2", page1);
        Assert.DoesNotContain("<script>alert(1)</script>", page1); Assert.Contains("&lt;script&gt;", page1);
        var disabled = await admin.GetStringAsync($"/Admin/Students?search={prefix}&status=2");
        Assert.Single(Regex.Matches(disabled, "data-student-id=").Cast<Match>());
        Assert.Contains("Chưa có tài khoản phù hợp", WebUtility.HtmlDecode(await admin.GetStringAsync("/Admin/Students?search=NONEXISTENT-" + Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/Admin/Students?status=999")).StatusCode);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = app.BrowserClient(); await LoginAsync(client, await SeedUserAsync(SystemRoles.Admin)); return client;
    }

    private async Task<TestUser> SeedUserAsync(string role = SystemRoles.Student)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var form = NewForm(); var user = new ApplicationUser { Email = form["Email"], UserName = form["Email"], FullName = form["FullName"], StudentCode = form["StudentCode"] };
        Assert.True((await manager.CreateAsync(user, form["Password"])).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        return new(user.Id, user.Email!, form["Password"]);
    }

    private async Task<ApplicationUser> ReadUserAsync(string id)
    {
        await using var db = database.CreateContext(); return await db.Users.AsNoTracking().SingleAsync(user => user.Id == id);
    }

    private static Dictionary<string, string> NewForm() => new()
    {
        ["FullName"] = "Sinh viên kiểm thử", ["Email"] = $"student-{Guid.NewGuid():N}@example.test",
        ["StudentCode"] = Guid.NewGuid().ToString("N")[..24], ["Password"] = "Initial-Test-Password!9", ["ConfirmPassword"] = "Initial-Test-Password!9"
    };

    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = Hidden(await client.GetStringAsync("/Admin/Students/Create"), "__RequestVerificationToken");
        return await client.PostAsync("/Admin/Students/Create", new FormUrlEncodedContent(form));
    }

    private static async Task<HttpResponseMessage> LoginResponseAsync(HttpClient client, TestUser user)
    {
        var token = Hidden(await client.GetStringAsync("/Account/Login"), "__RequestVerificationToken");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Email"] = user.Email, ["Password"] = user.Password, ["__RequestVerificationToken"] = token }));
    }

    private static async Task LoginAsync(HttpClient client, TestUser user) =>
        Assert.Equal(HttpStatusCode.Redirect, (await LoginResponseAsync(client, user)).StatusCode);

    private static async Task<HttpResponseMessage> ChangeAsync(HttpClient client, string id)
    {
        var path = $"/Admin/Students/ChangeStatus/{id}";
        return await client.PostAsync(path, new FormUrlEncodedContent(StatusForm(await client.GetStringAsync(path))));
    }

    private static Dictionary<string, string> StatusForm(string html) => new()
    {
        ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken"),
        ["ConcurrencyStamp"] = Hidden(html, "ConcurrencyStamp"), ["NewStatus"] = Hidden(html, "NewStatus"),
        ["Reason"] = "Kiểm thử thay đổi trạng thái"
    };

    private static string Hidden(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, "Thiếu trường " + name); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed record TestUser(string Id, string Email, string Password);
}
