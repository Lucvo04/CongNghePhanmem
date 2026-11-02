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

public class PersonalProfileTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    public Task InitializeAsync() { app = new ClubWebFactory(database.ConnectionString); return Task.CompletedTask; }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task AnonymousCannotReadOrSubmit()
    {
        using var client = app.BrowserClient();
        foreach (var response in new[] { await client.GetAsync("/Account/EditProfile"), await client.PostAsync("/Account/EditProfile", new FormUrlEncodedContent([])) })
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
        }
    }

    [Fact]
    public async Task RequiresAntiforgeryAndDoesNotCache()
    {
        var user = await CreateAsync(); using var client = await LoginAsync(user);
        var before = await ReadAsync(user.Id);
        Assert.True((await client.GetAsync("/Account/EditProfile")).Headers.CacheControl!.NoStore);
        Assert.Contains("/Account/EditProfile", await client.GetStringAsync("/Account/Overview"));
        var post = await client.PostAsync("/Account/EditProfile", new FormUrlEncodedContent(new Dictionary<string, string> { ["FullName"] = "Unauthorized change" }));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        Assert.Equal(before.ConcurrencyStamp, (await ReadAsync(user.Id)).ConcurrencyStamp);
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Admin)]
    public async Task SavesOwnProfilePreservesCredentialsRolesAndOtherSession(string role)
    {
        var user = await CreateAsync(role); using var client = await LoginAsync(user); using var other = await LoginAsync(user);
        var before = await ReadAsync(user.Id);
        var response = await PostAsync(client, await FormAsync(client));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Overview", response.Headers.Location!.OriginalString);
        var after = await ReadAsync(user.Id);
        Assert.Equal("Hồ sơ đã cập nhật", after.FullName); Assert.Equal("+84901234567", after.PhoneNumber);
        Assert.Equal("D24", after.ClassName); Assert.Equal("Công nghệ thông tin", after.Faculty);
        Assert.False(after.PhoneNumberConfirmed);
        Assert.Equal(before.Email, after.Email); Assert.Equal(before.StudentCode, after.StudentCode);
        Assert.Equal(before.PasswordHash, after.PasswordHash); Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.Status, after.Status); Assert.NotEqual(before.ConcurrencyStamp, after.ConcurrencyStamp);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal(new[] { role }, await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(after));
        var html = WebUtility.HtmlDecode(await other.GetStringAsync("/Account/Overview"));
        Assert.Contains(after.FullName, html); Assert.Contains(after.PhoneNumber!, html); Assert.Contains(after.Faculty!, html);
        Assert.Contains(after.FullName, WebUtility.HtmlDecode(await client.GetStringAsync("/Account/EditProfile")));
        await using var db = database.CreateContext();
        var audit = await db.AuditLogs.SingleAsync(x => x.ActorId == user.Id && x.Action == "PersonalProfileUpdated");
        Assert.Equal(user.Id, audit.EntityId); Assert.Contains("PhoneNumber", audit.AfterData);
        Assert.DoesNotContain(after.PhoneNumber!, audit.AfterData); Assert.DoesNotContain(after.FullName, audit.AfterData);
    }

    [Theory]
    [InlineData("FullName", "   ")]
    [InlineData("FullName", "long")]
    [InlineData("PhoneNumber", "abc12345")]
    [InlineData("PhoneNumber", "123456")]
    [InlineData("PhoneNumber", "1234567890123456")]
    [InlineData("PhoneNumber", "090 123 4567")]
    [InlineData("ClassName", "long")]
    [InlineData("Faculty", "long")]
    [InlineData("ConcurrencyStamp", "")]
    public async Task InvalidFieldsReturnErrorsWithoutSaving(string field, string value)
    {
        var user = await CreateAsync(); using var client = await LoginAsync(user);
        var before = await ReadAsync(user.Id); var fields = await FormAsync(client);
        fields[field] = value == "long" ? new string('x', field == "FullName" ? 151 : field == "ClassName" ? 51 : 101) : value;
        var response = await PostAsync(client, fields);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
        Assert.Equal(before.ConcurrencyStamp, (await ReadAsync(user.Id)).ConcurrencyStamp);
        await using var db = database.CreateContext(); Assert.False(await db.AuditLogs.AnyAsync(x => x.ActorId == user.Id));
    }

    [Fact]
    public async Task PostedIdentityAndSecurityFieldsCannotTargetOthersOrElevatePrivileges()
    {
        var owner = await CreateAsync(); var victim = await CreateAsync(); using var client = await LoginAsync(owner);
        var victimBefore = await ReadAsync(victim.Id); var before = await ReadAsync(owner.Id);
        var fields = await FormAsync(client);
        fields["Id"] = victim.Id; fields["UserId"] = victim.Id; fields["Email"] = victim.Email;
        fields["StudentCode"] = "REPLACED"; fields["Role"] = "Admin"; fields["Status"] = "2";
        fields["PasswordHash"] = "injected"; fields["SecurityStamp"] = "injected";
        fields["PhoneNumberConfirmed"] = "true"; fields["EmailConfirmed"] = "true";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, fields)).StatusCode);
        var after = await ReadAsync(owner.Id);
        Assert.Equal(before.Email, after.Email); Assert.Equal(before.StudentCode, after.StudentCode);
        Assert.Equal(before.PasswordHash, after.PasswordHash); Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(UserStatus.Active, after.Status); Assert.False(after.PhoneNumberConfirmed); Assert.False(after.EmailConfirmed);
        Assert.Equal(victimBefore.ConcurrencyStamp, (await ReadAsync(victim.Id)).ConcurrencyStamp);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(after, SystemRoles.Admin));
    }

    [Fact]
    public async Task StaleFormCannotOverwriteAndFreshFormCanSave()
    {
        var user = await CreateAsync(); using var client = await LoginAsync(user);
        var first = await FormAsync(client); var stale = await FormAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, first)).StatusCode);
        stale["FullName"] = "Stale change";
        var response = await PostAsync(client, stale);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Tải lại hồ sơ", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal("Hồ sơ đã cập nhật", (await ReadAsync(user.Id)).FullName);
        var fresh = await FormAsync(client); fresh["FullName"] = "Fresh change";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, fresh)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentFormsAllowOnlyOneWriter()
    {
        var user = await CreateAsync(); using var a = await LoginAsync(user); using var b = await LoginAsync(user);
        var fa = await FormAsync(a); var fb = await FormAsync(b); fa["FullName"] = "Writer A"; fb["FullName"] = "Writer B";
        var responses = await Task.WhenAll(PostAsync(a, fa), PostAsync(b, fb));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Redirect);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = database.CreateContext(); Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.ActorId == user.Id));
    }

    [Fact]
    public async Task BlankOptionalFieldsClearAndMarkupIsEncoded()
    {
        var user = await CreateAsync(); using var client = await LoginAsync(user); var fields = await FormAsync(client);
        fields["FullName"] = "<script>alert('profile')</script>";
        fields["PhoneNumber"] = ""; fields["ClassName"] = "  "; fields["Faculty"] = "";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, fields)).StatusCode);
        var after = await ReadAsync(user.Id);
        Assert.Null(after.PhoneNumber); Assert.Null(after.ClassName); Assert.Null(after.Faculty);
        var html = await client.GetStringAsync("/Account/Overview");
        Assert.DoesNotContain(fields["FullName"], html); Assert.Contains("&lt;script&gt;", html);
        using var guest = app.BrowserClient();
        Assert.DoesNotContain(fields["FullName"], WebUtility.HtmlDecode(await guest.GetStringAsync("/Clubs")));
    }

    [Fact]
    public async Task UnchangedProfileDoesNotWriteAuditOrRotateStamp()
    {
        var user = await CreateAsync(); using var client = await LoginAsync(user);
        var before = await ReadAsync(user.Id); var fields = await FormAsync(client);
        fields["FullName"] = before.FullName; fields["PhoneNumber"] = before.PhoneNumber!;
        fields["ClassName"] = before.ClassName!; fields["Faculty"] = before.Faculty!;
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, fields)).StatusCode);
        var after = await ReadAsync(user.Id); Assert.Equal(before.ConcurrencyStamp, after.ConcurrencyStamp); Assert.True(after.PhoneNumberConfirmed);
        await using var db = database.CreateContext(); Assert.False(await db.AuditLogs.AnyAsync(x => x.ActorId == user.Id));
    }

    [Fact]
    public async Task DisabledAccountCannotSubmitAndServiceReloadsTrackedUser()
    {
        var user = await CreateAsync(); using var client = await LoginAsync(user); var fields = await FormAsync(client);
        await using var scope = app.Services.CreateAsyncScope();
        var tracked = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id))!;
        await using (var db = database.CreateContext()) { var row = await db.Users.SingleAsync(x => x.Id == user.Id); row.Status = UserStatus.Disabled; await db.SaveChangesAsync(); }
        var result = await scope.ServiceProvider.GetRequiredService<PersonalProfileService>().UpdateAsync(user.Id,
            new UpdatePersonalProfile("Should not save", null, null, null, tracked.ConcurrencyStamp!));
        Assert.Contains(result.Errors, x => x.Code == "UnavailableAccount");
        var response = await PostAsync(client, fields); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
        Assert.NotEqual("Should not save", (await ReadAsync(user.Id)).FullName);
    }

    [Fact]
    public async Task AuditFailureRollsBackProfileAndStamp()
    {
        var user = await CreateAsync(); var before = await ReadAsync(user.Id);
        await using var db = database.CreateContext(); database.AssertOwnsContext(db);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE AuditLogs WITH NOCHECK ADD CONSTRAINT CK_QA_ProfileAuditFault CHECK ([Action] <> 'PersonalProfileUpdated')");
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<PersonalProfileService>().UpdateAsync(user.Id,
                new UpdatePersonalProfile("Rollback test", null, null, null, before.ConcurrencyStamp!)));
            var after = await ReadAsync(user.Id);
            Assert.Equal(before.FullName, after.FullName); Assert.Equal(before.PhoneNumber, after.PhoneNumber);
            Assert.Equal(before.ConcurrencyStamp, after.ConcurrencyStamp); Assert.Equal(before.PhoneNumberConfirmed, after.PhoneNumberConfirmed);
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE AuditLogs DROP CONSTRAINT CK_QA_ProfileAuditFault"); }
    }

    private async Task<TestUser> CreateAsync(string role = SystemRoles.Student)
    {
        await using var scope = app.Services.CreateAsyncScope(); database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var suffix = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { UserName = $"profile-{suffix}@example.test", Email = $"profile-{suffix}@example.test",
            FullName = "Hồ sơ ban đầu", StudentCode = suffix[..24], ClassName = "Old class", Faculty = "Old faculty", PhoneNumber = "0901234567", PhoneNumberConfirmed = true };
        const string password = "Initial-Profile-Test!9";
        Assert.True((await manager.CreateAsync(user, password)).Succeeded); Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        return new(user.Id, user.Email, password);
    }
    private async Task<ApplicationUser> ReadAsync(string id) { await using var db = database.CreateContext(); return await db.Users.AsNoTracking().SingleAsync(x => x.Id == id); }
    private async Task<HttpClient> LoginAsync(TestUser user)
    {
        var client = app.BrowserClient(); var page = await client.GetStringAsync("/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Email"] = user.Email, ["Password"] = user.Password, ["__RequestVerificationToken"] = Input(page, "__RequestVerificationToken") }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); return client;
    }
    private static async Task<Dictionary<string, string>> FormAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/Account/EditProfile");
        return new() { ["FullName"] = "  Hồ sơ đã cập nhật  ", ["PhoneNumber"] = "+84901234567", ["ClassName"] = "  D24  ",
            ["Faculty"] = "  Công nghệ thông tin  ", ["ConcurrencyStamp"] = Input(html, "ConcurrencyStamp"), ["__RequestVerificationToken"] = Input(html, "__RequestVerificationToken") };
    }
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Dictionary<string, string> fields) => client.PostAsync("/Account/EditProfile", new FormUrlEncodedContent(fields));
    private static string Input(string html, string name) { var match = Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]+)\""); Assert.True(match.Success); return WebUtility.HtmlDecode(match.Groups[1].Value); }
    private sealed record TestUser(string Id, string Email, string Password);
}
