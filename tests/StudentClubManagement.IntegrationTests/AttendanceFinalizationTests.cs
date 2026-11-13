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

public class AttendanceFinalizationTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person student = null!, second = null!, manager = null!, admin = null!;
    private int eventId, clubId, firstId, secondId;

    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        student = await User("Sinh viên A"); second = await User("Sinh viên B");
        manager = await User("Chủ nhiệm"); admin = await User("Quản trị", "system-admin");
        await using var db = database.CreateContext(); database.AssertOwnsContext(db);
        var club = new Club { Code = Guid.NewGuid().ToString("N")[..20], Name = "CLB Chốt điểm danh" };
        db.Memberships.AddRange(new Membership { Club = club, UserId = manager.Id, Role = ClubRole.Manager },
            new Membership { Club = club, UserId = student.Id });
        var item = new ClubEvent { Club = club, Title = "Workshop đã kết thúc", Description = "PRIVATE-INTERNAL-CONTENT", Location = "Hội trường",
            StartsAt = DateTimeOffset.UtcNow.AddHours(-3), EndsAt = DateTimeOffset.UtcNow.AddHours(-1), RegistrationDeadline = DateTimeOffset.UtcNow.AddHours(-4),
            Status = EventStatus.Published, PublishedAt = DateTimeOffset.UtcNow.AddDays(-3), Capacity = 30, Audience = EventAudience.Club };
        var first = new EventRegistration { Event = item, UserId = student.Id, RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var other = new EventRegistration { Event = item, UserId = second.Id, RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1) };
        db.EventRegistrations.AddRange(first, other); await db.SaveChangesAsync();
        clubId = club.Id; eventId = item.Id; firstId = first.Id; secondId = other.Id;
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task HttpFinalizationRequiresExplicitAbsenceConsentAndFreezesEntireRoster()
    {
        Assert.True((await Mark(firstId, AttendanceStatus.Present)).Succeeded);
        using var client = await Login(manager);
        var html = await client.GetStringAsync($"/Attendances?eventId={eventId}&search={student.Code}");
        Assert.Single(Regex.Matches(html, "data-attendance-id=")); Assert.Contains("2 người", WebUtility.HtmlDecode(html));
        var values = new Dictionary<string,string> { ["__RequestVerificationToken"] = Hidden(html,"__RequestVerificationToken"),
            ["EventVersion"] = Hidden(html,"EventVersion"), ["RosterVersion"] = Hidden(html,"RosterVersion"),
            ["AttendanceFinalizedAt"] = "2000-01-01", ["RegistrationIds"] = firstId.ToString() };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/Attendances/Finalize?eventId={eventId}", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/Attendances/Finalize?eventId={eventId}",new FormUrlEncodedContent(values))).StatusCode);
        await using var db = database.CreateContext(); Assert.Null((await db.Events.AsNoTracking().SingleAsync(e=>e.Id==eventId)).AttendanceFinalizedAt);
        Assert.False(await db.Attendances.AnyAsync(a=>a.IsInFinalRoster && a.Registration.EventId==eventId));
        values["MarkUnmarkedAbsent"] = "true";
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync($"/Attendances/Finalize?eventId={eventId}",new FormUrlEncodedContent(values))).StatusCode);
        var item = await db.Events.AsNoTracking().SingleAsync(e=>e.Id==eventId);
        Assert.True(item.AttendanceFinalizedAt > DateTimeOffset.UtcNow.AddMinutes(-2)); Assert.Equal(EventStatus.Published,item.Status);
        var rows = await db.Attendances.Where(a=>a.Registration.EventId==eventId).ToListAsync(); Assert.Equal(2,rows.Count); Assert.All(rows,a=>Assert.True(a.IsInFinalRoster));
        Assert.Equal(AttendanceStatus.Present,rows.Single(a=>a.RegistrationId==firstId).Status);
        var absent = rows.Single(a=>a.RegistrationId==secondId); Assert.Equal(AttendanceStatus.Absent,absent.Status); Assert.Equal(manager.Id,absent.MarkedById);
        Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.Action=="AttendanceFinalized"&&a.EntityId==eventId.ToString()));
        using var anonymous=app.BrowserClient(); Assert.Equal(HttpStatusCode.Redirect,(await anonymous.PostAsync($"/Attendances/Finalize?eventId={eventId}",new FormUrlEncodedContent(values))).StatusCode);
    }

    [Theory]
    [InlineData("Ongoing")][InlineData("Draft")][InlineData("Cancelled")][InlineData("Archived")]
    [InlineData("Student")][InlineData("Disabled")][InlineData("Demoted")]
    public async Task InvalidFinalizationDoesNotChangeResults(string condition)
    {
        await using (var db=database.CreateContext())
        {
            var item=(await db.Events.FindAsync(eventId))!;
            if(condition=="Ongoing")item.EndsAt=DateTimeOffset.UtcNow.AddHours(1);
            if(condition=="Draft"){item.Status=EventStatus.Draft;item.PublishedAt=null;}
            if(condition=="Cancelled")item.Status=EventStatus.Cancelled;
            if(condition=="Archived")(await db.Clubs.FindAsync(clubId))!.Status=ClubStatus.Archived;
            if(condition=="Disabled")(await db.Users.FindAsync(manager.Id))!.Status=UserStatus.Disabled;
            if(condition=="Demoted")(await db.Memberships.SingleAsync(m=>m.UserId==manager.Id&&m.ClubId==clubId)).Role=ClubRole.Member;
            await db.SaveChangesAsync();
        }
        Assert.False((await Finalize(await Preview(true),condition=="Student"?student.Id:manager.Id)).Succeeded);
        await using var verify=database.CreateContext(); Assert.Null((await verify.Events.FindAsync(eventId))!.AttendanceFinalizedAt);
        Assert.False(await verify.Attendances.AnyAsync(a=>a.Registration.EventId==eventId));
    }

    [Fact]
    public async Task AlreadyMarkedAndEmptyRostersCanFinalizeWithoutAbsenceConsent()
    {
        Assert.True((await Mark(firstId,AttendanceStatus.Present)).Succeeded); Assert.True((await Mark(secondId,AttendanceStatus.Excused)).Succeeded);
        Assert.True((await Finalize(await Preview())).Succeeded);
        await using var db=database.CreateContext();
        var empty = new ClubEvent { ClubId=clubId,Title="Empty",Location="Hall",StartsAt=DateTimeOffset.UtcNow.AddHours(-3),EndsAt=DateTimeOffset.UtcNow.AddHours(-2),RegistrationDeadline=DateTimeOffset.UtcNow.AddHours(-4),Capacity=10,Status=EventStatus.Completed,PublishedAt=DateTimeOffset.UtcNow.AddDays(-1) };
        db.Events.Add(empty);await db.SaveChangesAsync();
        await using var scope=app.Services.CreateAsyncScope(); var service=scope.ServiceProvider.GetRequiredService<AttendanceService>();
        var summary=await service.SummaryAsync(empty);
        Assert.Equal(0,summary.Total);Assert.True((await service.FinalizeAsync(empty.Id,admin.Id,new(){EventVersion=Convert.ToBase64String(empty.RowVersion),RosterVersion=summary.Version})).Succeeded);
    }

    [Fact]
    public async Task PreviewDetectsChangesOutsideTheFilteredPageAndOlderFormsCannotWriteAfterFinalization()
    {
        var preview=await Preview(true); var oldMark=await MarkForm(firstId,AttendanceStatus.Present);
        Assert.True((await Mark(secondId,AttendanceStatus.Excused)).Succeeded);
        Assert.Equal("Conflict",(await Finalize(preview)).Code);
        Assert.True((await Finalize(await Preview(true))).Succeeded);
        Assert.Equal("Conflict",(await Save(firstId,manager.Id,oldMark)).Code);
    }

    [Fact]
    public async Task ConcurrentFinalizationCommitsOnce()
    {
        var preview=await Preview(true);
        var results=await Task.WhenAll(Finalize(preview),Finalize(preview,admin.Id));
        Assert.Single(results,r=>r.Succeeded);Assert.Single(results,r=>r.Code=="Conflict");
        await using var db=database.CreateContext();Assert.Equal(2,await db.Attendances.CountAsync(a=>a.IsInFinalRoster&&a.Registration.EventId==eventId));
        Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.Action=="AttendanceFinalized"&&a.EntityId==eventId.ToString()));
    }

    [Fact]
    public async Task MarkingRaceWithFinalizationMustRefreshTheLosingForm()
    {
        var preview=await Preview(true);var mark=await MarkForm(firstId,AttendanceStatus.Present);
        var finalizeTask=Finalize(preview);var markTask=Save(firstId,admin.Id,mark);await Task.WhenAll(finalizeTask,markTask);
        Assert.NotEqual((await finalizeTask).Succeeded,(await markTask).Succeeded);
        if(!(await finalizeTask).Succeeded)Assert.True((await Finalize(await Preview(true))).Succeeded);
        await using var db=database.CreateContext();Assert.Equal(2,await db.Attendances.CountAsync(a=>a.IsInFinalRoster&&a.Registration.EventId==eventId));
    }

    [Theory]
    [InlineData("MissingReason")][InlineData("Whitespace")][InlineData("Unmarked")][InlineData("OutsideRoster")]
    public async Task CorrectionsCannotBypassReasonOrAlterFrozenRoster(string condition)
    {
        Assert.True((await Finalize(await Preview(true))).Succeeded);
        var target=firstId;
        if(condition=="OutsideRoster")
        {
            await using var db=database.CreateContext();var extra=new EventRegistration {EventId=eventId,UserId=manager.Id,RegisteredAt=DateTimeOffset.UtcNow.AddDays(-1)};
            db.EventRegistrations.Add(extra);await db.SaveChangesAsync();target=extra.Id;
        }
        var form=await MarkForm(target,condition=="Unmarked"?AttendanceStatus.Unmarked:AttendanceStatus.Present);
        form.Reason=condition=="MissingReason"?null:condition=="Whitespace"?"   ":"Có bằng chứng tham dự";
        Assert.False((await Save(target,manager.Id,form)).Succeeded);
        await using var verify=database.CreateContext();Assert.Equal(2,await verify.Attendances.CountAsync(a=>a.IsInFinalRoster&&a.Registration.EventId==eventId));
        Assert.All(await verify.Attendances.Where(a=>a.Registration.EventId==eventId).ToListAsync(),a=>Assert.Equal(AttendanceStatus.Absent,a.Status));
        using var client=await Login(manager);var html=await client.GetStringAsync($"/Attendances?eventId={eventId}");
        Assert.Equal(2,Regex.Matches(html,"data-attendance-id=").Count);
    }

    [Fact]
    public async Task CorrectionKeepsRosterAndExposesOnlyOwnFinalResultAfterLeavingClub()
    {
        Assert.True((await Mark(firstId,AttendanceStatus.Present,"PRIVATE-PRELIMINARY-NOTE")).Succeeded);
        using var client=await Login(student);
        var before=await client.GetStringAsync($"/Attendances/History/{firstId}");Assert.Contains("chưa chốt",WebUtility.HtmlDecode(before));Assert.DoesNotContain("PRIVATE-PRELIMINARY-NOTE",before);
        Assert.True((await Finalize(await Preview(true))).Succeeded);
        var stale=await MarkForm(firstId,AttendanceStatus.Absent);stale.Reason="Old form";
        var reason="Đối chiếu <script>unsafe()</script> với biên bản";
        Assert.True((await Mark(firstId,AttendanceStatus.Excused,reason)).Succeeded);
        Assert.Equal("Conflict",(await Save(firstId,admin.Id,stale)).Code);
        await using(var scope=app.Services.CreateAsyncScope())
        {
            await using var db=database.CreateContext();var member=await db.Memberships.SingleAsync(m=>m.UserId==student.Id&&m.ClubId==clubId);
            Assert.True((await scope.ServiceProvider.GetRequiredService<MembershipService>().ChangeStatusAsync(member.Id,manager.Id,Convert.ToBase64String(member.RowVersion),MembershipStatus.Left,"Rời CLB")).Succeeded);
        }
        var response=await client.GetAsync($"/Attendances/History/{firstId}");Assert.True(response.Headers.CacheControl!.NoStore);
        var html=await response.Content.ReadAsStringAsync();var decoded=WebUtility.HtmlDecode(html);
        Assert.Contains("Kết quả: Có phép",decoded);Assert.Contains("Có mặt → Có phép",decoded);Assert.Contains(reason,decoded);
        foreach(var hidden in new[]{"<script>unsafe()</script>","PRIVATE-PRELIMINARY-NOTE","PRIVATE-INTERNAL-CONTENT",manager.Email,manager.Id,"BeforeData","AfterData"})Assert.DoesNotContain(hidden,html);
        Assert.DoesNotContain($"href=\"/Events/Details/{eventId}\"",html);
        using var other=await Login(second);Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/Attendances/History/{firstId}")).StatusCode);
        using var leader=await Login(manager);Assert.Contains("PRIVATE-PRELIMINARY-NOTE",await leader.GetStringAsync($"/Attendances/History/{firstId}"));
        await using var verify=database.CreateContext();var audit=await verify.AuditLogs.SingleAsync(a=>a.Action=="AttendanceCorrected"&&a.EntityId==firstId.ToString());
        Assert.Contains("Present",audit.BeforeData!);Assert.Contains("Excused",audit.AfterData);Assert.Equal(reason,audit.Reason);Assert.Equal(manager.Id,audit.ActorId);
        Assert.Equal(2,await verify.Attendances.CountAsync(a=>a.IsInFinalRoster&&a.Registration.EventId==eventId));
    }

    [Theory]
    [InlineData("FinalHeader")][InlineData("ConvertedAbsence")][InlineData("Correction")]
    public async Task AuditFailureRollsBackRosterTimestampAndCorrections(string phase)
    {
        if(phase=="Correction")Assert.True((await Finalize(await Preview(true))).Succeeded);
        await using var db=database.CreateContext();
        var trigger = phase switch
        {
            "FinalHeader" => "CREATE TRIGGER RejectFinalizationAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='AttendanceFinalized') THROW 51001,'Test audit failure',1; END",
            "Correction" => "CREATE TRIGGER RejectFinalizationAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='AttendanceCorrected') THROW 51001,'Test audit failure',1; END",
            _ => "CREATE TRIGGER RejectFinalizationAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='AttendanceUpdated') THROW 51001,'Test audit failure',1; END"
        };
        await db.Database.ExecuteSqlRawAsync(trigger);
        try
        {
            if(phase=="Correction")await Assert.ThrowsAsync<DbUpdateException>(()=>Mark(firstId,AttendanceStatus.Present,"Chỉnh theo biên bản"));
            else { var form=await Preview(true);await Assert.ThrowsAsync<DbUpdateException>(()=>Finalize(form)); }
            var item=await db.Events.AsNoTracking().SingleAsync(e=>e.Id==eventId);
            if(phase=="Correction")
            {
                Assert.NotNull(item.AttendanceFinalizedAt);Assert.Equal(AttendanceStatus.Absent,(await db.Attendances.FindAsync(firstId))!.Status);
                Assert.Equal(2,await db.Attendances.CountAsync(a=>a.IsInFinalRoster&&a.Registration.EventId==eventId));
            }
            else { Assert.Null(item.AttendanceFinalizedAt);Assert.False(await db.Attendances.AnyAsync(a=>a.Registration.EventId==eventId)); }
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectFinalizationAudit"); }
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task EventCancellationPreservesFrozenRosterAndPreventsFurtherChanges(bool race)
    {
        bool finalized;
        if(race)
        {
            var preview=await Preview(true);var finalizeTask=Finalize(preview);var cancelTask=CancelEvent();await Task.WhenAll(finalizeTask,cancelTask);
            finalized=(await finalizeTask).Succeeded;
            if(!(await cancelTask).Succeeded)Assert.True((await CancelEvent()).Succeeded);
        }
        else { Assert.True((await Finalize(await Preview(true))).Succeeded);finalized=true;Assert.True((await CancelEvent()).Succeeded); }
        Assert.False((await Finalize(await Preview(true))).Succeeded);
        Assert.False((await Mark(firstId,AttendanceStatus.Present,"Chỉnh sau hủy")).Succeeded);
        await using var db=database.CreateContext();Assert.Equal(finalized?2:0,await db.Attendances.CountAsync(a=>a.IsInFinalRoster&&a.Registration.EventId==eventId));
        using var studentClient=await Login(student);var html=await studentClient.GetStringAsync($"/Attendances/History/{firstId}");
        Assert.Contains("Hoạt động đã hủy",WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task HistoryPaginationPermissionsAndBadSnapshotAreHandledWithoutLeakingData()
    {
        Assert.True((await Finalize(await Preview(true))).Succeeded);
        for(var i=0;i<12;i++)Assert.True((await Mark(firstId,i%2==0?AttendanceStatus.Present:AttendanceStatus.Excused,"Đối chiếu lần "+i)).Succeeded);
        await using var db=database.CreateContext();
        var corrections=await db.AuditLogs.Where(a=>a.Action=="AttendanceCorrected"&&a.EntityId==firstId.ToString()).ToListAsync();
        var time=DateTimeOffset.UtcNow;foreach(var a in corrections)a.OccurredAt=time;await db.SaveChangesAsync();
        using var client=await Login(student);
        var first=await client.GetStringAsync($"/Attendances/History/{firstId}?returnUrl=https://outside.example.test/");
        var secondPage=await client.GetStringAsync($"/Attendances/History/{firstId}?page=2");
        var ids=Regex.Matches(first+secondPage,"data-attendance-history-id=\"(\\d+)\"").Select(m=>long.Parse(m.Groups[1].Value));
        Assert.Equal(corrections.Select(a=>a.Id).OrderDescending(),ids);Assert.DoesNotContain("outside.example.test",first);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync($"/Attendances/History/{firstId}?page=bad")).StatusCode);
        var extra = new EventRegistration {EventId=eventId,UserId=manager.Id,RegisteredAt=DateTimeOffset.UtcNow.AddDays(-1)};
        db.EventRegistrations.Add(extra);await db.SaveChangesAsync();
        using var leader=await Login(manager);
        Assert.Contains("không thuộc danh sách",WebUtility.HtmlDecode(await leader.GetStringAsync($"/Attendances/History/{extra.Id}")));
        var malformed=new AuditLog {ActorId=manager.Id,Action="AttendanceCorrected",EntityType=nameof(Attendance),EntityId=firstId.ToString(),AfterData="bad json",BeforeData="[]"};
        db.AuditLogs.Add(malformed);await db.SaveChangesAsync();
        Assert.Contains("Không có dữ liệu trạng thái",WebUtility.HtmlDecode(await client.GetStringAsync($"/Attendances/History/{firstId}")));
        Assert.DoesNotContain("bad json",await client.GetStringAsync($"/Attendances/History/{firstId}"));
        (await db.Memberships.SingleAsync(m=>m.ClubId==clubId&&m.UserId==manager.Id)).Role=ClubRole.Member;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound,(await leader.GetAsync($"/Attendances/History/{firstId}")).StatusCode);
        (await db.Users.FindAsync(student.Id))!.Status=UserStatus.Disabled;await db.SaveChangesAsync();
        Assert.False((await client.GetAsync($"/Attendances/History/{firstId}")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task SqlRejectsUnmarkedFinalResultsInvalidFinalTimeAndDestructiveRollback()
    {
        await using var db=database.CreateContext();
        var item=(await db.Events.FindAsync(eventId))!;item.AttendanceFinalizedAt=item.EndsAt.AddSeconds(-1);
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        Assert.True((await Finalize(await Preview(true))).Succeeded);
        var result=(await db.Attendances.FindAsync(firstId))!;result.Status=AttendanceStatus.Unmarked;
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        var error=await Assert.ThrowsAsync<SqlException>(()=>db.GetService<IMigrator>().MigrateAsync("20261110103415_AddAttendances"));
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Equal(51000,error.Number);Assert.NotNull((await db.Events.FindAsync(eventId))!.AttendanceFinalizedAt);
        Assert.Equal(2,await db.Attendances.CountAsync(a=>a.IsInFinalRoster&&a.Registration.EventId==eventId));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private async Task<EventResult> CancelEvent(){await using var db=database.CreateContext();var e=await db.Events.AsNoTracking().SingleAsync(e=>e.Id==eventId);await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<EventService>().TransitionAsync(eventId,admin.Id,new(){Operation="Cancel",Reason="Hủy theo kế hoạch",RowVersion=Convert.ToBase64String(e.RowVersion)});}

    private async Task<FinalizeAttendanceForm> Preview(bool convert=false)
    {
        await using var db=database.CreateContext();var e=await db.Events.AsNoTracking().SingleAsync(e=>e.Id==eventId);
        await using var scope=app.Services.CreateAsyncScope();var summary=await scope.ServiceProvider.GetRequiredService<AttendanceService>().SummaryAsync(e);
        return new(){EventVersion=Convert.ToBase64String(e.RowVersion),RosterVersion=summary.Version,MarkUnmarkedAbsent=convert};
    }
    private async Task<WorkflowResult> Finalize(FinalizeAttendanceForm form,string? actor=null){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<AttendanceService>().FinalizeAsync(eventId,actor??manager.Id,form);}
    private async Task<AttendanceForm> MarkForm(int id,AttendanceStatus status){await using var db=database.CreateContext();var r=await db.EventRegistrations.AsNoTracking().Include(r=>r.Event).Include(r=>r.Attendance).SingleAsync(r=>r.Id==id);return new(){Status=status,EventVersion=Convert.ToBase64String(r.Event.RowVersion),RegistrationVersion=Convert.ToBase64String(r.RowVersion),RowVersion=r.Attendance is null?null:Convert.ToBase64String(r.Attendance.RowVersion)};}
    private async Task<WorkflowResult> Mark(int id,AttendanceStatus status,string? reason=null){var form=await MarkForm(id,status);form.Reason=reason;return await Save(id,manager.Id,form);}
    private async Task<WorkflowResult> Save(int id,string actor,AttendanceForm form){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<AttendanceService>().SaveAsync(id,actor,form);}
    private async Task<Person> User(string name,string role="system-student"){await using var scope=app.Services.CreateAsyncScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var tag=Guid.NewGuid().ToString("N");var password=$"Test-Only!9-{tag}";var u=new ApplicationUser{Email=$"final-{tag}@example.test",FullName=name,StudentCode=tag[..24],RoleId=role};Assert.True((await users.CreateAsync(u,password)).Succeeded);return new(u.Id,u.Email,password,u.StudentCode);}
    private async Task<HttpClient> Login(Person p){var c=app.BrowserClient();var token=Hidden(await c.GetStringAsync("/Account/Login"),"__RequestVerificationToken");Assert.Equal(HttpStatusCode.Redirect,(await c.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["Email"]=p.Email,["Password"]=p.Password,["__RequestVerificationToken"]=token}))).StatusCode);return c;}
    private static string Hidden(string html,string name){var m=Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]*)\"");Assert.True(m.Success,"Missing "+name);return WebUtility.HtmlDecode(m.Groups[1].Value);}
    private sealed record Person(string Id,string Email,string Password,string Code);
}
