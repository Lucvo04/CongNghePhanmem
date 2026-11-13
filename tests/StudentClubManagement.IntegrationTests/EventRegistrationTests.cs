using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.IntegrationTests;

public class EventRegistrationTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person student = null!, other = null!, manager = null!, admin = null!;
    private int clubId, eventId, membershipId;
    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        student = await User(); other = await User(); manager = await User(); admin = await User("system-admin");
        await using var db = database.CreateContext(); database.AssertOwnsContext(db);
        var club = new Club { Code = "REG-" + Guid.NewGuid().ToString("N")[..16], Name = "CLB đăng ký" };
        var member = new Membership { Club = club, UserId = student.Id };
        db.Memberships.AddRange(member, new Membership { Club = club, UserId = manager.Id, Role = ClubRole.Manager });
        var item = new ClubEvent { Club = club, Title = "Hoạt động thử <script>x</script>", Location = "Hội trường",
            StartsAt = DateTimeOffset.UtcNow.AddDays(5), EndsAt = DateTimeOffset.UtcNow.AddDays(5).AddHours(2),
            RegistrationDeadline = DateTimeOffset.UtcNow.AddDays(3), Capacity = 1, Status = EventStatus.Published, PublishedAt = DateTimeOffset.UtcNow };
        db.Events.Add(item); await db.SaveChangesAsync(); clubId = club.Id; eventId = item.Id; membershipId = member.Id;
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task HttpRegistrationUsesCurrentUserAndShowsOwnAndManagerLists()
    {
        using var client = await Login(student);
        var html = await client.GetStringAsync($"/Events/Details/{eventId}");
        var response = await client.PostAsync($"/EventRegistrations/Register?eventId={eventId}", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = Hidden(html,"__RequestVerificationToken"), ["EventVersion"] = Hidden(html,"EventVersion"),
            ["UserId"] = other.Id, ["CancelledAt"] = "2000-01-01", ["RegisteredAt"] = "2000-01-01" }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var row = await Own(); Assert.Equal(student.Id,row.UserId); Assert.Null(row.CancelledAt); Assert.True(row.RegisteredAt > DateTimeOffset.UtcNow.AddMinutes(-2));
        var mine = await client.GetAsync("/EventRegistrations/Mine"); Assert.True(mine.Headers.CacheControl!.NoStore);
        var content = WebUtility.HtmlDecode(await mine.Content.ReadAsStringAsync()); Assert.Contains("Đã đăng ký",content);
        Assert.DoesNotContain("<script>x</script>",await mine.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/EventRegistrations?eventId={eventId}")).StatusCode);
        using var management = await Login(manager);
        var list = WebUtility.HtmlDecode(await management.GetStringAsync($"/EventRegistrations?eventId={eventId}"));
        Assert.Contains(student.Code,list); Assert.DoesNotContain(student.Email,list);
        Assert.DoesNotContain("PasswordHash",list); Assert.DoesNotContain("SecurityStamp",list);
        using var outsider = await Login(other);
        Assert.DoesNotContain("Hoạt động thử",WebUtility.HtmlDecode(await outsider.GetStringAsync("/EventRegistrations/Mine")));
        Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/EventRegistrations?eventId={eventId}")).StatusCode);
    }

    [Theory]
    [InlineData("Draft")][InlineData("Cancelled")][InlineData("Completed")]
    [InlineData("SuspendedClub")][InlineData("ArchivedClub")][InlineData("Deadline")][InlineData("Started")]
    [InlineData("Admin")][InlineData("Disabled")][InlineData("Pending")][InlineData("RemovedRole")]
    [InlineData("NotMember")][InlineData("SuspendedMember")][InlineData("LeftMember")][InlineData("StaleEvent")]
    public async Task IneligibleRegistrationNeverConsumesAPlace(string condition)
    {
        var before = await Event();
        await using (var db = database.CreateContext())
        {
            var e = (await db.Events.FindAsync(eventId))!; var u = (await db.Users.FindAsync(student.Id))!;
            if (condition == "Draft") { e.Status = EventStatus.Draft; e.PublishedAt = null; }
            if (condition == "Cancelled") e.Status = EventStatus.Cancelled;
            if (condition == "Completed") e.Status = EventStatus.Completed;
            if (condition == "Deadline") e.RegistrationDeadline = DateTimeOffset.UtcNow.AddSeconds(-1);
            if (condition == "Started") { e.StartsAt = DateTimeOffset.UtcNow.AddHours(-1); e.RegistrationDeadline = e.StartsAt; }
            if (condition == "SuspendedClub") (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Suspended;
            if (condition == "ArchivedClub") (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Archived;
            if (condition == "Admin") u.RoleId = "system-admin";
            if (condition == "RemovedRole") u.RoleId = null;
            if (condition == "Disabled") u.Status = UserStatus.Disabled;
            if (condition == "Pending") u.Status = UserStatus.Pending;
            if (condition.Contains("Member"))
            {
                e.Audience = EventAudience.Club; var m = (await db.Memberships.FindAsync(membershipId))!;
                if (condition is "NotMember" or "LeftMember") { m.Status = MembershipStatus.Left; m.EndedAt = DateTimeOffset.UtcNow; }
                else m.Status = MembershipStatus.Suspended;
            }
            if (condition == "StaleEvent") e.Location = "Changed location";
            await db.SaveChangesAsync();
        }
        var result = await Register(student.Id, condition == "StaleEvent" ? Convert.ToBase64String(before.RowVersion) : null);
        Assert.False(result.Succeeded);
        await using var verify = database.CreateContext(); Assert.False(await verify.EventRegistrations.AnyAsync(r=>r.EventId==eventId));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task CompetingRequestsCannotOverbookOrDuplicate(bool sameStudent)
    {
        var results = await Task.WhenAll(Register(student.Id),Register(sameStudent ? student.Id : other.Id));
        Assert.Single(results,r=>r.Succeeded); Assert.Single(results,r=>r.Code=="Conflict");
        await using var db = database.CreateContext(); Assert.Equal(1,await db.EventRegistrations.CountAsync(r=>r.EventId==eventId&&r.CancelledAt==null));
        var id = await db.EventRegistrations.Where(r=>r.EventId==eventId).Select(r=>r.Id).SingleAsync();
        Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.EntityType==nameof(EventRegistration)&&a.EntityId==id.ToString()));
    }

    [Fact]
    public async Task CancelReleasesPlaceAndReregisterPreservesHistoryWithoutAcceptingStaleForm()
    {
        Assert.True((await Register(student.Id)).Succeeded); var first = await Own();
        Assert.Equal("NotFound",(await Cancel(first.Id,other.Id,first.RowVersion)).Code);
        Assert.True((await Cancel(first.Id,student.Id,first.RowVersion)).Succeeded); var cancelled = await Own(); Assert.NotNull(cancelled.CancelledAt);
        Assert.False((await Register(student.Id)).Succeeded); // A form from before the first registration cannot resurrect it.
        Assert.True((await Register(student.Id,registrationVersion:Convert.ToBase64String(cancelled.RowVersion))).Succeeded);
        Assert.Equal("Conflict",(await Cancel(first.Id,student.Id,first.RowVersion)).Code);
        var after = await Own(); Assert.Equal(first.Id,after.Id); Assert.Null(after.CancelledAt);
        await using var db = database.CreateContext(); Assert.Equal(3,await db.AuditLogs.CountAsync(a=>a.EntityType==nameof(EventRegistration)&&a.EntityId==first.Id.ToString()));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task CancellationAllowedAfterDeadlineButNotAfterStart(bool started)
    {
        Assert.True((await Register(student.Id)).Succeeded); var own = await Own();
        await using (var db = database.CreateContext()) { var e=(await db.Events.FindAsync(eventId))!; e.RegistrationDeadline=DateTimeOffset.UtcNow.AddHours(-2); if(started)e.StartsAt=DateTimeOffset.UtcNow.AddHours(-1); await db.SaveChangesAsync(); }
        Assert.Equal(!started,(await Cancel(own.Id,student.Id,own.RowVersion)).Succeeded);
    }

    [Fact]
    public async Task CapacityCannotShrinkBelowRegistrationsAndEventCancelReleasesAllPlaces()
    {
        await using (var db=database.CreateContext()){ (await db.Events.FindAsync(eventId))!.Capacity=2; await db.SaveChangesAsync(); }
        Assert.True((await Register(student.Id)).Succeeded); Assert.True((await Register(other.Id)).Succeeded);
        var item=await Event(); var form=EventForm.From(item); form.Capacity=1; form.Reason="Reduce capacity";
        await using(var s=app.Services.CreateAsyncScope()) Assert.False((await s.ServiceProvider.GetRequiredService<EventService>().SaveAsync(clubId,eventId,form,manager.Id)).Succeeded);
        var reason=new string('x',500);
        Assert.True((await CancelEvent(reason)).Succeeded);
        await using var verify=database.CreateContext(); Assert.Equal(0,await verify.EventRegistrations.CountAsync(r=>r.EventId==eventId&&r.CancelledAt==null));
        Assert.Equal(2,await verify.AuditLogs.CountAsync(a=>a.Action=="EventRegistrationCancelled"&&a.Reason==reason));
    }

    [Fact]
    public async Task SelfLeaveRequiresCancellingFutureInternalRegistrationFirst()
    {
        await Internal(); Assert.True((await Register(student.Id)).Succeeded);
        Assert.Equal("Conflict",(await ChangeMembership(MembershipStatus.Left,true)).Code);
        var row=await Own(); Assert.True((await Cancel(row.Id,student.Id,row.RowVersion)).Succeeded);
        Assert.True((await ChangeMembership(MembershipStatus.Left,true)).Succeeded);
        using var client=await Login(student); Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Events/Details/{eventId}")).StatusCode);
        Assert.Contains("Đã hủy đăng ký",WebUtility.HtmlDecode(await client.GetStringAsync("/EventRegistrations/Mine")));
    }

    [Theory]
    [InlineData(MembershipStatus.Suspended)][InlineData(MembershipStatus.Left)]
    public async Task ManagementStatusChangeCancelsOnlyFutureInternalRegistrations(MembershipStatus status)
    {
        await Internal(); Assert.True((await Register(student.Id)).Succeeded);
        await using (var db=database.CreateContext())
        {
            var futureSchool=new ClubEvent{ClubId=clubId,Title="School",Location="Hall",StartsAt=DateTimeOffset.UtcNow.AddDays(4),EndsAt=DateTimeOffset.UtcNow.AddDays(5),RegistrationDeadline=DateTimeOffset.UtcNow.AddDays(2),Capacity=20,Status=EventStatus.Published,PublishedAt=DateTimeOffset.UtcNow};
            var pastInternal=new ClubEvent{ClubId=clubId,Title="Past",Location="Hall",Audience=EventAudience.Club,StartsAt=DateTimeOffset.UtcNow.AddDays(-2),EndsAt=DateTimeOffset.UtcNow.AddDays(-1),RegistrationDeadline=DateTimeOffset.UtcNow.AddDays(-3),Capacity=20,Status=EventStatus.Published,PublishedAt=DateTimeOffset.UtcNow.AddDays(-4)};
            db.EventRegistrations.AddRange(new EventRegistration{Event=futureSchool,UserId=student.Id},new EventRegistration{Event=pastInternal,UserId=student.Id}); await db.SaveChangesAsync();
        }
        Assert.True((await ChangeMembership(status,false,new string('x',500))).Succeeded);
        await using var verify=database.CreateContext(); Assert.NotNull((await Own()).CancelledAt);
        Assert.Equal(2,await verify.EventRegistrations.CountAsync(r=>r.UserId==student.Id&&r.CancelledAt==null));
        using var client=await Login(student);
        var html=await client.GetStringAsync("/EventRegistrations/Mine");
        Assert.Contains(new string('x',500),html);
        Assert.DoesNotContain($"href=\"/Events/Details/{eventId}\"",html);
    }

    [Theory]
    [InlineData("Register")][InlineData("Cancel")][InlineData("CancelEvent")][InlineData("Suspend")]
    public async Task AuditFailureRollsBackTheEntireOperation(string operation)
    {
        await Internal();
        if(operation!="Register") Assert.True((await Register(student.Id)).Succeeded);
        await using var db=database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectRegistrationAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityType='EventRegistration') THROW 51001,'Test audit failure',1; END");
        try
        {
            if(operation=="Register") await Assert.ThrowsAsync<DbUpdateException>(()=>Register(student.Id));
            if(operation=="Cancel") { var own=await Own(); await Assert.ThrowsAsync<DbUpdateException>(()=>Cancel(own.Id,student.Id,own.RowVersion)); }
            if(operation=="CancelEvent") await Assert.ThrowsAsync<DbUpdateException>(()=>CancelEvent());
            if(operation=="Suspend") await Assert.ThrowsAsync<DbUpdateException>(()=>ChangeMembership(MembershipStatus.Suspended));
            Assert.Equal(operation=="Register"?0:1,await db.EventRegistrations.CountAsync(r=>r.EventId==eventId&&r.CancelledAt==null));
            Assert.Equal(EventStatus.Published,(await Event()).Status);
            Assert.Equal(MembershipStatus.Active,(await db.Memberships.FindAsync(membershipId))!.Status);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectRegistrationAudit"); }
    }

    [Theory]
    [InlineData("CancelEvent")][InlineData("Suspend")][InlineData("ReduceCapacity")]
    public async Task RegistrationRaceWithManagementCannotBreakInvariants(string operation)
    {
        await Internal();
        await using(var db=database.CreateContext()){ (await db.Events.FindAsync(eventId))!.Capacity=2; db.Memberships.Add(new Membership{ClubId=clubId,UserId=other.Id}); await db.SaveChangesAsync(); }
        Assert.True((await Register(other.Id)).Succeeded);
        async Task Manage()
        {
            if(operation=="CancelEvent") Assert.True((await CancelEvent()).Succeeded);
            else if(operation=="Suspend") Assert.True((await ChangeMembership(MembershipStatus.Suspended)).Succeeded);
            else { var e=await Event(); var f=EventForm.From(e); f.Capacity=1; f.Reason="Reduce"; await using var s=app.Services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<EventService>().SaveAsync(clubId,eventId,f,manager.Id); }
        }
        await Task.WhenAll(Register(student.Id),Manage());
        await using var verify=database.CreateContext(); var active=await verify.EventRegistrations.CountAsync(r=>r.EventId==eventId&&r.CancelledAt==null);
        Assert.True(active<=(await Event()).Capacity);
        if(operation=="CancelEvent") Assert.Equal(0,active);
        if(operation=="Suspend") Assert.False(await verify.EventRegistrations.AnyAsync(r=>r.EventId==eventId&&r.UserId==student.Id&&r.CancelledAt==null));
    }

    [Fact]
    public async Task FormsRequireAntiforgeryAndAnonymousCannotPost()
    {
        using var anonymous=app.BrowserClient(); using var client=await Login(student);
        Assert.Equal(HttpStatusCode.Redirect,(await anonymous.PostAsync($"/EventRegistrations/Register?eventId={eventId}",new FormUrlEncodedContent([]))).StatusCode);
        foreach(var path in new[]{$"/EventRegistrations/Register?eventId={eventId}","/EventRegistrations/Cancel/1"})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync(path,new FormUrlEncodedContent([]))).StatusCode);
        using var reviewer=await Login(admin); Assert.Equal(HttpStatusCode.Redirect,(await reviewer.GetAsync("/EventRegistrations/Mine")).StatusCode);
    }

    [Fact]
    public async Task SqlGuardsUniquenessTimeAndMigrationHistory()
    {
        Assert.True((await Register(student.Id)).Succeeded); var own=await Own();
        await using var db=database.CreateContext();
        db.EventRegistrations.Add(new EventRegistration{EventId=eventId,UserId=student.Id}); await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var row=(await db.EventRegistrations.FindAsync(own.Id))!; row.CancelledAt=row.RegisteredAt.AddDays(-1); await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var error=await Assert.ThrowsAsync<SqlException>(()=>db.GetService<IMigrator>().MigrateAsync("20261109033911_StreamlineAccountsAndClubProposals"));
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Equal(51000,error.Number); Assert.True(await db.EventRegistrations.AnyAsync(r=>r.Id==own.Id));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private async Task Internal(){ await using var db=database.CreateContext(); (await db.Events.FindAsync(eventId))!.Audience=EventAudience.Club; await db.SaveChangesAsync(); }
    private async Task<ClubEvent> Event(){ await using var db=database.CreateContext(); return await db.Events.AsNoTracking().Include(e=>e.Club).SingleAsync(e=>e.Id==eventId); }
    private async Task<EventRegistration> Own(){ await using var db=database.CreateContext(); return await db.EventRegistrations.AsNoTracking().SingleAsync(r=>r.EventId==eventId&&r.UserId==student.Id); }
    private async Task<WorkflowResult> Register(string actor,string? eventVersion=null,string? registrationVersion=null){ eventVersion??=Convert.ToBase64String((await Event()).RowVersion); await using var s=app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<EventRegistrationService>().RegisterAsync(eventId,actor,new(){EventVersion=eventVersion,RowVersion=registrationVersion}); }
    private async Task<WorkflowResult> Cancel(int id,string actor,byte[] stamp){ await using var s=app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<EventRegistrationService>().CancelAsync(id,actor,Convert.ToBase64String(stamp)); }
    private async Task<EventResult> CancelEvent(string reason="Hủy theo kế hoạch"){ var e=await Event(); await using var s=app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<EventService>().TransitionAsync(eventId,manager.Id,new(){Operation="Cancel",Reason=reason,RowVersion=Convert.ToBase64String(e.RowVersion)}); }
    private async Task<MembershipResult> ChangeMembership(MembershipStatus status,bool self=false,string reason="Thay đổi tư cách"){ await using var db=database.CreateContext(); var m=(await db.Memberships.FindAsync(membershipId))!; await using var s=app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<MembershipService>().ChangeStatusAsync(m.Id,self?student.Id:manager.Id,Convert.ToBase64String(m.RowVersion),status,reason,self); }
    private async Task<Person> User(string role="system-student"){ await using var s=app.Services.CreateAsyncScope(); var users=s.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var tag=Guid.NewGuid().ToString("N"); var email=$"registration-{tag}@example.test"; var password=$"Test-Only!9-{tag}"; var u=new ApplicationUser{Email=email,FullName="Sinh viên kiểm thử",StudentCode=tag[..24],RoleId=role}; Assert.True((await users.CreateAsync(u,password)).Succeeded); return new(u.Id,email,password,u.StudentCode); }
    private async Task<HttpClient> Login(Person p){ var c=app.BrowserClient(); var token=Hidden(await c.GetStringAsync("/Account/Login"),"__RequestVerificationToken"); Assert.Equal(HttpStatusCode.Redirect,(await c.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["Email"]=p.Email,["Password"]=p.Password,["__RequestVerificationToken"]=token}))).StatusCode); return c; }
    private static string Hidden(string html,string name){var m=Regex.Match(html,$"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");Assert.True(m.Success,"Missing "+name);return WebUtility.HtmlDecode(m.Groups[1].Value);}
    private sealed record Person(string Id,string Email,string Password,string Code);
}
