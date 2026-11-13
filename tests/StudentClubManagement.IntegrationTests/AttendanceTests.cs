using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.IntegrationTests;

public class AttendanceTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person student = null!, manager = null!, admin = null!, other = null!;
    private int clubId, eventId, registrationId;

    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        student = await User("Sinh viên <script>x</script>"); manager = await User("Chủ nhiệm");
        admin = await User("Quản trị", "system-admin"); other = await User("Chủ nhiệm CLB khác");
        await using var db = database.CreateContext(); database.AssertOwnsContext(db);
        var club = new Club { Code = Guid.NewGuid().ToString("N")[..20], Name = "CLB Điểm danh" };
        var foreignClub = new Club { Code = Guid.NewGuid().ToString("N")[..20], Name = "CLB khác" };
        db.Memberships.AddRange(new Membership { Club = club, UserId = manager.Id, Role = ClubRole.Manager },
            new Membership { Club = club, UserId = student.Id }, new Membership { Club = foreignClub, UserId = other.Id, Role = ClubRole.Manager });
        var item = new ClubEvent { Club = club, Title = "Workshop điểm danh", Location = "Hội trường",
            StartsAt = DateTimeOffset.UtcNow.AddHours(-1), EndsAt = DateTimeOffset.UtcNow.AddHours(1),
            RegistrationDeadline = DateTimeOffset.UtcNow.AddHours(-2), PublishedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Status = EventStatus.Published, Capacity = 100, Audience = EventAudience.Club };
        var row = new EventRegistration { Event = item, UserId = student.Id, RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1) };
        db.EventRegistrations.Add(row); await db.SaveChangesAsync(); clubId = club.Id; eventId = item.Id; registrationId = row.Id;
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task HttpMarkingUsesServerActorTimeAndProtectsRosterAndAntiforgery()
    {
        using var leader = await Login(manager);
        var response = await leader.GetAsync($"/Attendances?eventId={eventId}"); Assert.True(response.Headers.CacheControl!.NoStore);
        var html = await response.Content.ReadAsStringAsync(); Assert.Contains("Chưa điểm danh", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("<script>x</script>", html); Assert.DoesNotContain(student.Email, html);
        var form = new Dictionary<string,string> { ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken"),
            ["EventVersion"] = Hidden(html, "EventVersion"), ["RegistrationVersion"] = Hidden(html, "RegistrationVersion"),
            ["Status"] = "1", ["MarkedById"] = other.Id, ["MarkedAt"] = "2000-01-01", ["UserId"] = other.Id,
            ["returnUrl"] = "https://outside.example.test/", ["Reason"] = "Có mặt tại hội trường" };
        var posted = await leader.PostAsync($"/Attendances/Save/{registrationId}", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode); Assert.DoesNotContain("outside.example", posted.Headers.Location!.ToString());
        await using var db = database.CreateContext(); var attendance = await db.Attendances.SingleAsync(a => a.RegistrationId == registrationId);
        Assert.Equal(AttendanceStatus.Present, attendance.Status); Assert.Equal(manager.Id, attendance.MarkedById);
        Assert.True(attendance.MarkedAt > DateTimeOffset.UtcNow.AddMinutes(-2));
        var audit = await db.AuditLogs.SingleAsync(a => a.EntityType == nameof(Attendance) && a.EntityId == registrationId.ToString());
        Assert.Equal("Có mặt tại hội trường", audit.Reason); Assert.Equal(manager.Id, audit.ActorId); Assert.Contains("Present", audit.AfterData);
        Assert.Equal(HttpStatusCode.BadRequest, (await leader.PostAsync($"/Attendances/Save/{registrationId}", new FormUrlEncodedContent([]))).StatusCode);
        form.Remove("Status"); Assert.Equal(HttpStatusCode.BadRequest, (await leader.PostAsync($"/Attendances/Save/{registrationId}", new FormUrlEncodedContent(form))).StatusCode);
        using var ordinary = await Login(student); using var outsider = await Login(other); using var anonymous = app.BrowserClient();
        Assert.Equal(HttpStatusCode.NotFound, (await ordinary.GetAsync($"/Attendances?eventId={eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/Attendances?eventId={eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync($"/Attendances?eventId={eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.PostAsync($"/Attendances/Save/{registrationId}",new FormUrlEncodedContent([]))).StatusCode);
    }

    [Fact]
    public async Task StatusChangesAndResetReuseOneRowAndKeepAudit()
    {
        foreach (var status in new[] { AttendanceStatus.Present, AttendanceStatus.Absent, AttendanceStatus.Excused, AttendanceStatus.Unmarked })
        {
            var form = await Form(status); form.Reason = new string('x', 500);
            Assert.True((await Save(manager.Id, form)).Succeeded);
        }
        await using var db = database.CreateContext(); var row = await db.Attendances.SingleAsync(a => a.RegistrationId == registrationId);
        Assert.Equal(AttendanceStatus.Unmarked, row.Status);
        Assert.Equal(4, await db.AuditLogs.CountAsync(a => a.EntityType == nameof(Attendance) && a.EntityId == registrationId.ToString()));
        Assert.True((await Save(manager.Id, await Form(AttendanceStatus.Unmarked))).Succeeded);
        Assert.Equal(4, await db.AuditLogs.CountAsync(a => a.EntityType == nameof(Attendance) && a.EntityId == registrationId.ToString()));
        using var leader = await Login(manager);
        Assert.Contains($"data-attendance-id=\"{registrationId}\"", await leader.GetStringAsync($"/Attendances?eventId={eventId}&status=0"));
        Assert.DoesNotContain("data-attendance-id", await leader.GetStringAsync($"/Attendances?eventId={eventId}&status=2"));
    }

    [Theory]
    [InlineData("Future")][InlineData("Draft")][InlineData("CancelledEvent")][InlineData("ArchivedClub")]
    [InlineData("CancelledRegistration")][InlineData("LateRegistration")][InlineData("DisabledManager")]
    [InlineData("RemovedRole")][InlineData("SuspendedManager")][InlineData("WrongClub")][InlineData("InvalidStatus")][InlineData("LongReason")]
    public async Task IneligibleWritesAreRejectedWithoutAttendanceOrAudit(string condition)
    {
        await using (var db = database.CreateContext())
        {
            var e = (await db.Events.FindAsync(eventId))!; var r = (await db.EventRegistrations.FindAsync(registrationId))!;
            if (condition == "Future") { e.StartsAt = DateTimeOffset.UtcNow.AddDays(1); e.EndsAt = DateTimeOffset.UtcNow.AddDays(2); }
            if (condition == "Draft") { e.Status = EventStatus.Draft; e.PublishedAt = null; }
            if (condition == "CancelledEvent") e.Status = EventStatus.Cancelled;
            if (condition == "ArchivedClub") (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Archived;
            if (condition == "CancelledRegistration") r.CancelledAt = DateTimeOffset.UtcNow.AddHours(-3);
            if (condition == "LateRegistration") r.RegisteredAt = DateTimeOffset.UtcNow;
            if (condition == "DisabledManager") (await db.Users.FindAsync(manager.Id))!.Status = UserStatus.Disabled;
            if (condition == "RemovedRole") (await db.Users.FindAsync(manager.Id))!.RoleId = null;
            if (condition == "SuspendedManager")
            {
                var member = await db.Memberships.SingleAsync(m => m.UserId == manager.Id && m.ClubId == clubId);
                member.Role = ClubRole.Member; member.Status = MembershipStatus.Suspended;
            }
            await db.SaveChangesAsync();
        }
        var form = await Form(); if (condition == "InvalidStatus") form.Status = (AttendanceStatus)99;
        if (condition == "LongReason") form.Reason = new string('x', 501);
        Assert.False((await Save(condition == "WrongClub" ? other.Id : manager.Id, form)).Succeeded);
        await using var verify = database.CreateContext(); Assert.False(await verify.Attendances.AnyAsync(a => a.RegistrationId == registrationId));
        Assert.False(await verify.AuditLogs.AnyAsync(a => a.EntityType == nameof(Attendance) && a.EntityId == registrationId.ToString()));
    }

    [Theory]
    [InlineData("Event")][InlineData("Registration")][InlineData("Attendance")]
    public async Task StaleFormsNeverOverwriteNewerData(string changed)
    {
        var form = await Form();
        if (changed == "Attendance") Assert.True((await Save(admin.Id, await Form(AttendanceStatus.Excused))).Succeeded);
        else
        {
            await using var db = database.CreateContext();
            if (changed == "Event") (await db.Events.FindAsync(eventId))!.Location = "Phòng khác";
            else (await db.EventRegistrations.FindAsync(registrationId))!.RegisteredAt = DateTimeOffset.UtcNow.AddDays(-2);
            await db.SaveChangesAsync();
        }
        Assert.Equal("Conflict", (await Save(manager.Id, form)).Code);
        await using var verify = database.CreateContext();
        if (changed == "Attendance") Assert.Equal(AttendanceStatus.Excused, (await verify.Attendances.FindAsync(registrationId))!.Status);
        else Assert.Null(await verify.Attendances.FindAsync(registrationId));
    }

    [Fact]
    public async Task ConcurrentFirstMarksOnlyCreateOneResultAndOneAudit()
    {
        var one = await Form(); var two = await Form(AttendanceStatus.Absent);
        var results = await Task.WhenAll(Save(manager.Id, one), Save(admin.Id, two));
        Assert.Single(results, r => r.Succeeded); Assert.Single(results, r => r.Code == "Conflict");
        await using var db = database.CreateContext(); Assert.Equal(1, await db.Attendances.CountAsync(a => a.RegistrationId == registrationId));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityType == nameof(Attendance) && a.EntityId == registrationId.ToString()));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task AuditFailureRollsBackInitialAndUpdatedResults(bool update)
    {
        if (update) Assert.True((await Save(manager.Id, await Form())).Succeeded);
        await using var db = database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectAttendanceAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityType='Attendance') THROW 51001,'Test audit failure',1; END");
        try
        {
            var form = await Form(AttendanceStatus.Excused);
            await Assert.ThrowsAsync<DbUpdateException>(() => Save(admin.Id, form));
            var actual = await db.Attendances.AsNoTracking().SingleOrDefaultAsync(a => a.RegistrationId == registrationId);
            if (update) Assert.Equal(AttendanceStatus.Present, actual!.Status); else Assert.Null(actual);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectAttendanceAudit"); }
    }

    [Fact]
    public async Task DepartedOrDisabledStudentRemainsInStartedRosterAndCompletedEventCanBeMarked()
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            await using var db = database.CreateContext();
            var member = await db.Memberships.SingleAsync(m => m.UserId == student.Id && m.ClubId == clubId);
            Assert.True((await scope.ServiceProvider.GetRequiredService<MembershipService>().ChangeStatusAsync(member.Id, manager.Id,
                Convert.ToBase64String(member.RowVersion), MembershipStatus.Left, "Đã rời CLB")).Succeeded);
        }
        await using (var db = database.CreateContext())
        {
            (await db.Users.FindAsync(student.Id))!.Status = UserStatus.Disabled;
            (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Suspended;
            var e = (await db.Events.FindAsync(eventId))!; e.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1); e.Status = EventStatus.Completed;
            await db.SaveChangesAsync();
        }
        Assert.True((await Save(admin.Id, await Form())).Succeeded);
        await using var verify = database.CreateContext(); Assert.Null((await verify.EventRegistrations.FindAsync(registrationId))!.CancelledAt);
        using var leader = await Login(manager); Assert.Contains(student.Code, await leader.GetStringAsync($"/Attendances?eventId={eventId}"));
    }

    [Fact]
    public async Task EventCancellationPreservesRecordedResultsAndBlocksAllFurtherWrites()
    {
        Assert.True((await Save(manager.Id, await Form())).Succeeded);
        var stale = await Form(AttendanceStatus.Absent);
        Assert.True((await CancelEvent()).Succeeded);
        Assert.False((await Save(manager.Id, stale)).Succeeded);
        using var leader = await Login(manager); var html = await leader.GetStringAsync($"/Attendances?eventId={eventId}");
        Assert.Contains(student.Code, html); Assert.Contains("Có mặt", WebUtility.HtmlDecode(html)); Assert.DoesNotContain("/Attendances/Save", html);
        await using var db = database.CreateContext(); Assert.Equal(AttendanceStatus.Present, (await db.Attendances.FindAsync(registrationId))!.Status);
    }

    [Fact]
    public async Task CancellationRaceCannotMarkAfterCancellation()
    {
        var form = await Form(); var mark = Save(manager.Id, form); var cancel = CancelEvent();
        await Task.WhenAll(mark, cancel); Assert.True((await cancel).Succeeded);
        Assert.False((await Save(admin.Id, await Form(AttendanceStatus.Absent))).Succeeded);
        await using var db = database.CreateContext();
        Assert.Equal((await mark).Succeeded ? 1 : 0, await db.Attendances.CountAsync(a => a.RegistrationId == registrationId));
    }

    [Fact]
    public async Task SearchFilterPaginationExcludeCancelledAndLateRegistrations()
    {
        var expected = new List<int> { registrationId };
        await using (var db = database.CreateContext())
        {
            for (var i = 0; i < 12; i++)
            {
                var person = await User("Sinh viên thêm");
                var row = new EventRegistration { EventId = eventId, UserId = person.Id, RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1) };
                if (i == 10) row.CancelledAt = DateTimeOffset.UtcNow.AddHours(-3);
                if (i == 11) row.RegisteredAt = DateTimeOffset.UtcNow;
                db.EventRegistrations.Add(row); await db.SaveChangesAsync(); if (i < 10) expected.Add(row.Id);
            }
        }
        using var leader = await Login(manager);
        var first = await leader.GetStringAsync($"/Attendances?eventId={eventId}&status=0&search={Uri.EscapeDataString("Sinh viên")}");
        var next = WebUtility.HtmlDecode(Regex.Match(first, "href=\"([^\"]+)\"[^>]*>Trang sau").Groups[1].Value);
        Assert.Contains("status=Unmarked", next, StringComparison.OrdinalIgnoreCase); Assert.Contains("search=", next);
        var second = await leader.GetStringAsync(next);
        var found = Regex.Matches(first + second, "data-attendance-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToArray();
        Assert.Equal(expected.Order(), found.Order());
        Assert.Single(Regex.Matches(await leader.GetStringAsync($"/Attendances?eventId={eventId}&search={student.Code}"), "data-attendance-id="));
        Assert.Equal(HttpStatusCode.BadRequest, (await leader.GetAsync($"/Attendances?eventId={eventId}&status=99")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await leader.GetAsync($"/Attendances?eventId={eventId}&page=bad")).StatusCode);
    }

    [Fact]
    public async Task DatabaseGuardsStatusUniquenessForeignKeysAndDestructiveRollback()
    {
        Assert.True((await Save(manager.Id, await Form())).Succeeded);
        await using var db = database.CreateContext();
        var columns = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id=OBJECT_ID('Attendances')").SingleAsync(); Assert.Equal(6, columns);
        db.Attendances.Add(new Attendance { RegistrationId = registrationId, Status = AttendanceStatus.Present, MarkedById = manager.Id, MarkedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var row = (await db.Attendances.FindAsync(registrationId))!; row.Status = (AttendanceStatus)99;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        (await db.Attendances.FindAsync(registrationId))!.MarkedById = "missing-user";
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var error = await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync("20261110043425_AddEventRegistrations"));
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Equal(51000, error.Number); Assert.True(await db.Attendances.AnyAsync(a => a.RegistrationId == registrationId));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private async Task<AttendanceForm> Form(AttendanceStatus status = AttendanceStatus.Present)
    {
        await using var db = database.CreateContext(); var row = await db.EventRegistrations.AsNoTracking().Include(r => r.Event).Include(r => r.Attendance).SingleAsync(r => r.Id == registrationId);
        return new() { Status = status, EventVersion = Convert.ToBase64String(row.Event.RowVersion), RegistrationVersion = Convert.ToBase64String(row.RowVersion), RowVersion = row.Attendance is null ? null : Convert.ToBase64String(row.Attendance.RowVersion) };
    }
    private async Task<WorkflowResult> Save(string actor, AttendanceForm form) { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<AttendanceService>().SaveAsync(registrationId, actor, form); }
    private async Task<EventResult> CancelEvent() { await using var db = database.CreateContext(); var e = (await db.Events.FindAsync(eventId))!; await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<EventService>().TransitionAsync(eventId, admin.Id, new() { Operation = "Cancel", Reason = "Hủy theo kế hoạch", RowVersion = Convert.ToBase64String(e.RowVersion) }); }
    private async Task<Person> User(string name, string role = "system-student") { await using var scope = app.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var tag = Guid.NewGuid().ToString("N"); var password = $"Test-Only!9-{tag}"; var user = new ApplicationUser { Email = $"attendance-{tag}@example.test", FullName = name, StudentCode = tag[..24], RoleId = role }; Assert.True((await users.CreateAsync(user,password)).Succeeded); return new(user.Id,user.Email,password,user.StudentCode); }
    private async Task<HttpClient> Login(Person person) { var client = app.BrowserClient(); var html = await client.GetStringAsync("/Account/Login"); Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["Email"]=person.Email,["Password"]=person.Password,["__RequestVerificationToken"]=Hidden(html,"__RequestVerificationToken")}))).StatusCode);return client; }
    private static string Hidden(string html,string name) { var match=Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]*)\""); Assert.True(match.Success,"Missing "+name);return WebUtility.HtmlDecode(match.Groups[1].Value); }
    private sealed record Person(string Id,string Email,string Password,string Code);
}
