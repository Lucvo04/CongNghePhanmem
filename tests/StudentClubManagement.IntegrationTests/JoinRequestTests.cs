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

public class JoinRequestTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private TestUser student = null!;
    private int clubId;
    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        student = await CreateUserAsync();
        await using var db = database.CreateContext();
        var club = new Club { Code = "JOIN-" + Guid.NewGuid().ToString("N")[..15], Name = "CLB thử đăng ký" };
        db.Clubs.Add(club); await db.SaveChangesAsync(); clubId = club.Id;
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Theory]
    [InlineData("Guest")]
    [InlineData("Admin")]
    [InlineData("Both")]
    public async Task OnlyStudentsWithoutAdminRoleCanUseJoinRequestEndpoints(string role)
    {
        using var client = role == "Guest" ? app.BrowserClient() : await LoginAsync(await CreateUserAsync(role));
        foreach (var path in new[] { "/JoinRequests", $"/JoinRequests/Create?clubId={clubId}", "/JoinRequests/Withdraw/1" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(role == "Guest" ? "/Account/Login" : "/Account/AccessDenied", response.Headers.Location!.OriginalString);
        }
        var tokenPage = await client.GetStringAsync(role == "Guest" ? "/Account/Login" : "/Account/Overview");
        var denied = await client.PostAsync("/JoinRequests/Create", Fields(Token(tokenPage), "Valid reason"));
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        await using var db = database.CreateContext();
        Assert.False(await db.JoinRequests.AnyAsync(r => r.ClubId == clubId));
    }

    [Fact]
    public async Task SubmitUsesAuthenticatedOwnerAndCreatesPendingAuditWithoutMembership()
    {
        using var client = await LoginAsync(student);
        var victim = await CreateUserAsync();
        var page = await client.GetAsync($"/JoinRequests/Create?clubId={clubId}");
        Assert.True(page.Headers.CacheControl!.NoStore);
        var fields = FormFields(Token(await page.Content.ReadAsStringAsync()), "  Tôi muốn học C#  ");
        fields["UserId"] = victim.Id; fields["Status"] = "2"; fields["ClosedAt"] = DateTimeOffset.UtcNow.ToString("O");
        var response = await client.PostAsync("/JoinRequests/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/JoinRequests", response.Headers.Location!.OriginalString);
        await using var db = database.CreateContext();
        var request = await db.JoinRequests.SingleAsync(r => r.ClubId == clubId);
        Assert.Equal(student.Id, request.UserId); Assert.Equal("Tôi muốn học C#", request.Reason);
        Assert.Equal(JoinRequestStatus.Pending, request.Status); Assert.Null(request.ClosedAt);
        Assert.False(await db.Memberships.AnyAsync(m => m.ClubId == clubId));
        var audit = await db.AuditLogs.SingleAsync(a => a.ActorId == student.Id && a.Action == "JoinRequestSubmitted");
        Assert.DoesNotContain(request.Reason, audit.AfterData);
        var history = await client.GetAsync("/JoinRequests"); Assert.True(history.Headers.CacheControl!.NoStore);
        Assert.Contains("Chờ xử lý", WebUtility.HtmlDecode(await history.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1001)]
    public async Task EmptyWhitespaceAndTooLongReasonsDoNotCreateRequests(int size)
    {
        using var client = await LoginAsync(student);
        var reason = size == 1 ? "   " : new string('x', size);
        var response = await PostAsync(client, reason);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = database.CreateContext(); Assert.False(await db.JoinRequests.AnyAsync(r => r.ClubId == clubId));
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal("Validation", (await scope.ServiceProvider.GetRequiredService<JoinRequestService>().SubmitAsync(clubId, student.Id, reason)).Code);
    }

    [Fact]
    public async Task DuplicateSubmitIsConflictAndDoesNotRepeatAudit()
    {
        using var client = await LoginAsync(student);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client)).StatusCode);
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.JoinRequests.CountAsync(r => r.ClubId == clubId));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.ActorId == student.Id && a.Action == "JoinRequestSubmitted"));
    }

    [Theory]
    [InlineData(ClubStatus.Suspended, HttpStatusCode.Conflict)]
    [InlineData(ClubStatus.Archived, HttpStatusCode.NotFound)]
    public async Task ClubStatusChangedAfterFormOpenedIsCheckedAgain(ClubStatus status, HttpStatusCode expected)
    {
        using var client = await LoginAsync(student);
        var token = Token(await client.GetStringAsync($"/JoinRequests/Create?clubId={clubId}"));
        await using (var db = database.CreateContext()) { (await db.Clubs.FindAsync(clubId))!.Status = status; await db.SaveChangesAsync(); }
        Assert.Equal(expected, (await client.PostAsync("/JoinRequests/Create", Fields(token, "Valid reason"))).StatusCode);
        await using var after = database.CreateContext(); Assert.False(await after.JoinRequests.AnyAsync(r => r.ClubId == clubId));
    }

    [Theory]
    [InlineData(MembershipStatus.Active, HttpStatusCode.Conflict)]
    [InlineData(MembershipStatus.Suspended, HttpStatusCode.Conflict)]
    [InlineData(MembershipStatus.Left, HttpStatusCode.Redirect)]
    public async Task OpenMembershipBlocksReapplicationButEndedMembershipDoesNot(MembershipStatus status, HttpStatusCode expected)
    {
        await using (var db = database.CreateContext())
        {
            db.Memberships.Add(new Membership { ClubId = clubId, UserId = student.Id, Status = status,
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-2), EndedAt = status == MembershipStatus.Left ? DateTimeOffset.UtcNow.AddDays(-1) : null });
            await db.SaveChangesAsync();
        }
        using var client = await LoginAsync(student);
        Assert.Equal(expected, (await PostAsync(client)).StatusCode);
    }

    [Fact]
    public async Task CsrfIsRequiredAndWithdrawalGetDoesNotMutateData()
    {
        using var client = await LoginAsync(student);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/JoinRequests/Create", new FormUrlEncodedContent(new Dictionary<string,string> { ["ClubId"] = clubId.ToString(), ["Reason"] = "Test" }))).StatusCode);
        await PostAsync(client);
        var request = await OwnRequestAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/JoinRequests/Withdraw/{request.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/JoinRequests/Withdraw/{request.Id}", new FormUrlEncodedContent(new Dictionary<string,string> { ["RowVersion"] = Convert.ToBase64String(request.RowVersion) }))).StatusCode);
        Assert.Equal(JoinRequestStatus.Pending, (await OwnRequestAsync()).Status);
    }

    [Fact]
    public async Task WithdrawalKeepsHistoryAndReapplicationCreatesNewRow()
    {
        using var client = await LoginAsync(student); await PostAsync(client);
        var request = await OwnRequestAsync(); var fields = await WithdrawFieldsAsync(client, request.Id);
        var withdrawn = await client.PostAsync($"/JoinRequests/Withdraw/{request.Id}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, withdrawn.StatusCode);
        var old = await OwnRequestAsync(); Assert.Equal(JoinRequestStatus.Withdrawn, old.Status); Assert.NotNull(old.ClosedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/JoinRequests/Withdraw/{request.Id}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "Nộp lại đơn mới")).StatusCode);
        await using var db = database.CreateContext();
        Assert.Equal(2, await db.JoinRequests.CountAsync(r => r.ClubId == clubId));
        Assert.Equal(1, await db.JoinRequests.CountAsync(r => r.ClubId == clubId && r.Status == JoinRequestStatus.Pending));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.ActorId == student.Id && a.Action == "JoinRequestWithdrawn"));
    }

    [Fact]
    public async Task StudentCannotSeeOrWithdrawAnothersRequestAndHtmlIsEncoded()
    {
        const string reason = "<script>alert('private-join-reason')</script>";
        using var owner = await LoginAsync(student); await PostAsync(owner, reason);
        var request = await OwnRequestAsync();
        var ownHtml = await owner.GetStringAsync("/JoinRequests"); Assert.DoesNotContain(reason, ownHtml); Assert.Contains("&lt;script&gt;", ownHtml);
        using var other = await LoginAsync(await CreateUserAsync());
        var otherHtml = await other.GetStringAsync("/JoinRequests"); Assert.DoesNotContain("private-join-reason", otherHtml);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/JoinRequests/Withdraw/{request.Id}")).StatusCode);
        var fields = new Dictionary<string,string> { ["__RequestVerificationToken"] = Token(otherHtml), ["RowVersion"] = Convert.ToBase64String(request.RowVersion), ["UserId"] = student.Id };
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/JoinRequests/Withdraw/{request.Id}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(JoinRequestStatus.Pending, (await OwnRequestAsync()).Status);
    }

    [Fact]
    public async Task ChangedVersionRequiresReloadBeforeWithdrawal()
    {
        using var client = await LoginAsync(student); await PostAsync(client);
        var request = await OwnRequestAsync(); var fields = await WithdrawFieldsAsync(client, request.Id);
        await using (var db = database.CreateContext()) { (await db.JoinRequests.FindAsync(request.Id))!.Reason = "Changed by another operation"; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/JoinRequests/Withdraw/{request.Id}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(JoinRequestStatus.Pending, (await OwnRequestAsync()).Status);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("RoleRemoved")]
    [InlineData("AdminAdded")]
    public async Task AccountOrRoleChangesBlockPreviouslyOpenedForm(string change)
    {
        using var client = await LoginAsync(student);
        var fields = FormFields(Token(await client.GetStringAsync($"/JoinRequests/Create?clubId={clubId}")), "Valid reason");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(student.Id))!;
            if (change == "Disabled") { user.Status = UserStatus.Disabled; await users.UpdateAsync(user); }
            else if (change == "RoleRemoved") await users.RemoveFromRoleAsync(user, SystemRoles.Student);
            else { await users.RemoveFromRoleAsync(user, SystemRoles.Student); await users.AddToRoleAsync(user, SystemRoles.Admin); }
            Assert.Equal("Forbidden", (await scope.ServiceProvider.GetRequiredService<JoinRequestService>().SubmitAsync(clubId, student.Id, "Reason")).Code);
        }
        var response = await client.PostAsync("/JoinRequests/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = database.CreateContext(); Assert.False(await db.JoinRequests.AnyAsync(r => r.ClubId == clubId));
    }

    [Fact]
    public async Task ConcurrentSubmissionsAndWithdrawalsHaveOnlyOneWinner()
    {
        async Task<JoinRequestResult> Submit() { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<JoinRequestService>().SubmitAsync(clubId, student.Id, "Concurrent reason"); }
        var results = await Task.WhenAll(Submit(), Submit());
        Assert.Single(results, r => r.Succeeded); Assert.Single(results, r => r.Code == "Conflict");
        var request = await OwnRequestAsync();
        async Task<JoinRequestResult> Withdraw() { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<JoinRequestService>().WithdrawAsync(request.Id, student.Id, Convert.ToBase64String(request.RowVersion)); }
        results = await Task.WhenAll(Withdraw(), Withdraw());
        Assert.Single(results, r => r.Succeeded); Assert.Single(results, r => r.Code == "Conflict");
    }

    [Fact]
    public async Task SqlConstraintRejectsDuplicatePendingAndInvalidPeriod()
    {
        using var client = await LoginAsync(student); await PostAsync(client);
        await using var db = database.CreateContext();
        db.JoinRequests.Add(new JoinRequest { UserId = student.Id, ClubId = clubId, Reason = "Duplicate" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.JoinRequests.Add(new JoinRequest { UserId = student.Id, ClubId = clubId, Reason = "Invalid period", Status = JoinRequestStatus.Withdrawn });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task OwnHistoryIsPaginatedAndPendingRequestCanBeWithdrawnAfterClubArchived()
    {
        using var client = await LoginAsync(student); await PostAsync(client);
        var request = await OwnRequestAsync();
        await using (var db = database.CreateContext())
        {
            for (var i = 0; i < 11; i++) db.JoinRequests.Add(new JoinRequest { ClubId = clubId, UserId = student.Id, Reason = $"Old request {i}", Status = JoinRequestStatus.Withdrawn, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-2), ClosedAt = DateTimeOffset.UtcNow.AddDays(-1) });
            (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Archived;
            await db.SaveChangesAsync();
        }
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/JoinRequests")); Assert.Contains("12 đơn", html); Assert.Contains("Trang 1 / 2", html);
        Assert.DoesNotContain($"href=\"/Clubs/Details/{clubId}\"", html);
        Assert.Contains("Trang 2 / 2", WebUtility.HtmlDecode(await client.GetStringAsync("/JoinRequests?page=2147483647")));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/JoinRequests?page=abc")).StatusCode);
        var fields = await WithdrawFieldsAsync(client, request.Id);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/JoinRequests/Withdraw/{request.Id}", new FormUrlEncodedContent(fields))).StatusCode);
    }

    [Fact]
    public async Task AuditFailureRollsBackSubmissionAndWithdrawal()
    {
        const string trigger = "CREATE TRIGGER RejectJoinAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action LIKE 'JoinRequest%') THROW 51001, 'test audit failure', 1; END";
        await using var db = database.CreateContext();
        await db.Database.ExecuteSqlRawAsync(trigger);
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<JoinRequestService>().SubmitAsync(clubId, student.Id, "Rollback"));
            Assert.False(await db.JoinRequests.AnyAsync(r => r.ClubId == clubId));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectJoinAudit"); }
        using var client = await LoginAsync(student); await PostAsync(client); var request = await OwnRequestAsync();
        await db.Database.ExecuteSqlRawAsync(trigger);
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<JoinRequestService>().WithdrawAsync(request.Id, student.Id, Convert.ToBase64String(request.RowVersion)));
            var unchanged = await OwnRequestAsync(); Assert.Equal(JoinRequestStatus.Pending, unchanged.Status); Assert.Null(unchanged.ClosedAt);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectJoinAudit"); }
    }

    private async Task<TestUser> CreateUserAsync(string role = "Student")
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tag = Guid.NewGuid().ToString("N"); var email = $"join-{tag}@example.test"; var password = $"Test-Only!9-{tag}";
        var user = new ApplicationUser { Email = email, UserName = email, FullName = "Sinh viên thử đăng ký", StudentCode = tag[..24] };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role is "Admin" or "Both" ? SystemRoles.Admin : SystemRoles.Student)).Succeeded);

        return new(user.Id, email, password);
    }
    private async Task<HttpClient> LoginAsync(TestUser user)
    {
        var client = app.BrowserClient(); var token = Token(await client.GetStringAsync("/Account/Login"));
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["Email"] = user.Email, ["Password"] = user.Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode); return client;
    }
    private async Task<HttpResponseMessage> PostAsync(HttpClient client, string reason = "Muốn học hỏi cùng CLB")
        => await client.PostAsync("/JoinRequests/Create", Fields(Token(await client.GetStringAsync($"/JoinRequests/Create?clubId={clubId}")), reason));
    private Dictionary<string,string> FormFields(string token, string reason) => new() { ["__RequestVerificationToken"] = token, ["ClubId"] = clubId.ToString(), ["Reason"] = reason };
    private FormUrlEncodedContent Fields(string token, string reason) => new(FormFields(token, reason));
    private async Task<JoinRequest> OwnRequestAsync() { await using var db = database.CreateContext(); return await db.JoinRequests.AsNoTracking().SingleAsync(r => r.ClubId == clubId && r.UserId == student.Id); }
    private static async Task<Dictionary<string,string>> WithdrawFieldsAsync(HttpClient client, int id)
    {
        var html = await client.GetStringAsync($"/JoinRequests/Withdraw/{id}");
        return new() { ["__RequestVerificationToken"] = Token(html), ["RowVersion"] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"RowVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value) };
    }
    private static string Token(string html)
    {
        var value = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(value); return WebUtility.HtmlDecode(value);
    }
    private sealed record TestUser(string Id, string Email, string Password);
}