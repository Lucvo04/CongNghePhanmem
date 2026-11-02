using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.IntegrationTests;

public class PasswordChangeTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;

    public async Task InitializeAsync()
    {
        app = new ClubWebFactory(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task AnonymousUsersCannotReadOrSubmitPasswordChange()
    {
        using var client = app.BrowserClient();
        var get = await client.GetAsync("/Account/ChangePassword");
        var post = await client.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent([]));
        foreach (var response in new[] { get, post })
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
        }
    }

    [Fact]
    public async Task FormRequiresAntiforgeryAndIsNotCached()
    {
        var user = await CreateUserAsync();
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        var overview = await client.GetStringAsync("/Account/Overview");
        Assert.Contains("/Account/ChangePassword", overview);
        var form = await client.GetAsync("/Account/ChangePassword");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.True(form.Headers.CacheControl!.NoStore);

        var original = await ReadUserAsync(user.Id);
        var response = await client.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = user.Password, ["NewPassword"] = "New-Password!9", ["ConfirmPassword"] = "New-Password!9"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertPasswordUnchangedAsync(original);
    }

    [Fact]
    public async Task WrongCurrentPasswordDoesNotChangeCredentialsOrEchoInputs()
    {
        var user = await CreateUserAsync();
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        var original = await ReadUserAsync(user.Id);
        const string incorrect = "Wrong-Current!9";
        const string proposed = "New-Proposed!9";
        var response = await ChangeAsync(client, incorrect, proposed, proposed);
        await AssertFormErrorAsync(response, "Mật khẩu hiện tại không đúng.", incorrect, proposed);
        await AssertPasswordUnchangedAsync(original);
        Assert.Equal(1, (await ReadUserAsync(user.Id)).AccessFailedCount);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Overview")).StatusCode);
    }

    [Theory]
    [InlineData("Aa7!short", "ít nhất 10 ký tự")]
    [InlineData("onlylowercase123!", "chữ hoa")]
    [InlineData("ONLYUPPERCASE123!", "chữ thường")]
    [InlineData("LettersOnly!!!!", "chữ số")]
    [InlineData("LettersAndNumbers123", "ký tự đặc biệt")]
    public async Task NewPasswordMustSatisfyPolicyWithoutChangingStoredPassword(string proposed, string expectedError)
    {
        var user = await CreateUserAsync();
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        var original = await ReadUserAsync(user.Id);
        var response = await ChangeAsync(client, user.Password, proposed, proposed);
        await AssertFormErrorAsync(response, expectedError, user.Password, proposed);
        await AssertPasswordUnchangedAsync(original);
    }

    [Theory]
    [InlineData("", "New-Password!9", "New-Password!9", "Vui lòng nhập mật khẩu hiện tại.")]
    [InlineData("current", "", "", "Vui lòng nhập mật khẩu mới.")]
    [InlineData("current", "New-Password!9", "", "Vui lòng nhập lại mật khẩu mới.")]
    [InlineData("current", "New-Password!9", "Different-Password!9", "Mật khẩu xác nhận chưa khớp")]
    public async Task RequiredFieldsAndConfirmationAreCheckedOnServer(string current, string proposed, string confirmation, string expectedError)
    {
        var user = await CreateUserAsync();
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        var original = await ReadUserAsync(user.Id);
        var currentInput = current == "current" ? user.Password : current;
        var response = await ChangeAsync(client, currentInput, proposed, confirmation);
        await AssertFormErrorAsync(response, expectedError, currentInput, proposed, confirmation);
        await AssertPasswordUnchangedAsync(original);
    }

    [Fact]
    public async Task NewPasswordMustDifferFromCurrentPassword()
    {
        var user = await CreateUserAsync();
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        var original = await ReadUserAsync(user.Id);
        var response = await ChangeAsync(client, user.Password, user.Password, user.Password);
        await AssertFormErrorAsync(response, "Mật khẩu mới phải khác mật khẩu hiện tại.", user.Password);
        await AssertPasswordUnchangedAsync(original);
    }

    [Theory]
    [InlineData(SystemRoles.Admin, false)]
    [InlineData(SystemRoles.Student, true)]
    public async Task SuccessRefreshesCurrentSessionAndRevokesOtherSession(string role, bool remember)
    {
        var user = await CreateUserAsync(role);
        using var currentDevice = app.BrowserClient();
        using var otherDevice = app.BrowserClient();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(currentDevice, user.Email, user.Password, remember)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(otherDevice, user.Email, user.Password)).StatusCode);
        var original = await ReadUserAsync(user.Id);
        var staleFormToken = Token(await otherDevice.GetStringAsync("/Account/ChangePassword"));
        var newPassword = $"New-Password!9-{Guid.NewGuid():N}";

        var response = await ChangeAsync(currentDevice, user.Password, newPassword, newPassword);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Overview", response.Headers.Location!.OriginalString);
        var refreshedCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("StudentClub.Auth="));
        Assert.Equal(remember, refreshedCookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        var overview = await currentDevice.GetAsync("/Account/Overview");
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        var overviewHtml = WebUtility.HtmlDecode(await overview.Content.ReadAsStringAsync());
        Assert.Contains("Đã đổi mật khẩu thành công.", overviewHtml);
        Assert.DoesNotContain(newPassword, overviewHtml);
        Assert.DoesNotContain("Đã đổi mật khẩu thành công.", WebUtility.HtmlDecode(await currentDevice.GetStringAsync("/Account/Overview")));
        if (role == SystemRoles.Admin)
        {
            Assert.Equal(HttpStatusCode.OK, (await currentDevice.GetAsync("/Admin")).StatusCode);
        }

        // Một form đang mở ở thiết bị khác không còn được phép ghi sau khi stamp thay đổi.
        var stalePost = await otherDevice.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = user.Password, ["NewPassword"] = "Stale-Password!9", ["ConfirmPassword"] = "Stale-Password!9",
            ["__RequestVerificationToken"] = staleFormToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, stalePost.StatusCode);
        Assert.Contains("/Account/Login", stalePost.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await otherDevice.GetAsync("/Account/Overview")).StatusCode);

        var updated = await ReadUserAsync(user.Id);
        Assert.NotEqual(original.SecurityStamp, updated.SecurityStamp);
        Assert.NotEqual(original.PasswordHash, updated.PasswordHash);
        using var freshDevice = app.BrowserClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(freshDevice, user.Email, user.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(freshDevice, user.Email, newPassword)).StatusCode);
    }

    [Fact]
    public async Task FiveWrongCurrentPasswordsLockAccountAndEndCurrentSession()
    {
        var user = await CreateUserAsync();
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        var original = await ReadUserAsync(user.Id);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await ChangeAsync(client, "Wrong-Current!9", "New-Proposed!9", "New-Proposed!9");
            Assert.Equal(attempt < 5 ? HttpStatusCode.OK : HttpStatusCode.Redirect, response.StatusCode);
        }
        await AssertPasswordUnchangedAsync(original);
        Assert.True((await ReadUserAsync(user.Id)).LockoutEnd > DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Overview")).StatusCode);
        var loginHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Login"));
        Assert.Contains("Tài khoản tạm khóa", loginHtml);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, user.Email, user.Password)).StatusCode);
    }

    [Fact]
    public async Task DisabledAccountCannotSubmitPreviouslyOpenedForm()
    {
        var user = await CreateUserAsync();
        using var client = app.BrowserClient();
        await LoginAsync(client, user.Email, user.Password);
        var token = Token(await client.GetStringAsync("/Account/ChangePassword"));
        var original = await ReadUserAsync(user.Id);
        await using (var db = database.CreateContext())
        {
            var stored = await db.Users.SingleAsync(u => u.Id == user.Id);
            stored.Status = UserStatus.Disabled;
            await db.SaveChangesAsync();
        }
        var response = await client.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = user.Password, ["NewPassword"] = "New-Proposed!9", ["ConfirmPassword"] = "New-Proposed!9",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
        await AssertPasswordUnchangedAsync(original);
    }

    [Fact]
    public async Task SubmittedUserIdCannotChangeAnotherAccountsPassword()
    {
        var owner = await CreateUserAsync();
        var target = await CreateUserAsync();
        var targetOriginal = await ReadUserAsync(target.Id);
        using var client = app.BrowserClient();
        await LoginAsync(client, owner.Email, owner.Password);
        var token = Token(await client.GetStringAsync("/Account/ChangePassword"));
        const string newPassword = "Owner-New-Password!9";
        var response = await client.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = target.Id, ["Email"] = target.Email,
            ["CurrentPassword"] = owner.Password, ["NewPassword"] = newPassword, ["ConfirmPassword"] = newPassword,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await AssertPasswordUnchangedAsync(targetOriginal);
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await manager.CheckPasswordAsync((await manager.FindByIdAsync(owner.Id))!, newPassword));
    }

    private async Task<TestUser> CreateUserAsync(string role = SystemRoles.Student)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"password-{suffix}@example.test";
        var password = $"Initial-Test!9-{Guid.NewGuid():N}";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = "Kiểm thử đổi mật khẩu", StudentCode = suffix[..24] };
        Assert.True((await manager.CreateAsync(user, password)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        return new(user.Id, email, password);
    }

    private async Task<ApplicationUser> ReadUserAsync(string id)
    {
        await using var db = database.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    private async Task AssertPasswordUnchangedAsync(ApplicationUser original)
    {
        var updated = await ReadUserAsync(original.Id);
        Assert.Equal(original.PasswordHash, updated.PasswordHash);
        Assert.Equal(original.SecurityStamp, updated.SecurityStamp);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, bool remember = false)
    {
        var page = await client.GetStringAsync("/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["RememberMe"] = remember.ToString(), ["__RequestVerificationToken"] = Token(page)
        }));
    }

    private static async Task<HttpResponseMessage> ChangeAsync(HttpClient client, string current, string proposed, string confirmation)
    {
        var page = await client.GetStringAsync("/Account/ChangePassword");
        return await client.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = current, ["NewPassword"] = proposed, ["ConfirmPassword"] = confirmation,
            ["__RequestVerificationToken"] = Token(page)
        }));
    }

    private static async Task AssertFormErrorAsync(HttpResponseMessage response, string expectedError, params string[] inputs)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(expectedError, html);
        foreach (var input in inputs.Where(input => !string.IsNullOrEmpty(input)))
        {
            Assert.DoesNotContain(input, html);
        }
    }

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Form cần có antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed record TestUser(string Id, string Email, string Password);
}
