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

public class EventNoticeTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app=null!;
    private Person manager=null!, student=null!, other=null!;
    private int clubId,eventId;
    public async Task InitializeAsync()
    {
        app=new(database.ConnectionString);await using var scope=app.Services.CreateAsyncScope();database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        manager=await User();student=await User();other=await User();
        await using var db=database.CreateContext();var club=new Club{Code=Guid.NewGuid().ToString("N")[..24],Name="CLB thông báo hoạt động"};
        db.Clubs.Add(club);await db.SaveChangesAsync();clubId=club.Id;
        db.Memberships.AddRange(new Membership{ClubId=clubId,UserId=manager.Id,Role=ClubRole.Manager},new Membership{ClubId=clubId,UserId=student.Id});
        var start=new DateTimeOffset(DateTime.UtcNow.Date.AddDays(4).AddHours(2),TimeSpan.Zero);
        var item=new ClubEvent{ClubId=clubId,Title="Workshop MVC",Location="Phòng A",Description="PRIVATE-EVENT-CONTENT",StartsAt=start,EndsAt=start.AddHours(2),RegistrationDeadline=start.AddDays(-1),Capacity=20,Status=EventStatus.Published,PublishedAt=DateTimeOffset.UtcNow};
        db.Events.Add(item);await db.SaveChangesAsync();eventId=item.Id;
        db.EventRegistrations.AddRange(new EventRegistration{EventId=eventId,UserId=student.Id},new EventRegistration{EventId=eventId,UserId=other.Id});await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()=>await app.DisposeAsync();

    [Fact]
    public async Task ScheduleIncludesBeforeAfterAndExactConfirmedRecipientsIncludingDisabledAccounts()
    {
        var cancelled=await User();await using var db=database.CreateContext();
        db.EventRegistrations.Add(new(){EventId=eventId,UserId=cancelled.Id,CancelledAt=DateTimeOffset.UtcNow.AddMinutes(1)});
        (await db.Users.FindAsync(other.Id))!.Status=UserStatus.Disabled;await db.SaveChangesAsync();
        var old=await Event();var form=EventForm.From(old);form.StartsAt=form.StartsAt!.Value.AddDays(1);form.EndsAt=form.EndsAt!.Value.AddDays(1);form.RegistrationDeadline=form.RegistrationDeadline!.Value.AddHours(2);form.Location="Phòng B <script>unsafe()</script>";form.Reason="Điều chỉnh theo lịch phòng";
        Assert.True((await Save(form)).Succeeded);var notice=await Notice();
        Assert.Equal(AnnouncementStatus.Published,notice.Status);Assert.Equal(64,notice.SourceKey!.Length);Assert.Equal(eventId,notice.EventId);
        foreach(var value in new[]{"Bắt đầu:","Kết thúc:","Hạn đăng ký:","Phòng A → Phòng B",old.StartsAt.Vietnam(),form.Reason,"Đăng ký của bạn được giữ nguyên"})Assert.Contains(value,notice.Content);
        Assert.Equal(new[]{student.Id,other.Id}.Order(),(await db.AnnouncementRecipients.Where(r=>r.AnnouncementId==notice.Id).Select(r=>r.UserId).ToListAsync()).Order());
        Assert.Equal(2,await db.EventRegistrations.CountAsync(r=>r.EventId==eventId&&r.CancelledAt==null));
        using var client=await Login(student);var html=await client.GetStringAsync($"/Announcements/Details/{notice.Id}");Assert.DoesNotContain("<script>unsafe()</script>",html);Assert.DoesNotContain("PRIVATE-EVENT-CONTENT",html);Assert.Contains($"href=\"/Events/Details/{eventId}\"",html);
        Assert.Contains($"data-inbox-id=\"{notice.Id}\"",await client.GetStringAsync("/Announcements/Mine"));
        (await db.Users.FindAsync(other.Id))!.Status=UserStatus.Active;await db.SaveChangesAsync();using var restored=await Login(other);Assert.Equal(HttpStatusCode.OK,(await restored.GetAsync($"/Announcements/Details/{notice.Id}")).StatusCode);
        Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.EntityType==nameof(Announcement)&&a.EntityId==notice.Id.ToString()&&a.Action=="EventNoticePublished"));
    }

    [Fact]
    public async Task CancelCapturesBeforeRegistrationsAreCancelledAndReplayKeepsOriginalSnapshotAndReadState()
    {
        var old=await Event();const string reason="Mưa lớn, hủy buổi sinh hoạt";
        Assert.True((await Cancel(old,reason)).Succeeded);var notice=await Notice();
        await using var db=database.CreateContext();Assert.Equal(0,await db.EventRegistrations.CountAsync(r=>r.EventId==eventId&&r.CancelledAt==null));
        Assert.Equal(2,await db.AnnouncementRecipients.CountAsync(r=>r.AnnouncementId==notice.Id));Assert.Contains(reason,notice.Content);Assert.Contains("đã bị hủy",notice.Content);Assert.Equal(EventStatus.Cancelled,(await Event()).Status);
        await using(var scope=app.Services.CreateAsyncScope())Assert.True((await scope.ServiceProvider.GetRequiredService<AnnouncementService>().MarkReadAsync(notice.Id,student.Id)).Succeeded);
        var firstRead=await db.AnnouncementRecipients.AsNoTracking().SingleAsync(r=>r.AnnouncementId==notice.Id&&r.UserId==student.Id);
        Assert.Equal("Conflict",(await Cancel(old,reason)).Code);
        Assert.False((await Cancel(await Event(),reason)).Succeeded);
        Assert.Equal(notice.SourceKey,(await Notice()).SourceKey);Assert.Equal(firstRead.ReadAt,(await db.AnnouncementRecipients.AsNoTracking().SingleAsync(r=>r.AnnouncementId==notice.Id&&r.UserId==student.Id)).ReadAt);
        using var client=await Login(student);Assert.Contains("Hủy hoạt động",WebUtility.HtmlDecode(await client.GetStringAsync("/Announcements/Mine")));
        Assert.Equal(2,await db.AuditLogs.CountAsync(a=>a.Action=="EventRegistrationCancelled"&&a.Reason==reason));
    }

    [Fact]
    public async Task ConcurrentScheduleCommitsOneNoticeAndLaterChangesHaveNewKeysAndNewSnapshots()
    {
        var form=EventForm.From(await Event());form.Location="Phòng B";form.Reason="Đổi phòng";
        var results=await Task.WhenAll(Save(form),Save(form));Assert.Single(results,r=>r.Succeeded);Assert.Single(results,r=>r.Code=="Conflict");var first=await Notice();
        await using(var scope=app.Services.CreateAsyncScope())
        {
            var item=await Event();var result=await scope.ServiceProvider.GetRequiredService<EventRegistrationService>().RegisterAsync(eventId,manager.Id,new(){EventVersion=Convert.ToBase64String(item.RowVersion)});Assert.True(result.Succeeded,result.Message);
        }
        Assert.Equal("Conflict",(await Save(form)).Code);await using var db=database.CreateContext();Assert.Equal(2,await db.AnnouncementRecipients.CountAsync(r=>r.AnnouncementId==first.Id));
        var next=EventForm.From(await Event());next.Location="Phòng C";next.Reason="Đổi phòng lần hai";Assert.True((await Save(next)).Succeeded);
        var notices=await db.Announcements.Where(a=>a.EventId==eventId&&a.SourceKey!=null).OrderBy(a=>a.Id).ToListAsync();Assert.Equal(2,notices.Count);Assert.NotEqual(notices[0].SourceKey,notices[1].SourceKey);
        Assert.Equal(3,await db.AnnouncementRecipients.CountAsync(r=>r.AnnouncementId==notices[1].Id));Assert.Contains("Phòng A → Phòng B",notices[0].Content);Assert.Contains("Phòng B → Phòng C",notices[1].Content);
    }

    [Theory]
    [InlineData("DraftEdit")][InlineData("DraftCancel")][InlineData("Publish")][InlineData("Complete")][InlineData("Title")][InlineData("Description")][InlineData("Capacity")][InlineData("NoChange")]
    public async Task NonScheduleOperationsDoNotSendEventNotices(string operation)
    {
        await using var db=database.CreateContext();var item=(await db.Events.FindAsync(eventId))!;
        if(operation is "DraftEdit" or "DraftCancel" or "Publish"){item.Status=EventStatus.Draft;item.PublishedAt=null;await db.SaveChangesAsync();}
        if(operation=="Complete"){item.StartsAt=DateTimeOffset.UtcNow.AddHours(-3);item.EndsAt=DateTimeOffset.UtcNow.AddHours(-1);item.RegistrationDeadline=item.StartsAt.AddHours(-1);await db.SaveChangesAsync();}
        if(operation is "DraftCancel" or "Publish" or "Complete")Assert.True((await Transition(await Event(),operation=="DraftCancel"?"Cancel":operation,"Thay đổi kế hoạch")).Succeeded);
        else
        {
            var form=EventForm.From(await Event());form.Reason="Cập nhật thông tin";
            switch(operation){case "Title":form.Title="Tên mới";break;case "Description":form.Description="Nội dung mới";break;case "Capacity":form.Capacity=21;break;case "DraftEdit":form.Location="Phòng khác";break;}
            Assert.True((await Save(form)).Succeeded);
        }
        Assert.False(await db.Announcements.AnyAsync(a=>a.EventId==eventId));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ZeroRegistrationsStillAllowChangeAndKeepSourceRecord(bool cancel)
    {
        await using var db=database.CreateContext();await db.EventRegistrations.Where(r=>r.EventId==eventId).ExecuteDeleteAsync();
        if(cancel)Assert.True((await Cancel(await Event())).Succeeded);
        else{var form=EventForm.From(await Event());form.Location="Phòng khác";form.Reason="Đổi phòng";Assert.True((await Save(form)).Succeeded);}
        var notice=await Notice();Assert.False(await db.AnnouncementRecipients.AnyAsync(r=>r.AnnouncementId==notice.Id));
        using var client=await Login(manager);Assert.Contains("0 người nhận",WebUtility.HtmlDecode(await client.GetStringAsync($"/Events/Details/{eventId}")));
    }

    [Theory]
    [InlineData("Schedule","Notice")][InlineData("Cancel","Recipient")][InlineData("Schedule","NoticeAudit")][InlineData("Cancel","NoticeAudit")][InlineData("Schedule","EventAudit")][InlineData("Cancel","EventAudit")][InlineData("Cancel","RegistrationAudit")]
    public async Task AnyWriteFailureRollsBackEventRegistrationsNoticeAndRecipients(string operation,string failure)
    {
        var old=await Event();await using var db=database.CreateContext();
        var sql=failure switch
        {
            "Notice"=>"CREATE TRIGGER RejectEventNotice ON Announcements AFTER INSERT AS BEGIN THROW 51001,'Test notice failure',1; END",
            "Recipient"=>"CREATE TRIGGER RejectEventNotice ON AnnouncementRecipients AFTER INSERT AS BEGIN THROW 51001,'Test recipient failure',1; END",
            "NoticeAudit"=>"CREATE TRIGGER RejectEventNotice ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='EventNoticePublished') THROW 51001,'Test notice audit failure',1; END",
            "EventAudit"=>"CREATE TRIGGER RejectEventNotice ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityType='ClubEvent') THROW 51001,'Test event audit failure',1; END",
            _=>"CREATE TRIGGER RejectEventNotice ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action='EventRegistrationCancelled') THROW 51001,'Test registration audit failure',1; END"
        };
        await db.Database.ExecuteSqlRawAsync(sql);
        try
        {
            if(operation=="Cancel")await Assert.ThrowsAsync<DbUpdateException>(()=>Cancel(old));
            else{var form=EventForm.From(old);form.Location="Phòng mới";form.Reason="Đổi địa điểm";await Assert.ThrowsAsync<DbUpdateException>(()=>Save(form));}
            var current=await Event();Assert.Equal(old.Status,current.Status);Assert.Equal(old.Location,current.Location);Assert.Equal(old.RowVersion,current.RowVersion);
            Assert.Equal(2,await db.EventRegistrations.CountAsync(r=>r.EventId==eventId&&r.CancelledAt==null));Assert.False(await db.Announcements.AnyAsync(a=>a.EventId==eventId));
            Assert.False(await db.AuditLogs.AnyAsync(a=>a.EntityType==nameof(ClubEvent)&&a.EntityId==eventId.ToString()));
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectEventNotice");}
        // Retry đúng form cũ sau rollback thành công, không cần tạo snapshot từ dữ liệu đã hủy.
        Assert.True((await Cancel(old)).Succeeded);Assert.Equal(2,await db.AnnouncementRecipients.CountAsync(r=>r.Announcement.EventId==eventId));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task CancellationRacingJoinOrWithdrawalSnapshotsTheWinnerOrder(bool withdraw)
    {
        var old=await Event();async Task<WorkflowResult> Registration()
        {
            await using var scope=app.Services.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<EventRegistrationService>();
            if(!withdraw)return await service.RegisterAsync(eventId,manager.Id,new(){EventVersion=Convert.ToBase64String(old.RowVersion)});
            await using var db=database.CreateContext();var row=await db.EventRegistrations.AsNoTracking().SingleAsync(r=>r.EventId==eventId&&r.UserId==student.Id);
            return await service.CancelAsync(row.Id,student.Id,Convert.ToBase64String(row.RowVersion));
        }
        var change=Registration();var cancel=Cancel(old);await Task.WhenAll(change,cancel);Assert.True((await cancel).Succeeded);
        var result=await change;var notice=await Notice();await using var verify=database.CreateContext();
        Assert.Equal(2+(result.Succeeded?(withdraw?-1:1):0),await verify.AnnouncementRecipients.CountAsync(r=>r.AnnouncementId==notice.Id));
        Assert.False(await verify.EventRegistrations.AnyAsync(r=>r.EventId==eventId&&r.CancelledAt==null));
    }

    [Fact]
    public async Task InvalidInputRevokedManagerAndForeignClubNeverCreateNotices()
    {
        var form=EventForm.From(await Event());form.Location="Phòng B";
        Assert.Equal("Validation",(await Save(form)).Code);form.Reason="Đổi phòng";form.Capacity=1;Assert.Equal("Validation",(await Save(form)).Code);
        form.Capacity=20;Assert.Equal("NotFound",(await Save(form,other.Id)).Code);
        await using var db=database.CreateContext();(await db.Memberships.SingleAsync(m=>m.ClubId==clubId&&m.UserId==manager.Id)).Role=ClubRole.Member;await db.SaveChangesAsync();
        Assert.Equal("NotFound",(await Save(form)).Code);Assert.Equal("NotFound",(await Cancel(await Event())).Code);Assert.False(await db.Announcements.AnyAsync(a=>a.EventId==eventId));
        var admin=await User(SystemRoles.Admin);Assert.True((await Save(form,admin.Id)).Succeeded);Assert.NotNull((await Notice()).SourceKey);
    }

    [Fact]
    public async Task InternalRecipientKeepsNoticeAfterLeavingWithoutRegainingPrivateEventContent()
    {
        await using var db=database.CreateContext();(await db.Events.FindAsync(eventId))!.Audience=EventAudience.Club;await db.SaveChangesAsync();
        Assert.True((await Cancel(await Event())).Succeeded);var notice=await Notice();
        var member=await db.Memberships.SingleAsync(m=>m.ClubId==clubId&&m.UserId==student.Id);member.Status=MembershipStatus.Left;member.EndedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();
        using var client=await Login(student);var html=await client.GetStringAsync($"/Announcements/Details/{notice.Id}");
        Assert.Contains("Workshop MVC",html);Assert.DoesNotContain($"href=\"/Events/Details/{eventId}\"",html);Assert.DoesNotContain("PRIVATE-EVENT-CONTENT",html);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Events/Details/{eventId}")).StatusCode);
        var late=await User();using var outsider=await Login(late);Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/Announcements/Details/{notice.Id}")).StatusCode);
    }

    [Fact]
    public async Task SourceKeyConstraintsPreventDuplicatesAndLosingHistory()
    {
        Assert.True((await Cancel(await Event())).Succeeded);var notice=await Notice();await using var db=database.CreateContext();
        var copy=new Announcement{ClubId=clubId,EventId=eventId,SourceKey=notice.SourceKey,Title="Bản trùng",Content="Test",Status=AnnouncementStatus.Published,CreatedAt=notice.CreatedAt,PublishedAt=notice.PublishedAt};
        db.Announcements.Add(copy);await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        copy.SourceKey=new string('A',64);copy.EventId=null;db.Announcements.Add(copy);await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        var error=await Assert.ThrowsAsync<SqlException>(()=>db.GetService<IMigrator>().MigrateAsync("20261111011414_AddAnnouncementReadAt"));Assert.Equal(51000,error.Number);Assert.Equal(notice.SourceKey,(await Notice()).SourceKey);Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task MaxLengthInputsRemainWithinNoticeLimits()
    {
        await using var db=database.CreateContext();var item=(await db.Events.FindAsync(eventId))!;item.Title=new string('T',159)+"😀"+new string('T',19);item.Location=new string('A',250);await db.SaveChangesAsync();
        var form=EventForm.From(await Event());form.Location=new string('B',250);form.Reason=new string('L',500);form.StartsAt=form.StartsAt!.Value.AddDays(1);form.EndsAt=form.EndsAt!.Value.AddDays(1);form.RegistrationDeadline=form.RegistrationDeadline!.Value.AddHours(1);
        Assert.True((await Save(form)).Succeeded);var notice=await Notice();Assert.InRange(notice.Title.Length,1,180);Assert.InRange(notice.Content.Length,1,4000);Assert.DoesNotContain('\uFFFD',notice.Title);
    }

    private async Task<ClubEvent> Event(){await using var db=database.CreateContext();return await db.Events.AsNoTracking().Include(e=>e.Club).SingleAsync(e=>e.Id==eventId);}
    private async Task<Announcement> Notice(){await using var db=database.CreateContext();return await db.Announcements.AsNoTracking().SingleAsync(a=>a.EventId==eventId&&a.SourceKey!=null);}
    private async Task<EventResult> Save(EventForm form,string? actor=null){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<EventService>().SaveAsync(clubId,eventId,form,actor??manager.Id);}
    private Task<EventResult> Cancel(ClubEvent item,string reason="Thay đổi kế hoạch")=>Transition(item,"Cancel",reason);
    private async Task<EventResult> Transition(ClubEvent item,string operation,string reason){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<EventService>().TransitionAsync(eventId,manager.Id,new(){Operation=operation,Reason=reason,RowVersion=Convert.ToBase64String(item.RowVersion)});}
    private async Task<Person> User(string role=SystemRoles.Student){await using var scope=app.Services.CreateAsyncScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var tag=Guid.NewGuid().ToString("N");var email=$"eventnotice-{tag}@example.test";var password=$"Test-Only!9-{tag}";var user=new ApplicationUser{UserName=email,Email=email,FullName="Người kiểm thử",StudentCode=tag[..24]};Assert.True((await users.CreateAsync(user,password)).Succeeded);Assert.True((await users.AddToRoleAsync(user,role)).Succeeded);return new(user.Id,email,password);}
    private async Task<HttpClient> Login(Person person){var client=app.BrowserClient();var token=Regex.Match(await client.GetStringAsync("/Account/Login"),"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;Assert.NotEmpty(token);Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",person.Email},{"Password",person.Password},{"__RequestVerificationToken",WebUtility.HtmlDecode(token)}}))).StatusCode);return client;}
    private record Person(string Id,string Email,string Password);
}
