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
using StudentClubManagement.Web.ViewModels.Announcements;

namespace StudentClubManagement.IntegrationTests;

public class AnnouncementTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person manager = null!, member = null!, outsider = null!;
    private int clubId, eventId;

    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        manager = await User(); member = await User(); outsider = await User();
        await using var db = database.CreateContext();
        var club = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "CLB thông báo kiểm thử" };
        db.Clubs.Add(club); await db.SaveChangesAsync(); clubId = club.Id;
        db.Memberships.AddRange(new Membership { ClubId = clubId, UserId = manager.Id, Role = ClubRole.Manager }, new Membership { ClubId = clubId, UserId = member.Id });
        var now = DateTimeOffset.UtcNow;
        var item = new ClubEvent { ClubId = clubId, Title = "Sự kiện nhận thông báo", Location = "Hội trường", StartsAt = now.AddDays(3), EndsAt = now.AddDays(3).AddHours(2), RegistrationDeadline = now.AddDays(2), Capacity = 30, Status = EventStatus.Published, PublishedAt = now };
        db.Events.Add(item); await db.SaveChangesAsync(); eventId = item.Id;
        db.EventRegistrations.AddRange(new EventRegistration { EventId = eventId, UserId = member.Id }, new EventRegistration { EventId = eventId, UserId = outsider.Id });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task HttpDraftPublishArchiveProtectsFieldsAndEncodesContent()
    {
        using var client = await Login(manager);
        var html = await client.GetStringAsync($"/Announcements/Create?clubId={clubId}");
        var fields = new Dictionary<string,string> { ["Title"] = "Thông báo <script>unsafe()</script>", ["Content"] = "Nội dung\n<img src=x onerror=unsafe()>", ["Status"] = "2", ["PublishedAt"] = "2000-01-01", ["Club.Id"] = "9999", ["__RequestVerificationToken"] = Extract(html, "__RequestVerificationToken") };
        var response = await client.PostAsync($"/Announcements/Create?clubId={clubId}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = int.Parse(response.Headers.Location!.OriginalString.Split('/').Last());
        var item = await Item(id); Assert.Equal(AnnouncementStatus.Draft, item.Status); Assert.Null(item.PublishedAt); Assert.Equal(clubId, item.ClubId);
        html = await client.GetStringAsync($"/Announcements/Details/{id}");
        Assert.DoesNotContain("<script>unsafe()</script>", html); Assert.DoesNotContain("<img src=x", html);
        var post = new Dictionary<string,string> { ["RowVersion"] = Extract(html, "RowVersion"), ["RecipientVersion"] = Extract(html, "RecipientVersion"), ["Operation"] = "Publish", ["__RequestVerificationToken"] = Extract(html, "__RequestVerificationToken"), ["UserIds"] = outsider.Id };
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/Announcements/ChangeStatus/{id}", new FormUrlEncodedContent(post))).StatusCode);
        Assert.Equal(AnnouncementStatus.Published, (await Item(id)).Status);
        await using var db = database.CreateContext();
        Assert.Equal(2, await db.AnnouncementRecipients.CountAsync(r => r.AnnouncementId == id));
        Assert.False(await db.AnnouncementRecipients.AnyAsync(r => r.AnnouncementId == id && r.UserId == outsider.Id));
        Assert.Equal("Validation", (await Change(id, "Archive")).Code);
        Assert.True((await Change(id, "Archive", reason: "Hết hiệu lực <script>unsafe()</script>")).Succeeded);
        Assert.Equal(AnnouncementStatus.Archived, (await Item(id)).Status);
        Assert.False((await Change(id, "Publish")).Succeeded);
        Assert.False((await Save(Form(), id)).Succeeded);
        html = await client.GetStringAsync($"/Announcements/Details/{id}"); Assert.DoesNotContain("<script>unsafe()</script>", html);
        Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.EntityType == nameof(Announcement) && a.EntityId == id.ToString()));
    }

    [Fact]
    public async Task DraftAndRecipientListArePrivateAndPublishedRecipientCannotReadAuditOrOtherRecipients()
    {
        var id = await Create(); using var student = await Login(member); using var other = await Login(outsider); using var guest = app.BrowserClient();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.GetAsync($"/Announcements/Details/{id}")).StatusCode);
        foreach (var client in new[] { student, other })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Announcements/Details/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Announcements?clubId={clubId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Announcements/Create?clubId={clubId}")).StatusCode);
        }
        Assert.True((await Change(id, "Publish")).Succeeded);
        var response = await student.GetAsync($"/Announcements/Details/{id}"); Assert.True(response.Headers.CacheControl!.NoStore);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(Form().Content, html);
        foreach (var hidden in new[] { "Xem người nhận", "Lịch sử xử lý", manager.Email, manager.Id, "RecipientCount", "ContentHash" }) Assert.DoesNotContain(hidden, html);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/Announcements/Recipients/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/Announcements/Edit/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/Announcements/Details/{id}")).StatusCode);
        Assert.True((await Change(id, "Archive", reason: "Nội dung hết hạn")).Succeeded);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/Announcements/Details/{id}")).StatusCode);
    }

    [Fact]
    public async Task ClubSnapshotExcludesInactiveAccountsAndMembershipsAndSurvivesDeparture()
    {
        var suspended = await User(); var disabled = await User(); var left = await User(); var joinedLater = await User();
        await using var db = database.CreateContext();
        db.Memberships.AddRange(new Membership { ClubId = clubId, UserId = suspended.Id, Status = MembershipStatus.Suspended },
            new Membership { ClubId = clubId, UserId = disabled.Id }, new Membership { ClubId = clubId, UserId = left.Id, Status = MembershipStatus.Left, EndedAt = DateTimeOffset.UtcNow.AddMinutes(1) });
        (await db.Users.FindAsync(disabled.Id))!.Status = UserStatus.Disabled; await db.SaveChangesAsync();
        var id = await Create(); Assert.True((await Change(id, "Publish")).Succeeded);
        var membership = await db.Memberships.SingleAsync(m => m.ClubId == clubId && m.UserId == member.Id);
        membership.Status = MembershipStatus.Left; membership.EndedAt = DateTimeOffset.UtcNow;
        db.Memberships.Add(new Membership { ClubId = clubId, UserId = joinedLater.Id }); await db.SaveChangesAsync();
        var ids = await db.AnnouncementRecipients.Where(r => r.AnnouncementId == id).Select(r => r.UserId).ToListAsync();
        Assert.Equal(new[] { manager.Id, member.Id }.Order(), ids.Order());
        using var client = await Login(member); using var late = await Login(joinedLater);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Announcements/Details/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await late.GetAsync($"/Announcements/Details/{id}")).StatusCode);
        (await db.Clubs.FindAsync(clubId))!.Status = ClubStatus.Archived; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Announcements/Details/{id}")).StatusCode);
    }

    [Fact]
    public async Task EventSnapshotIncludesRegisteredOutsiderButNotCancelledOrDisabledAndNeverChanges()
    {
        var cancelled = await User(); var disabled = await User();
        await using var db = database.CreateContext();
        db.EventRegistrations.AddRange(new EventRegistration { EventId = eventId, UserId = cancelled.Id, CancelledAt = DateTimeOffset.UtcNow.AddMinutes(1) }, new EventRegistration { EventId = eventId, UserId = disabled.Id });
        (await db.Users.FindAsync(disabled.Id))!.Status = UserStatus.Disabled; await db.SaveChangesAsync();
        var id = await Create(Form(eventId)); Assert.True((await Change(id, "Publish")).Succeeded);
        Assert.Equal(new[] { member.Id, outsider.Id }.Order(), (await db.AnnouncementRecipients.Where(r => r.AnnouncementId == id).Select(r => r.UserId).ToListAsync()).Order());
        (await db.EventRegistrations.SingleAsync(r => r.EventId == eventId && r.UserId == outsider.Id)).CancelledAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); using var client = await Login(outsider);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Announcements/Details/{id}")).StatusCode);
        Assert.Equal(2, await db.AnnouncementRecipients.CountAsync(r => r.AnnouncementId == id));
    }

    [Fact]
    public async Task PreviewDetectsRecipientReplacementEvenWhenCountIsUnchanged()
    {
        var id = await Create(); var form = await Action(id, "Publish");
        await using var db = database.CreateContext();
        (await db.Memberships.SingleAsync(m => m.ClubId == clubId && m.UserId == member.Id)).Status = MembershipStatus.Suspended;
        db.Memberships.Add(new Membership { ClubId = clubId, UserId = outsider.Id }); await db.SaveChangesAsync();
        Assert.Equal("Conflict", (await Transition(id, form)).Code); Assert.Equal(AnnouncementStatus.Draft, (await Item(id)).Status);
        Assert.False(await db.AnnouncementRecipients.AnyAsync(r => r.AnnouncementId == id));
        Assert.True((await Change(id, "Publish")).Succeeded);
        Assert.True(await db.AnnouncementRecipients.AnyAsync(r => r.AnnouncementId == id && r.UserId == outsider.Id));
    }

    [Fact]
    public async Task TwoPublishRequestsCommitOnceAndRejectStaleEdit()
    {
        var id = await Create(); var item = await Item(id); var action = await Action(id, "Publish");
        var results = await Task.WhenAll(Transition(id, action), Transition(id, action));
        Assert.Single(results, r => r.Succeeded); Assert.Single(results, r => r.Code == "Conflict");
        var edit = Form(); edit.RowVersion = Convert.ToBase64String(item.RowVersion);
        Assert.Equal("Conflict", (await Save(edit, id)).Code);
        await using var db = database.CreateContext();
        Assert.Equal(2, await db.AnnouncementRecipients.CountAsync(r => r.AnnouncementId == id));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "AnnouncementPublished" && a.EntityId == id.ToString()));
    }

    [Fact]
    public async Task EmptyRecipientsKeepDraftAndNoPublicationAudit()
    {
        await using var db = database.CreateContext(); await db.EventRegistrations.Where(r => r.EventId == eventId).ExecuteDeleteAsync();
        var id = await Create(Form(eventId)); Assert.Equal("Validation", (await Change(id, "Publish")).Code);
        Assert.Null((await Item(id)).PublishedAt); Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "AnnouncementPublished" && a.EntityId == id.ToString()));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PublishRacingMembershipDepartureOrEventCancellationKeepsOneConsistentSnapshot(bool cancelEvent)
    {
        var id = await Create(Form(cancelEvent ? eventId : null)); var form = await Action(id, "Publish");
        async Task Mutate()
        {
            await using var scope = app.Services.CreateAsyncScope(); await using var db = database.CreateContext();
            if (cancelEvent)
            {
                var item = await db.Events.SingleAsync(e => e.Id == eventId);
                var result = await scope.ServiceProvider.GetRequiredService<EventService>().TransitionAsync(eventId, manager.Id,
                    new() { Operation = "Cancel", Reason = "Hủy kế hoạch", RowVersion = Convert.ToBase64String(item.RowVersion) });
                Assert.True(result.Succeeded, result.Message);
            }
            else
            {
                var item = await db.Memberships.SingleAsync(m => m.ClubId == clubId && m.UserId == member.Id);
                var result = await scope.ServiceProvider.GetRequiredService<MembershipService>().ChangeStatusAsync(item.Id, manager.Id,
                    Convert.ToBase64String(item.RowVersion), MembershipStatus.Left, "Rời CLB");
                Assert.True(result.Succeeded, result.Message);
            }
        }
        var mutation = Mutate(); var publish = Transition(id, form); await Task.WhenAll(mutation, publish);
        var result = await publish; await using var verify = database.CreateContext();
        Assert.Equal(result.Succeeded ? 2 : 0, await verify.AnnouncementRecipients.CountAsync(r => r.AnnouncementId == id));
        Assert.Equal(result.Succeeded ? AnnouncementStatus.Published : AnnouncementStatus.Draft, (await Item(id)).Status);
        if (!result.Succeeded) Assert.Contains(result.Code, new[] { "Conflict", "Validation" });
        if (cancelEvent) Assert.False(await verify.EventRegistrations.AnyAsync(r => r.EventId == eventId && r.CancelledAt == null));
    }

    [Theory]
    [InlineData("BlankTitle")][InlineData("LongTitle")][InlineData("ControlTitle")][InlineData("BlankContent")][InlineData("LongContent")]
    public async Task InvalidInputCannotSave(string kind)
    {
        var form = Form();
        switch (kind) { case "BlankTitle": form.Title = " "; break; case "LongTitle": form.Title = new('x',181); break; case "ControlTitle": form.Title = "A\nB"; break; case "BlankContent": form.Content = "\n "; break; case "LongContent": form.Content = new('x',4001); break; }
        Assert.Equal("Validation", (await Save(form)).Code);
        await using var db = database.CreateContext(); Assert.False(await db.Announcements.AnyAsync(a => a.ClubId == clubId));
    }

    [Theory]
    [InlineData(ClubStatus.Suspended)][InlineData(ClubStatus.Archived)]
    public async Task InactiveClubCannotSaveOrPublish(ClubStatus status)
    {
        var id = await Create(); await using var db = database.CreateContext();
        (await db.Clubs.FindAsync(clubId))!.Status = status; await db.SaveChangesAsync();
        Assert.Equal("Validation", (await Save(Form())).Code); Assert.Equal("Validation", (await Change(id,"Publish")).Code);
    }

    [Theory]
    [InlineData("Draft")][InlineData("Cancelled")][InlineData("WrongClub")][InlineData("Missing")]
    public async Task InvalidEventCannotBeChosenOrPublished(string kind)
    {
        var id = await Create(Form(eventId)); await using var db = database.CreateContext(); var e = (await db.Events.FindAsync(eventId))!;
        var target = eventId;
        switch (kind)
        {
            case "Draft": e.Status = EventStatus.Draft; e.PublishedAt = null; break;
            case "Cancelled": e.Status = EventStatus.Cancelled; break;
            case "WrongClub": var club = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "CLB khác" }; db.Clubs.Add(club); await db.SaveChangesAsync(); e.ClubId = club.Id; break;
            case "Missing": target = int.MaxValue; break;
        }
        await db.SaveChangesAsync();
        Assert.Equal("Validation", (await Save(Form(target))).Code);
        if (kind != "Missing") Assert.Equal("Validation", (await Change(id,"Publish")).Code);
    }

    [Fact]
    public async Task RevokedManagerCannotPostOldFormAndActiveAdminCanManage()
    {
        var id = await Create(); var action = await Action(id,"Publish"); using var client = await Login(manager);
        var html = await client.GetStringAsync($"/Announcements/Details/{id}");
        await using var db = database.CreateContext();
        (await db.Memberships.SingleAsync(m => m.ClubId == clubId && m.UserId == manager.Id)).Role = ClubRole.Member; await db.SaveChangesAsync();
        Assert.Equal("NotFound", (await Transition(id,action)).Code);
        var fields = new Dictionary<string,string> { ["RowVersion"] = action.RowVersion, ["RecipientVersion"] = action.RecipientVersion!, ["Operation"] = "Publish", ["__RequestVerificationToken"] = Extract(html,"__RequestVerificationToken") };
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync($"/Announcements/ChangeStatus/{id}",new FormUrlEncodedContent(fields))).StatusCode);
        var admin = await User(SystemRoles.Admin); Assert.True((await Transition(id,action,admin.Id)).Succeeded);
        Assert.Equal("NotFound", (await Save(Form(),actor:outsider.Id)).Code);
        (await db.Users.FindAsync(admin.Id))!.Status = UserStatus.Disabled; await db.SaveChangesAsync();
        Assert.Equal("NotFound", (await Transition(id,await Action(id,"Archive"),admin.Id)).Code);
    }

    [Fact]
    public async Task CsrfInvalidFiltersAndInvalidModelAreRejected()
    {
        var id = await Create(); using var client = await Login(manager);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync($"/Announcements/ChangeStatus/{id}",new FormUrlEncodedContent(new Dictionary<string,string>{{"Operation","Publish"}}))).StatusCode);
        foreach(var query in new[]{"status=99","page=abc","clubId=abc","search="+new string('a',181)})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/Announcements?"+query)).StatusCode);
        var html = await client.GetStringAsync($"/Announcements/Create?clubId={clubId}");
        var response = await client.PostAsync($"/Announcements/Create?clubId={clubId}",new FormUrlEncodedContent(new Dictionary<string,string>{{"Title","Valid"},{"Content","Valid"},{"EventId","NaN"},{"__RequestVerificationToken",Extract(html,"__RequestVerificationToken")}}));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        await using var db=database.CreateContext(); Assert.Equal(1,await db.Announcements.CountAsync(a=>a.ClubId==clubId));
    }

    [Theory]
    [InlineData("Create")][InlineData("Publish")][InlineData("Archive")]
    public async Task AuditFailureRollsBackContentRecipientsAndState(string phase)
    {
        var id = phase == "Create" ? 0 : await Create();
        if(phase=="Archive")Assert.True((await Change(id,"Publish")).Succeeded);
        await using var db=database.CreateContext();
        var sql=phase switch
        {
            "Create"=>"CREATE TRIGGER RejectAnnouncementAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='AnnouncementCreated') THROW 51001,'Test audit failure',1; END",
            "Publish"=>"CREATE TRIGGER RejectAnnouncementAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='AnnouncementPublished') THROW 51001,'Test audit failure',1; END",
            _=>"CREATE TRIGGER RejectAnnouncementAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='AnnouncementArchived') THROW 51001,'Test audit failure',1; END"
        };
        await db.Database.ExecuteSqlRawAsync(sql);
        try
        {
            if(phase=="Create") { await Assert.ThrowsAsync<DbUpdateException>(()=>Save(Form())); Assert.False(await db.Announcements.AnyAsync(a=>a.ClubId==clubId)); }
            else
            {
                await Assert.ThrowsAsync<DbUpdateException>(()=>Change(id,phase,reason:"Kiểm thử rollback"));
                Assert.Equal(phase=="Publish"?AnnouncementStatus.Draft:AnnouncementStatus.Published,(await Item(id)).Status);
                Assert.Equal(phase=="Publish"?0:2,await db.AnnouncementRecipients.CountAsync(r=>r.AnnouncementId==id));
            }
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectAnnouncementAudit"); }
    }

    [Fact]
    public async Task ListsFilterAndPaginateAndRecipientNamesAreEncoded()
    {
        for(var i=0;i<11;i++) { var form=Form(); form.Title=$"NOTICE-{i:D2}"; await Create(form); }
        var id=await Create();Assert.True((await Change(id,"Publish")).Succeeded);
        using var client=await Login(manager);
        Assert.Equal(10,Regex.Matches(await client.GetStringAsync($"/Announcements?clubId={clubId}"),"Chi tiết thông báo").Count);
        Assert.Equal(2,Regex.Matches(await client.GetStringAsync($"/Announcements?clubId={clubId}&page=2"),"Chi tiết thông báo").Count);
        Assert.Single(Regex.Matches(await client.GetStringAsync($"/Announcements?clubId={clubId}&status=2"),"Chi tiết thông báo"));
        Assert.Single(Regex.Matches(await client.GetStringAsync($"/Announcements?clubId={clubId}&search=NOTICE-03"),"Chi tiết thông báo"));
        await using var db=database.CreateContext();
        (await db.Users.FindAsync(member.Id))!.FullName="<script>member()</script>";await db.SaveChangesAsync();
        var html=await client.GetStringAsync($"/Announcements/Recipients/{id}");Assert.DoesNotContain("<script>member()</script>",html);
        Assert.DoesNotContain(member.Email,html);Assert.DoesNotContain(member.Id,html);Assert.Equal(2,Regex.Matches(html,"data-recipient").Count);
    }

    [Fact]
    public async Task SqlRejectsInvalidStatesDuplicatesAndDestructiveRollback()
    {
        var id=await Create();await using var db=database.CreateContext();
        var item=(await db.Announcements.FindAsync(id))!;item.Status=AnnouncementStatus.Published;
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        item=(await db.Announcements.FindAsync(id))!;item.Status=(AnnouncementStatus)99;
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        Assert.True((await Change(id,"Publish")).Succeeded);
        db.AnnouncementRecipients.Add(new(){AnnouncementId=id,UserId=member.Id});
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        var error=await Assert.ThrowsAsync<SqlException>(()=>db.GetService<IMigrator>().MigrateAsync("20261110120624_FinalizeAttendance"));Assert.Equal(51000,error.Number);
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Equal(2,await db.AnnouncementRecipients.CountAsync(r=>r.AnnouncementId==id));Assert.False(db.Database.HasPendingModelChanges());
    }

    private static AnnouncementForm Form(int? target=null)=>new(){Title="Thông báo sinh hoạt",Content="Chuẩn bị nội dung và tham gia đúng giờ.",EventId=target};
    private async Task<int> Create(AnnouncementForm? form=null){var result=await Save(form??Form());Assert.True(result.Succeeded,result.Message);return result.Id!.Value;}
    private async Task<WorkflowResult> Save(AnnouncementForm form,int? id=null,string? actor=null){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<AnnouncementService>().SaveAsync(clubId,id,form,actor??manager.Id);}
    private async Task<Announcement> Item(int id){await using var db=database.CreateContext();return await db.Announcements.AsNoTracking().SingleAsync(a=>a.Id==id);}
    private async Task<AnnouncementActionForm> Action(int id,string operation,string? reason=null){var item=await Item(id);await using var scope=app.Services.CreateAsyncScope();var preview=await scope.ServiceProvider.GetRequiredService<AnnouncementService>().PreviewAsync(item);return new(){Operation=operation,Reason=reason,RowVersion=Convert.ToBase64String(item.RowVersion),RecipientVersion=preview.Version};}
    private async Task<WorkflowResult> Change(int id,string operation,string? reason=null)=>await Transition(id,await Action(id,operation,reason));
    private async Task<WorkflowResult> Transition(int id,AnnouncementActionForm form,string? actor=null){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<AnnouncementService>().TransitionAsync(id,actor??manager.Id,form);}
    private async Task<Person> User(string role=SystemRoles.Student){await using var scope=app.Services.CreateAsyncScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var tag=Guid.NewGuid().ToString("N");var email=$"notice-{tag}@example.test";var password=$"Test-Only!9-{tag}";var user=new ApplicationUser{UserName=email,Email=email,FullName="Người kiểm thử",StudentCode=tag[..24]};Assert.True((await users.CreateAsync(user,password)).Succeeded);Assert.True((await users.AddToRoleAsync(user,role)).Succeeded);return new(user.Id,email,password);}
    private async Task<HttpClient> Login(Person person){var client=app.BrowserClient();var token=Extract(await client.GetStringAsync("/Account/Login"),"__RequestVerificationToken");Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",person.Email},{"Password",person.Password},{"__RequestVerificationToken",token}}))).StatusCode);return client;}
    private static string Extract(string html,string name){var value=Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;Assert.NotEmpty(value);return WebUtility.HtmlDecode(value);}
    private record Person(string Id,string Email,string Password);
}
