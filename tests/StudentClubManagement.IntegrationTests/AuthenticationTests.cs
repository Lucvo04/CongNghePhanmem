using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Controllers;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.IntegrationTests;

public class AuthenticationTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;

    public async Task InitializeAsync()
    {
        app = new ClubWebFactory(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        // Dừng trước mọi thao tác tạo dữ liệu nếu host chưa được tách khỏi database ứng dụng.
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task AnonymousAdminRequestRedirectsToLoginWithReturnUrl()
    {
        using var client = app.BrowserClient();
        var response = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login?ReturnUrl=%2FAdmin", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Overview")).StatusCode);
    }

    [Fact]
    public async Task AdminCanLogInWithCaseInsensitiveEmailAndOpenProtectedPages()
    {
        var user = await CreateUserAsync(SystemRoles.Admin);
        using var client = app.BrowserClient();
        var response = await LoginAsync(client, user.Email.ToUpperInvariant(), user.Password, "/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin", response.Headers.Location!.OriginalString);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("StudentClub.Auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
        var overview = await client.GetAsync("/Account/Overview");
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        Assert.True(overview.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task StudentCanOpenOwnAccountButNotAdmin()
    {
        var user = await CreateUserAsync(SystemRoles.Student);
        using var client = app.BrowserClient();
        var login = await LoginAsync(client, user.Email, user.Password);
        Assert.Equal("/Account/Overview", login.Headers.Location!.OriginalString);
        var overview = await client.GetStringAsync("/Account/Overview");
        Assert.Contains(user.Email, overview);
        Assert.DoesNotContain(">Quản trị</a>", overview);
        var forbidden = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Contains("/Account/AccessDenied", forbidden.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(forbidden.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task InvalidUnknownAndDisabledLoginsUseSameMessageWithoutEchoingPassword()
    {
        var user = await CreateUserAsync(SystemRoles.Student);
        using var client = app.BrowserClient();
        var wrongPassword = "Wrong-Test-Password!9";
        var wrong = await LoginAsync(client, user.Email, wrongPassword);
        var unknown = await LoginAsync(client, $"missing-{Guid.NewGuid():N}@example.test", wrongPassword);
        await ChangeUserAsync(user.Id, u => u.Status = UserStatus.Disabled);
        var disabled = await LoginAsync(client, user.Email, user.Password);
        foreach (var response in new[] { wrong, unknown, disabled })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains(AccountController.LoginFailureMessage, html);
            Assert.DoesNotContain(wrongPassword, html);
            Assert.DoesNotContain(user.Password, html);
            Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("StudentClub.Auth=")));
        }
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Overview")).StatusCode);
    }

    [Fact]
    public async Task FiveFailuresLockAccountAndCorrectPasswordWorksAfterLockExpires()
    {
        var user = await CreateUserAsync(SystemRoles.Student);
        using var client = app.BrowserClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(client, user.Email, "Wrong-Password!9");
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await manager.FindByIdAsync(user.Id);
            Assert.True(await manager.IsLockedOutAsync(stored!));
        }
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, user.Email, user.Password)).StatusCode);
        await ChangeUserAsync(user.Id, u => u.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, user.Email, user.Password)).StatusCode);
    }

    [Fact]
    public async Task DisablingAccountRejectsExistingCookieOnNextRequest()
    {
        var user = await CreateUserAsync(SystemRoles.Admin);
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
        await ChangeUserAsync(user.Id, u => u.Status = UserStatus.Disabled);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Overview")).StatusCode);
    }

    [Fact]
    public async Task SecurityStampChangeRevokesExistingCookie()
    {
        var user = await CreateUserAsync(SystemRoles.Student);
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await manager.UpdateSecurityStampAsync((await manager.FindByIdAsync(user.Id))!)).Succeeded);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Overview")).StatusCode);
    }

    [Fact]
    public async Task RemovedAdminRoleDoesNotRemainInExistingCookie()
    {
        var user = await CreateUserAsync(SystemRoles.Admin);
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await manager.RemoveFromRoleAsync((await manager.FindByIdAsync(user.Id))!, SystemRoles.Admin)).Succeeded);
        var response = await client.GetAsync("/Admin");
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task LoginAndLogoutRequireAntiforgeryAndGetCannotLogout()
    {
        using var client = app.BrowserClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent([]))).StatusCode);
        var user = await CreateUserAsync(SystemRoles.Student);
        await LoginAsync(client, user.Email, user.Password);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Account/Logout")).StatusCode);
        var page = await client.GetStringAsync("/Account/Overview");
        var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", logout.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Overview")).StatusCode);
    }

    [Theory]
    [InlineData("https://example.org/outside")]
    [InlineData("//example.org/outside")]
    [InlineData("/\\example.org/outside")]
    public async Task LoginNeverRedirectsToExternalReturnUrl(string returnUrl)
    {
        var user = await CreateUserAsync(SystemRoles.Student);
        using var client = app.BrowserClient();
        var response = await LoginAsync(client, user.Email, user.Password, returnUrl);
        Assert.Equal("/Account/Overview", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task RememberMeCreatesPersistentCookie()
    {
        var user = await CreateUserAsync(SystemRoles.Student);
        using var client = app.BrowserClient();
        var response = await LoginAsync(client, user.Email, user.Password, remember: true);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("StudentClub.Auth=") && cookie.Contains("expires="));
    }

    [Fact]
    public async Task MissingPasswordShowsVietnameseServerValidation()
    {
        using var client = app.BrowserClient();
        var response = await LoginAsync(client, "student@example.test", "");
        Assert.Contains("Vui lòng nhập mật khẩu.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    private async Task<TestUser> CreateUserAsync(string role)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"auth-{suffix}@example.test";
        var password = $"Test-Only!9-{Guid.NewGuid():N}";
        var user = new ApplicationUser { Email = email, UserName = email, FullName = "Tài khoản kiểm thử", StudentCode = suffix[..24] };
        Assert.True((await manager.CreateAsync(user, password)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        return new(user.Id, email, password);
    }

    private async Task ChangeUserAsync(string id, Action<ApplicationUser> change)
    {
        await using var db = database.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Id == id);
        change(user);
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string? returnUrl = null, bool remember = false)
    {
        var page = await client.GetStringAsync("/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["RememberMe"] = remember.ToString(),
            ["ReturnUrl"] = returnUrl ?? "", ["__RequestVerificationToken"] = Token(page)
        }));
    }

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Trang cần có antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed record TestUser(string Id, string Email, string Password);
}
