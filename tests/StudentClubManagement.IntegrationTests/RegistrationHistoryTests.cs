using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.IntegrationTests;

public class RegistrationHistoryTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person student = null!, other = null!, manager = null!, admin = null!;
    private int clubId, eventId;

    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        student = await User("Nguyễn Sinh Viên"); other = await User("Người khác");
        manager = await User("Chủ nhiệm"); admin = await User("Quản trị", "system-admin");
        await using var db = database.CreateContext(); database.AssertOwnsContext(db);
        var club = new Club { Code = Guid.NewGuid().ToString("N")[..20], Name = "CLB Lịch sử" };
        db.Memberships.AddRange(new Membership { Club = club, UserId = student.Id },
            new Membership { Club = club, UserId = manager.Id, Role = ClubRole.Manager });
        var item = NewEvent(club, "Workshop <script>unsafe()</script>");
        db.Events.Add(item); await db.SaveChangesAsync(); clubId = club.Id; eventId = item.Id;
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task HistoryPreservesRegisterCancelReregisterAndProtectsOtherPeoplesData()
    {
        var row = await Register();
        await using (var scope = app.Services.CreateAsyncScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<EventRegistrationService>()
                .CancelAsync(row.Id, student.Id, Convert.ToBase64String(row.RowVersion))).Succeeded);
        await Register();
        using var client = await Login(student);
        var response = await client.GetAsync($"/EventRegistrations/History/{row.Id}?UserId={other.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var html = await response.Content.ReadAsStringAsync(); var decoded = WebUtility.HtmlDecode(html);
        Assert.Equal(3, Ids(html, "history").Count);
        Assert.Contains("Đăng ký lại", decoded); Assert.Contains("Hủy đăng ký", decoded); Assert.Contains("Đăng ký tham gia", decoded);
        Assert.Contains("Thực hiện bởi: Bạn", decoded); Assert.Contains("Sinh viên tự hủy đăng ký.", decoded);
        Assert.DoesNotContain("<script>unsafe()</script>", html);
        foreach (var secret in new[] { student.Email, student.Id, manager.Email, "PasswordHash", "SecurityStamp", "BeforeData", "AfterData" })
            Assert.DoesNotContain(secret, html);
        using var outsider = await Login(other); using var leader = await Login(manager); using var administrator = await Login(admin);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/EventRegistrations/History/{row.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await leader.GetAsync($"/EventRegistrations/History/{row.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await administrator.GetAsync($"/EventRegistrations/History/{row.Id}")).StatusCode);
        using var anonymous = app.BrowserClient();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync($"/EventRegistrations/History/{row.Id}")).StatusCode);
    }

    [Fact]
    public async Task FormerMemberKeepsOwnHistoryWithoutInternalDescriptionOrOtherAuditEvents()
    {
        await using (var db = database.CreateContext()) { var e = (await db.Events.FindAsync(eventId))!; e.Audience = EventAudience.Club; e.Description = "PRIVATE-DESCRIPTION"; await db.SaveChangesAsync(); }
        var row = await Register();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await using var db = database.CreateContext();
            var member = await db.Memberships.SingleAsync(m => m.UserId == student.Id && m.ClubId == clubId);
            var result = await scope.ServiceProvider.GetRequiredService<MembershipService>().ChangeStatusAsync(member.Id, manager.Id,
                Convert.ToBase64String(member.RowVersion), MembershipStatus.Left, "Lý do <img src=x onerror=alert(1)>");
            Assert.True(result.Succeeded);
        }
        await using (var db = database.CreateContext())
        {
            (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Archived;
            db.AuditLogs.AddRange(
                new AuditLog { ActorId = student.Id, Action = "OtherAction", EntityType = nameof(EventRegistration), EntityId = row.Id.ToString(), AfterData = "{}", Reason = "PRIVATE-OTHER-ACTION" },
                new AuditLog { ActorId = student.Id, Action = "EventRegistered", EntityType = nameof(Club), EntityId = row.Id.ToString(), AfterData = "{}", Reason = "PRIVATE-OTHER-ENTITY" });
            await db.SaveChangesAsync();
        }
        using var client = await Login(student);
        var html = await client.GetStringAsync($"/EventRegistrations/History/{row.Id}");
        Assert.Equal(2, Ids(html, "history").Count); Assert.Contains("Người quản lý", WebUtility.HtmlDecode(html));
        Assert.Contains("Lý do <img src=x onerror=alert(1)>", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("<img src=x", html);
        foreach (var hidden in new[] { "PRIVATE-DESCRIPTION", "PRIVATE-OTHER-ACTION", "PRIVATE-OTHER-ENTITY", $"href=\"/Events/Details/{eventId}\"" })
            Assert.DoesNotContain(hidden, html);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Events/Details/{eventId}")).StatusCode);
        var mine = await client.GetStringAsync($"/EventRegistrations/Mine?ClubId={clubId}&Status=cancelled");
        Assert.Equal([row.Id], Ids(mine, "registration"));
    }

    [Fact]
    public async Task MineSearchStatusAndDateFilterUseOwnRowsAndVietnamDayBoundaries()
    {
        var day = new DateTimeOffset(2030, 6, 12, 0, 0, 0, TimeSpan.FromHours(7));
        await using var db = database.CreateContext(); var club = (await db.Clubs.FindAsync(clubId))!;
        var ids = new List<int>();
        foreach (var start in new[] { day.AddTicks(-1), day, day.AddDays(1).AddTicks(-1), day.AddDays(1) })
        {
            var e = NewEvent(club, "Boundary workshop"); e.StartsAt = start; e.EndsAt = start.AddHours(1); e.RegistrationDeadline = start.AddDays(-1);
            var r = new EventRegistration { Event = e, UserId = student.Id }; db.EventRegistrations.Add(r); await db.SaveChangesAsync(); ids.Add(r.Id);
        }
        var ownCancelled = new EventRegistration { Event = NewEvent(club, "Cancelled-only"), UserId = student.Id, RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-2), CancelledAt = DateTimeOffset.UtcNow };
        var secretClub = new Club { Code = Guid.NewGuid().ToString("N")[..20], Name = "PRIVATE-CLUB" };
        db.EventRegistrations.AddRange(ownCancelled, new EventRegistration { Event = NewEvent(secretClub, "PRIVATE-EVENT"), UserId = other.Id }); await db.SaveChangesAsync();
        using var client = await Login(student);
        var html = await client.GetStringAsync($"/EventRegistrations/Mine?Search=%20Boundary%20&Status=confirmed&ClubId={clubId}&From=2030-06-12&To=2030-06-12");
        Assert.Equal(ids.Skip(1).Take(2).Order(), Ids(html, "registration").Order());
        Assert.DoesNotContain("PRIVATE-CLUB", html); Assert.DoesNotContain("PRIVATE-EVENT", html);
        Assert.Equal([ownCancelled.Id], Ids(await client.GetStringAsync("/EventRegistrations/Mine?Status=cancelled"), "registration"));
        Assert.Equal(5, Ids(await client.GetStringAsync("/EventRegistrations/Mine?Search=" + Uri.EscapeDataString(club.Name)), "registration").Count);
        Assert.Empty(Ids(await client.GetStringAsync("/EventRegistrations/Mine?Search=NO-MATCH"), "registration"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/EventRegistrations/Mine?ClubId={secretClub.Id}")).StatusCode);
    }

    [Fact]
    public async Task ManagerSearchesOnlyCorrectEventByNameCodeAndRegistrationStatus()
    {
        var row = await Register();
        await using (var db = database.CreateContext())
        {
            db.EventRegistrations.Add(new EventRegistration { EventId = eventId, UserId = other.Id, RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-1), CancelledAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        using var leader = await Login(manager); using var administrator = await Login(admin); using var ordinary = await Login(student);
        var route = $"/EventRegistrations?eventId={eventId}&Status=confirmed&Search={student.Code}";
        var html = await leader.GetStringAsync(route);
        Assert.Equal([row.Id], Ids(html, "registration")); Assert.Contains(student.Code, html); Assert.DoesNotContain(student.Email, html);
        Assert.DoesNotContain("/EventRegistrations/History/", html);
        Assert.Equal([row.Id], Ids(await administrator.GetStringAsync(route), "registration"));
        Assert.Single(Ids(await leader.GetStringAsync($"/EventRegistrations?eventId={eventId}&Status=cancelled&Search={Uri.EscapeDataString("Người khác")}"), "registration"));
        Assert.Equal(HttpStatusCode.NotFound, (await ordinary.GetAsync(route)).StatusCode);
        using var outsider = await Login(other);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(route)).StatusCode);
        await using (var db = database.CreateContext()) { var m = await db.Memberships.SingleAsync(m => m.UserId == manager.Id && m.ClubId == clubId); m.Role = ClubRole.Member; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.NotFound, (await leader.GetAsync(route)).StatusCode);
    }

    [Theory]
    [InlineData("Status=invalid")][InlineData("Status=1")][InlineData("From=bad")][InlineData("From=0001-01-01")]
    [InlineData("To=9999-12-31")][InlineData("From=2030-12-01&To=2030-01-01")][InlineData("Page=bad")][InlineData("ClubId=-1")]
    public async Task InvalidFiltersReturnEditableFormWithoutServerError(string query)
    {
        using var client = await Login(student);
        var result = await client.GetAsync("/EventRegistrations/Mine?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var html = await result.Content.ReadAsStringAsync(); Assert.Contains("validation-summary-errors", html);
        Assert.Contains("name=\"Search\"", html); Assert.Empty(Ids(html, "registration"));
    }

    [Fact]
    public async Task PaginationKeepsFiltersAndUsesStableOrderWithoutDuplicateRows()
    {
        await using var db = database.CreateContext(); var club = (await db.Clubs.FindAsync(clubId))!;
        var registered = DateTimeOffset.UtcNow; var expected = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            var row = new EventRegistration { Event = NewEvent(club, "Paging & workshop " + i), UserId = student.Id, RegisteredAt = registered };
            db.EventRegistrations.Add(row); await db.SaveChangesAsync(); expected.Add(row.Id);
        }
        using var client = await Login(student);
        var route = $"/EventRegistrations/Mine?Status=confirmed&Search=Paging&ClubId={clubId}&From=2026-01-01&To=2099-01-01";
        var first = await client.GetStringAsync(route);
        var next = Regex.Matches(first, "href=\"([^\"]+)\"[^>]*>Trang sau").Single();
        var url = WebUtility.HtmlDecode(next.Groups[1].Value);
        foreach (var value in new[] { "Search=Paging", "Status=confirmed", $"ClubId={clubId}", "From=2026-01-01", "To=2099-01-01", "page=2" }) Assert.Contains(value, url, StringComparison.OrdinalIgnoreCase);
        var second = await client.GetStringAsync(url);
        Assert.Equal(expected.OrderDescending(), Ids(first, "registration").Concat(Ids(second, "registration")));
        Assert.Equal(Ids(second, "registration"), Ids(await client.GetStringAsync(route + "&page=999999"), "registration"));
        Assert.Equal(Ids(first, "registration"), Ids(await client.GetStringAsync(route + "&page=-5"), "registration"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/EventRegistrations/Mine?Search=" + new string('x', 181))).StatusCode);
    }

    [Fact]
    public async Task HistoryPaginatesRealAuditAndRejectsExternalReturnUrl()
    {
        EventRegistration row = null!;
        for (var i = 0; i < 6; i++)
        {
            row = await Register();
            await using var scope = app.Services.CreateAsyncScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<EventRegistrationService>().CancelAsync(row.Id, student.Id, Convert.ToBase64String(row.RowVersion))).Succeeded);
        }
        await using var db = database.CreateContext();
        var audit = await db.AuditLogs.Where(a => a.EntityType == nameof(EventRegistration) && a.EntityId == row.Id.ToString()).ToListAsync();
        var moment = DateTimeOffset.UtcNow; foreach (var a in audit) a.OccurredAt = moment; await db.SaveChangesAsync();
        using var client = await Login(student);
        var back = "/EventRegistrations/Mine?Status=cancelled&Search=Workshop";
        var path = $"/EventRegistrations/History/{row.Id}?returnUrl={Uri.EscapeDataString(back)}";
        var first = await client.GetStringAsync(path); var second = await client.GetStringAsync(path + "&page=2");
        Assert.Equal(audit.Select(a => (int)a.Id).OrderDescending(), Ids(first, "history").Concat(Ids(second, "history")));
        Assert.Contains(back, WebUtility.HtmlDecode(first)); Assert.Contains("returnUrl=", second);
        var external = await client.GetStringAsync($"/EventRegistrations/History/{row.Id}?returnUrl=https://outside.example.test/");
        Assert.DoesNotContain("outside.example.test", external); Assert.Contains("href=\"/EventRegistrations/Mine\"", external);
    }

    [Fact]
    public async Task MissingAuditIsNotFabricatedAndDisabledStudentCannotViewHistory()
    {
        await using var db = database.CreateContext();
        var row = new EventRegistration { UserId = student.Id, EventId = eventId }; db.EventRegistrations.Add(row); await db.SaveChangesAsync();
        using var client = await Login(student);
        var html = await client.GetStringAsync($"/EventRegistrations/History/{row.Id}");
        Assert.Empty(Ids(html, "history")); Assert.Contains("Chưa có nhật ký", WebUtility.HtmlDecode(html));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/EventRegistrations/History/2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/EventRegistrations/History/{row.Id}?page=bad")).StatusCode);
        (await db.Users.FindAsync(student.Id))!.Status = UserStatus.Disabled; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/EventRegistrations/History/{row.Id}")).StatusCode);
    }

    private static ClubEvent NewEvent(Club club, string title) => new() { Club = club, Title = title, Location = "Hội trường", StartsAt = DateTimeOffset.UtcNow.AddDays(5), EndsAt = DateTimeOffset.UtcNow.AddDays(5).AddHours(2), RegistrationDeadline = DateTimeOffset.UtcNow.AddDays(3), Capacity = 30, Status = EventStatus.Published, PublishedAt = DateTimeOffset.UtcNow };
    private async Task<EventRegistration> Register()
    {
        await using var db = database.CreateContext(); var e = (await db.Events.FindAsync(eventId))!;
        var existing = await db.EventRegistrations.AsNoTracking().SingleOrDefaultAsync(r => r.EventId == eventId && r.UserId == student.Id);
        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<EventRegistrationService>().RegisterAsync(eventId, student.Id,
            new EventRegistrationForm { EventVersion = Convert.ToBase64String(e.RowVersion), RowVersion = existing is null ? null : Convert.ToBase64String(existing.RowVersion) });
        Assert.True(result.Succeeded);
        return await db.EventRegistrations.AsNoTracking().SingleAsync(r => r.Id == result.Id);
    }
    private async Task<Person> User(string name, string role = "system-student")
    {
        await using var scope = app.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tag = Guid.NewGuid().ToString("N"); var email = $"history-{tag}@example.test"; var password = $"Test-Only!9-{tag}";
        var user = new ApplicationUser { Email = email, FullName = name, StudentCode = tag[..24], RoleId = role };
        Assert.True((await users.CreateAsync(user, password)).Succeeded); return new(user.Id, email, password, user.StudentCode);
    }
    private async Task<HttpClient> Login(Person person)
    {
        var client = app.BrowserClient(); var html = await client.GetStringAsync("/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = person.Email, ["Password"] = person.Password, ["__RequestVerificationToken"] = token }))).StatusCode);
        return client;
    }
    private static List<int> Ids(string html, string kind) => Regex.Matches(html, $"data-{kind}-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToList();
    private sealed record Person(string Id, string Email, string Password, string Code);
}
