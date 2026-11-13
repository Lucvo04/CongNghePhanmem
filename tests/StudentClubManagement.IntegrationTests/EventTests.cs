using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.IntegrationTests;

public class EventTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person manager = null!, member = null!, outsider = null!;
    private int clubId;
    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope(); database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        manager = await UserAsync(); member = await UserAsync(); outsider = await UserAsync();
        await using var db = database.CreateContext();
        var club = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "CLB kiểm thử hoạt động" };
        db.Clubs.Add(club); await db.SaveChangesAsync(); clubId = club.Id;
        db.Memberships.AddRange(new Membership { ClubId = clubId, UserId = manager.Id, Role = ClubRole.Manager }, new Membership { ClubId = clubId, UserId = member.Id });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task HttpCreatePublishEditCancelProtectsFieldsAndKeepsAudit()
    {
        using var client = await LoginAsync(manager); var form = Form();
        var html = await client.GetStringAsync($"/Events/Create?clubId={clubId}");
        var fields = Fields(form); fields["__RequestVerificationToken"] = Extract(html, "__RequestVerificationToken");
        fields["Status"] = "2"; fields["PublishedAt"] = "2020-01-01"; fields["Id"] = "1"; fields["Club.Id"] = "99999";
        var response = await client.PostAsync($"/Events/Create?clubId={clubId}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        var id = int.Parse(location.Split('/').Last()); var item = await EventAsync(id);
        Assert.Equal(EventStatus.Draft, item.Status); Assert.Null(item.PublishedAt); Assert.Equal(clubId, item.ClubId);
        Assert.Equal(new DateTimeOffset(form.StartsAt!.Value, TimeSpan.FromHours(7)).ToUniversalTime(), item.StartsAt);
        var detail = await client.GetAsync(location); Assert.True(detail.Headers.CacheControl!.NoStore);
        Assert.DoesNotContain("<script>alert(1)</script>", await detail.Content.ReadAsStringAsync());
        var publishFields = new Dictionary<string,string> { ["__RequestVerificationToken"] = Extract(await detail.Content.ReadAsStringAsync(), "__RequestVerificationToken"), ["RowVersion"] = Convert.ToBase64String(item.RowVersion), ["Operation"] = "Publish" };
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/Events/ChangeStatus/{id}", new FormUrlEncodedContent(publishFields))).StatusCode);
        item = await EventAsync(id); Assert.Equal(EventStatus.Published, item.Status);
        var editHtml = await client.GetStringAsync($"/Events/Edit/{id}");
        form.Title = "Hoạt động đã sửa"; form.RowVersion = Extract(editHtml,"RowVersion"); form.Reason = "Đổi tên rõ hơn";
        fields = Fields(form); fields["__RequestVerificationToken"] = Extract(editHtml,"__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/Events/Edit/{id}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.True((await Transition(id,"Cancel",reason:"Mưa lớn")).Succeeded);
        Assert.Equal(EventStatus.Cancelled,(await EventAsync(id)).Status);
        using var guest = app.BrowserClient(); Assert.Equal(HttpStatusCode.OK,(await guest.GetAsync(location)).StatusCode);
        await using var db = database.CreateContext(); var logs = await db.AuditLogs.Where(a => a.EntityType == nameof(ClubEvent) && a.EntityId == id.ToString()).ToListAsync();
        Assert.Equal(4,logs.Count); Assert.Contains(logs,a=>a.Action=="EventCancelled" && a.Reason=="Mưa lớn");
        Assert.All(logs,a=>Assert.Equal(manager.Id,a.ActorId));
        Assert.Contains("Đã hủy",WebUtility.HtmlDecode(await guest.GetStringAsync(location)));
        Assert.DoesNotContain("Lịch sử thao tác",WebUtility.HtmlDecode(await guest.GetStringAsync(location)));
    }

    [Fact]
    public async Task DraftAndCancelledDraftNeverLeakButSchoolPublicationIsPublic()
    {
        var id = await Create(); using var guest = app.BrowserClient(); using var other = await LoginAsync(outsider);
        foreach(var client in new[] {guest,other}) Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Events/Details/{id}")).StatusCode);
        Assert.True((await Transition(id,"Cancel",reason:"Bỏ kế hoạch")).Succeeded);
        Assert.Equal(HttpStatusCode.NotFound,(await guest.GetAsync($"/Events/Details/{id}")).StatusCode);
        Assert.False((await Transition(id,"Publish")).Succeeded);
        var published = await Create(); Assert.True((await Transition(published,"Publish")).Succeeded);
        Assert.Equal(HttpStatusCode.OK,(await guest.GetAsync($"/Events/Details/{published}")).StatusCode);
    }

    [Fact]
    public async Task InternalEventRequiresActiveMembershipEvenWithDirectIdAndAfterCancellation()
    {
        var form=Form(); form.Audience=EventAudience.Club; var id=await Create(form); Assert.True((await Transition(id,"Publish")).Succeeded);
        using var guest=app.BrowserClient(); using var other=await LoginAsync(outsider); using var own=await LoginAsync(member);
        Assert.Equal(HttpStatusCode.OK,(await own.GetAsync($"/Events/Details/{id}")).StatusCode);
        foreach(var client in new[]{guest,other}) Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Events/Details/{id}")).StatusCode);
        await using(var db=database.CreateContext()) { var membership=await db.Memberships.SingleAsync(m=>m.ClubId==clubId && m.UserId==member.Id); membership.Status=MembershipStatus.Suspended; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.NotFound,(await own.GetAsync($"/Events/Details/{id}")).StatusCode);
        Assert.True((await Transition(id,"Cancel",reason:"Hủy nội bộ")).Succeeded);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/Events/Details/{id}")).StatusCode);
    }

    [Fact]
    public async Task WrongClubCannotManageAndRevokedManagerCannotUseOldForm()
    {
        var id=await Create(); var form=EventForm.From(await EventAsync(id));
        using var other=await LoginAsync(outsider);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/Events?clubId={clubId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/Events/Edit/{id}")).StatusCode);
        Assert.Equal("NotFound",(await Save(form,id,outsider.Id)).Code);
        Assert.Equal("NotFound",(await Transition(id,"Publish",actor:outsider.Id)).Code);
        await using(var db=database.CreateContext()) { var chief=await db.Memberships.SingleAsync(m=>m.ClubId==clubId && m.UserId==manager.Id); chief.Role=ClubRole.Member; await db.SaveChangesAsync(); }
        Assert.Equal("NotFound",(await Save(form,id)).Code); Assert.Equal("NotFound",(await Transition(id,"Publish")).Code);
        var admin=await UserAsync(SystemRoles.Admin); Assert.True((await Transition(id,"Publish",actor:admin.Id)).Succeeded);
    }

    [Theory]
    [InlineData("Dates")][InlineData("Deadline")][InlineData("Past")][InlineData("Capacity")][InlineData("Audience")][InlineData("Title")]
    public async Task InvalidInputCannotCreateEvent(string kind)
    {
        var f=Form();
        switch(kind) { case "Dates": f.EndsAt=f.StartsAt; break; case "Deadline": f.RegistrationDeadline=f.EndsAt; break;
            case "Past": f.StartsAt=DateTime.Now.AddDays(-20); break; case "Capacity": f.Capacity=0; break; case "Audience": f.Audience=(EventAudience)99; break; case "Title": f.Title=" "; break; }
        Assert.Equal("Validation",(await Save(f)).Code);
        await using var db=database.CreateContext(); Assert.False(await db.Events.AnyAsync(e=>e.ClubId==clubId));
    }

    [Fact]
    public async Task PublishedEditNeedsReasonAndCannotChangeAudienceOrStartAfterOldStart()
    {
        var id=await Create(); Assert.True((await Transition(id,"Publish")).Succeeded); var f=EventForm.From(await EventAsync(id));
        Assert.Equal("Validation",(await Save(f,id)).Code); f.Reason="Đổi kế hoạch"; f.Audience=EventAudience.Club;
        Assert.Equal("Validation",(await Save(f,id)).Code); f.Audience=EventAudience.School;
        await using(var db=database.CreateContext()) { var item=await db.Events.FindAsync(id); item!.StartsAt=DateTimeOffset.UtcNow.AddHours(-1); item.RegistrationDeadline=item.StartsAt.AddHours(-1); await db.SaveChangesAsync(); }
        f.RowVersion=Convert.ToBase64String((await EventAsync(id)).RowVersion);
        Assert.Equal("Validation",(await Save(f,id)).Code);
    }

    [Fact]
    public async Task StateTransitionsEnforceDeadlineReasonCompletionAndTerminalState()
    {
        var id=await Create(); Assert.False((await Transition(id,"Complete")).Succeeded); Assert.False((await Transition(id,"Cancel")).Succeeded);
        Assert.True((await Transition(id,"Publish")).Succeeded); Assert.False((await Transition(id,"Publish")).Succeeded); Assert.False((await Transition(id,"Complete")).Succeeded);
        await using(var db=database.CreateContext()) { var e=await db.Events.FindAsync(id); e!.StartsAt=DateTimeOffset.UtcNow.AddHours(-2); e.EndsAt=DateTimeOffset.UtcNow.AddHours(-1); e.RegistrationDeadline=e.StartsAt.AddHours(-1); await db.SaveChangesAsync(); }
        Assert.True((await Transition(id,"Complete")).Succeeded); Assert.False((await Transition(id,"Cancel",reason:"Không được")).Succeeded);
        var old=Form(); old.RegistrationDeadline=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime.AddDays(-1);
        var closed=await Create(old); Assert.False((await Transition(closed,"Publish")).Succeeded);
    }

    [Theory]
    [InlineData(ClubStatus.Suspended)][InlineData(ClubStatus.Archived)]
    public async Task ClubStatePreventsPublicationAndEdits(ClubStatus state)
    {
        var id=await Create();
        await using(var db=database.CreateContext()) { var club=await db.Clubs.FindAsync(clubId); club!.Status=state; await db.SaveChangesAsync(); }
        Assert.False((await Save(Form())).Succeeded); Assert.False((await Save(EventForm.From(await EventAsync(id)),id)).Succeeded);
        Assert.False((await Transition(id,"Publish")).Succeeded);
        Assert.Equal(state==ClubStatus.Suspended,(await Transition(id,"Cancel",reason:"CLB ngừng hoạt động")).Succeeded);
    }

    [Fact]
    public async Task ConcurrentPublicationProducesOnlyOneTransitionAndStaleEditIsRejected()
    {
        var id=await Create(); var initial=await EventAsync(id); var stamp=Convert.ToBase64String(initial.RowVersion);
        var results=await Task.WhenAll(Transition(id,"Publish",stamp:stamp),Transition(id,"Publish",stamp:stamp));
        Assert.Single(results,r=>r.Succeeded); Assert.Contains(results,r=>r.Code=="Conflict");
        var stale=EventForm.From(initial); stale.Reason="Bản cũ"; Assert.Equal("Conflict",(await Save(stale,id)).Code);
        await using var db=database.CreateContext(); Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.EntityType==nameof(ClubEvent) && a.EntityId==id.ToString() && a.Action=="EventPublished"));
    }

    [Fact]
    public async Task MissingAntiforgeryAndInvalidEnumAreRejected()
    {
        var id=await Create(); using var client=await LoginAsync(manager);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync($"/Events/ChangeStatus/{id}",new FormUrlEncodedContent(new Dictionary<string,string>{{"Operation","Publish"}}))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync($"/Events?clubId={clubId}&status=99")).StatusCode);
        var html=await client.GetStringAsync($"/Events/Create?clubId={clubId}"); var fields=Fields(Form()); fields["StartsAt"]="not-a-date"; fields["__RequestVerificationToken"]=Extract(html,"__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/Events/Create?clubId={clubId}",new FormUrlEncodedContent(fields))).StatusCode);
        await using var db=database.CreateContext(); Assert.Equal(1,await db.Events.CountAsync(e=>e.ClubId==clubId));
    }

    [Fact]
    public async Task ManagementListFiltersAndPaginatesWithoutShowingOtherClubs()
    {
        for(var i=0;i<11;i++) { var f=Form(); f.Title=$"ACT-{i:D2}"; await Create(f); }
        var special=Form(); special.Title="Buổi giao lưu riêng"; var id=await Create(special); Assert.True((await Transition(id,"Publish")).Succeeded);
        using var client=await LoginAsync(manager);
        var first=await client.GetStringAsync($"/Events?clubId={clubId}"); Assert.Equal(10,Regex.Matches(first,"Chi tiết hoạt động").Count);
        var second=await client.GetStringAsync($"/Events?clubId={clubId}&page=2"); Assert.Equal(2,Regex.Matches(second,"Chi tiết hoạt động").Count);
        var filtered=await client.GetStringAsync($"/Events?clubId={clubId}&status=2"); Assert.Single(Regex.Matches(filtered,"Chi tiết hoạt động"));
        var search=await client.GetStringAsync($"/Events?clubId={clubId}&search=ACT-03"); Assert.Single(Regex.Matches(search,"Chi tiết hoạt động")); Assert.Contains("ACT-03",search);
    }

    [Fact]
    public async Task SqlRejectsInvalidPeriodAndMigrationRollbackRefusesExistingHistory()
    {
        var id=await Create();
        await using var db=database.CreateContext();
        var item=await db.Events.FindAsync(id); item!.Capacity=0;
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); db.ChangeTracker.Clear();
        item=await db.Events.FindAsync(id); item!.EndsAt=item.StartsAt;
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var migrator=db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        var error=await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>migrator.MigrateAsync("20261102233544_AddJoinRequestDecisions"));
        await migrator.MigrateAsync();
        Assert.Equal(51000,error.Number); Assert.True(await db.Events.AnyAsync(e=>e.Id==id));
    }

    private static EventForm Form() { var start=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime.Date.AddDays(5).AddHours(9); return new() {Title="Giao lưu <script>alert(1)</script>",Description="Nội dung sinh hoạt",Location="Hội trường",StartsAt=start,EndsAt=start.AddHours(2),RegistrationDeadline=start.AddDays(-1),Capacity=30}; }
    private async Task<int> Create(EventForm? f=null) { var r=await Save(f??Form()); Assert.True(r.Succeeded,r.Message); return r.Id!.Value; }
    private async Task<EventResult> Save(EventForm f,int? id=null,string? actor=null) { await using var s=app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<EventService>().SaveAsync(clubId,id,f,actor??manager.Id); }
    private async Task<EventResult> Transition(int id,string op,string? reason=null,string? actor=null,string? stamp=null) { stamp??=Convert.ToBase64String((await EventAsync(id)).RowVersion); await using var s=app.Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<EventService>().TransitionAsync(id,actor??manager.Id,new(){Operation=op,Reason=reason,RowVersion=stamp}); }
    private async Task<ClubEvent> EventAsync(int id) { await using var db=database.CreateContext(); return await db.Events.AsNoTracking().Include(e=>e.Club).SingleAsync(e=>e.Id==id); }
    private async Task<Person> UserAsync(string role=SystemRoles.Student) { await using var s=app.Services.CreateAsyncScope(); var users=s.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var tag=Guid.NewGuid().ToString("N"); var email=$"event-{tag}@example.test"; var password=$"Test-Only!9-{tag}"; var user=new ApplicationUser{UserName=email,Email=email,FullName="Người kiểm thử",StudentCode=tag[..24]}; Assert.True((await users.CreateAsync(user,password)).Succeeded); Assert.True((await users.AddToRoleAsync(user,role)).Succeeded); return new(user.Id,email,password); }
    private async Task<HttpClient> LoginAsync(Person p) { var c=app.BrowserClient(); var token=Extract(await c.GetStringAsync("/Account/Login"),"__RequestVerificationToken"); Assert.Equal(HttpStatusCode.Redirect,(await c.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",p.Email},{"Password",p.Password},{"__RequestVerificationToken",token}}))).StatusCode); return c; }
    private static string Extract(string html,string name) { var value=Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(value); return WebUtility.HtmlDecode(value); }
    private static Dictionary<string,string> Fields(EventForm f)=>new(){{"Title",f.Title},{"Description",f.Description??""},{"Location",f.Location},{"StartsAt",f.StartsAt!.Value.ToString("yyyy-MM-ddTHH:mm")},{"EndsAt",f.EndsAt!.Value.ToString("yyyy-MM-ddTHH:mm")},{"RegistrationDeadline",f.RegistrationDeadline!.Value.ToString("yyyy-MM-ddTHH:mm")},{"Capacity",f.Capacity.ToString()},{"Audience",((int)f.Audience).ToString()},{"RowVersion",f.RowVersion??""},{"Reason",f.Reason??""}};
    private record Person(string Id,string Email,string Password);
}
