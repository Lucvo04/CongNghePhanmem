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

namespace StudentClubManagement.IntegrationTests;

public class AnnouncementInboxTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private Person manager = null!, student = null!, other = null!;
    private int clubId;
    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope(); database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        manager = await User(); student = await User(); other = await User();
        await using var db = database.CreateContext();
        var club = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "CLB hộp thông báo" };
        db.Clubs.Add(club); await db.SaveChangesAsync(); clubId = club.Id;
        db.Memberships.AddRange(new Membership { ClubId = clubId, UserId = manager.Id, Role = ClubRole.Manager }, new Membership { ClubId = clubId, UserId = student.Id });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task HttpReadIsExplicitIdempotentUsesServerIdentityAndPreservesReturnFilters()
    {
        var id = await Notice(); var second = await Notice("Thông báo thứ hai");
        using var client = await Login(student);
        var list = await client.GetAsync("/Announcements/Mine"); Assert.Equal(HttpStatusCode.OK,list.StatusCode); Assert.True(list.Headers.CacheControl!.NoStore);
        var html = await list.Content.ReadAsStringAsync(); Assert.Equal(2, Regex.Matches(html,"data-inbox-id=").Count); Assert.Contains("data-unread-count=\"2\"", html);
        html = await client.GetStringAsync($"/Announcements/Details/{id}");
        Assert.Null(await ReadAt(id,student.Id)); Assert.Equal(2, await Unread(student.Id));
        var back = $"/Announcements/Mine?status=Unread&clubId={clubId}&search=Th%C3%B4ng&page=1";
        var fields = new Dictionary<string,string> { ["__RequestVerificationToken"] = Extract(html), ["UserId"] = other.Id, ["ReadAt"] = "2000-01-01T00:00:00Z", ["returnUrl"] = back };
        var before = DateTimeOffset.UtcNow;
        var response = await client.PostAsync($"/Announcements/MarkRead/{id}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode); Assert.Equal(back,response.Headers.Location!.OriginalString);
        var at = await ReadAt(id,student.Id); Assert.NotNull(at); Assert.InRange(at.Value,before,DateTimeOffset.UtcNow);
        Assert.Null(await ReadAt(id,manager.Id)); Assert.Null(await ReadAt(second,student.Id));
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync($"/Announcements/MarkRead/{id}",new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(at,await ReadAt(id,student.Id)); Assert.Equal(1,await Unread(student.Id));
        html = await client.GetStringAsync("/Announcements/Mine?status=Read"); Assert.Single(Regex.Matches(html,"data-inbox-id=")); Assert.Contains("data-unread-count=\"1\"",html);
        await using var db=database.CreateContext(); Assert.False(await db.AuditLogs.AnyAsync(a=>a.Action=="AnnouncementRead"));
        using var leader=await Login(manager); html=await leader.GetStringAsync($"/Announcements/Recipients/{id}"); Assert.Contains("data-read-count>1</span>",html);
    }

    [Fact]
    public async Task CrossUserAndManagerWithoutReceiptCannotReadForSomeoneElse()
    {
        var id=await Notice(recipient:other.Id);
        using var client=await Login(student); var token=Extract(await client.GetStringAsync("/Account/Overview"));
        var form=new Dictionary<string,string>{{"__RequestVerificationToken",token},{"UserId",other.Id}};
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync($"/Announcements/MarkRead/{id}",new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Announcements/Details/{id}")).StatusCode);
        Assert.Equal("NotFound",(await Mark(id,manager.Id)).Code);
        Assert.Null(await ReadAt(id,other.Id));
        using var leader=await Login(manager);
        var html=await leader.GetStringAsync($"/Announcements/Details/{id}");Assert.DoesNotContain("Đánh dấu đã đọc",WebUtility.HtmlDecode(html));
        Assert.Equal(0,await Unread(student.Id));Assert.Equal(0,await Unread(manager.Id));
    }

    [Fact]
    public async Task ConcurrentReadsKeepFirstTimestampAndDoNotAlterOtherRecipients()
    {
        var id=await Notice(); var results=await Task.WhenAll(Mark(id,student.Id),Mark(id,student.Id),Mark(id,student.Id));
        Assert.All(results,r=>Assert.True(r.Succeeded,r.Message));var at=await ReadAt(id,student.Id);Assert.NotNull(at);
        Assert.True((await Mark(id,student.Id)).Succeeded);Assert.Equal(at,await ReadAt(id,student.Id));
        Assert.Null(await ReadAt(id,manager.Id));Assert.Equal(0,await Unread(student.Id));Assert.Equal(1,await Unread(manager.Id));
        await using var db=database.CreateContext();Assert.Equal(2,await db.AnnouncementRecipients.CountAsync(r=>r.AnnouncementId==id));
    }

    [Fact]
    public async Task SnapshotSurvivesDepartureAndArchivedClubButDoesNotIncludeNewMembers()
    {
        var id=await Notice();await using var db=database.CreateContext();
        var membership=await db.Memberships.SingleAsync(m=>m.ClubId==clubId&&m.UserId==student.Id);
        membership.Status=MembershipStatus.Left;membership.EndedAt=DateTimeOffset.UtcNow;
        db.Memberships.Add(new Membership{ClubId=clubId,UserId=other.Id});
        (await db.Clubs.FindAsync(clubId))!.Status=ClubStatus.Archived;
        (await db.Announcements.FindAsync(id))!.Status=AnnouncementStatus.Archived;await db.SaveChangesAsync();
        using var client=await Login(student);var html=WebUtility.HtmlDecode(await client.GetStringAsync("/Announcements/Mine"));
        Assert.Contains("Đã lưu trữ",html);Assert.Contains("CLB hộp thông báo",html);Assert.Equal(1,await Unread(student.Id));
        Assert.True((await Mark(id,student.Id)).Succeeded);Assert.Equal(0,await Unread(student.Id));
        Assert.Equal("NotFound",(await Mark(id,other.Id)).Code);using var late=await Login(other);
        Assert.Empty(Regex.Matches(await late.GetStringAsync("/Announcements/Mine"),"data-inbox-id="));
    }

    [Theory]
    [InlineData(UserStatus.Disabled)][InlineData(UserStatus.Pending)][InlineData(UserStatus.Rejected)]
    public async Task InactiveAccountCannotListCountOrMarkWithOldCookie(UserStatus status)
    {
        var id=await Notice();using var client=await Login(student);var token=Extract(await client.GetStringAsync("/Announcements/Mine"));
        await using var db=database.CreateContext();var user=(await db.Users.FindAsync(student.Id))!;user.Status=status;await db.SaveChangesAsync();
        Assert.Equal(0,await Unread(student.Id));Assert.Equal("NotFound",(await Mark(id,student.Id)).Code);
        Assert.False((await client.GetAsync("/Announcements/Mine")).IsSuccessStatusCode);
        Assert.False((await client.PostAsync($"/Announcements/MarkRead/{id}",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",token}}))).IsSuccessStatusCode);
        Assert.Null(await ReadAt(id,student.Id));
    }

    [Fact]
    public async Task AdminAndGuestsDoNotReceiveStudentInboxPrivileges()
    {
        var id=await Notice();using var guest=app.BrowserClient();Assert.Equal(HttpStatusCode.Redirect,(await guest.GetAsync("/Announcements/Mine")).StatusCode);
        var admin=await User(SystemRoles.Admin);using var client=await Login(admin);
        var response=await client.GetAsync("/Announcements/Mine");Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Contains("AccessDenied",response.Headers.Location!.OriginalString);
        Assert.Equal("NotFound",(await Mark(id,admin.Id)).Code);Assert.Equal(0,await Unread(admin.Id));
        var html=await client.GetStringAsync("/Account/Overview");Assert.DoesNotContain("/Announcements/Mine",html);
        using var member=await Login(student);html=await member.GetStringAsync("/Account/Overview");Assert.Contains("/Announcements/Mine",html);Assert.DoesNotContain("CLB tôi quản lý",WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task DraftsEvenWithInvalidReceiptAreNeverCountedOrMarked()
    {
        var id=await Notice();await using var db=database.CreateContext();var item=(await db.Announcements.FindAsync(id))!;
        item.Status=AnnouncementStatus.Draft;item.PublishedAt=null;await db.SaveChangesAsync();
        Assert.Equal(0,await Unread(student.Id));Assert.Equal("NotFound",(await Mark(id,student.Id)).Code);
        using var client=await Login(student);Assert.DoesNotContain($"data-inbox-id=\"{id}\"",await client.GetStringAsync("/Announcements/Mine"));
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Announcements/Details/{id}")).StatusCode);Assert.Null(await ReadAt(id,student.Id));
    }

    [Fact]
    public async Task SearchClubReadFiltersAndPagingUseOnlyOwnReceivedMessages()
    {
        for(var i=0;i<12;i++)await Notice($"LETTER-{i:D2}");
        var read=await Notice("SPECIAL-READ");Assert.True((await Mark(read,student.Id)).Succeeded);
        var hidden=await Notice("PRIVATE-OTHER",recipient:other.Id);
        await using var db=database.CreateContext();var extra=new Club{Code=Guid.NewGuid().ToString("N")[..24],Name="CLB nhận khác"};db.Clubs.Add(extra);await db.SaveChangesAsync();
        var second=await Notice("SECOND-CLUB",targetClub:extra.Id);
        using var client=await Login(student);
        var html=await client.GetStringAsync("/Announcements/Mine");Assert.Equal(10,Regex.Matches(html,"data-inbox-id=").Count);Assert.DoesNotContain("PRIVATE-OTHER",html);Assert.Contains("data-unread-count=\"13\"",html);
        html=await client.GetStringAsync("/Announcements/Mine?page=2");Assert.Equal(4,Regex.Matches(html,"data-inbox-id=").Count);
        html=await client.GetStringAsync("/Announcements/Mine?status=Read");Assert.Single(Regex.Matches(html,"data-inbox-id="));Assert.Contains("SPECIAL-READ",html);Assert.Contains("data-inbox-unread>13</strong>",html);
        html=await client.GetStringAsync($"/Announcements/Mine?clubId={extra.Id}&status=Unread");Assert.Single(Regex.Matches(html,"data-inbox-id="));Assert.Contains("SECOND-CLUB",html);
        html=await client.GetStringAsync("/Announcements/Mine?search=LETTER-03");Assert.Single(Regex.Matches(html,"data-inbox-id="));Assert.Contains("LETTER-03",html);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/Announcements/Mine?clubId=2147483647")).StatusCode);
        Assert.Equal(0,await Unread("missing-user"));Assert.Equal("NotFound",(await Mark(hidden,student.Id)).Code);
    }

    [Fact]
    public async Task InvalidFiltersCsrfAndGetWritesAreRejected()
    {
        var id=await Notice();using var client=await Login(student);
        foreach(var query in new[]{"status=99","status=bad","page=bad","clubId=bad","search="+new string('x',181)})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/Announcements/Mine?"+query)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Announcements/MarkRead/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync($"/Announcements/MarkRead/{id}",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Null(await ReadAt(id,student.Id));
        var html=await client.GetStringAsync("/Announcements/Mine?page=-4");Assert.Contains("Trang 1 / 1",WebUtility.HtmlDecode(html));
    }

    [Theory]
    [InlineData("https://evil.example/path")][InlineData("//evil.example/path")][InlineData("/\\evil.example/path")]
    public async Task ReturnUrlCannotRedirectOutsideApplication(string url)
    {
        var id=await Notice();using var client=await Login(student);var html=await client.GetStringAsync($"/Announcements/Details/{id}?returnUrl={Uri.EscapeDataString(url)}");
        Assert.DoesNotContain("evil.example",html);
        var response=await client.PostAsync($"/Announcements/MarkRead/{id}",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",Extract(html)},{"returnUrl",url}}));
        Assert.Equal($"/Announcements/Details/{id}",response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task PersonalContentIsEncodedAndDoesNotExposeManagementDetails()
    {
        var id=await Notice("XSS <script>unsafe()</script>");using var client=await Login(student);
        var list=await client.GetStringAsync("/Announcements/Mine");var detail=await client.GetStringAsync($"/Announcements/Details/{id}");
        foreach(var html in new[]{list,detail})
        {
            foreach(var value in new[]{"<script>unsafe()</script>",manager.Email,manager.Id,other.Id})Assert.DoesNotContain(value,html);
            foreach(var value in new[]{"Xem người nhận","Lịch sử xử lý"})Assert.DoesNotContain(value,WebUtility.HtmlDecode(html));
            Assert.Contains("&lt;script&gt;",html);
        }
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/Announcements/Recipients/{id}")).StatusCode);
    }

    [Fact]
    public async Task ReadRacingArchiveKeepsReceiptAndContentIntact()
    {
        var id=await Notice();await using var db=database.CreateContext();var item=await db.Announcements.AsNoTracking().SingleAsync(a=>a.Id==id);
        async Task<WorkflowResult> Archive(){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<AnnouncementService>().TransitionAsync(id,manager.Id,new(){Operation="Archive",Reason="Hết hạn",RowVersion=Convert.ToBase64String(item.RowVersion)});}
        var results=await Task.WhenAll(Mark(id,student.Id),Archive());Assert.All(results,r=>Assert.True(r.Succeeded,r.Message));
        Assert.NotNull(await ReadAt(id,student.Id));Assert.Equal(0,await Unread(student.Id));Assert.Equal(1,await Unread(manager.Id));
        var current=await db.Announcements.AsNoTracking().SingleAsync(a=>a.Id==id);Assert.Equal(AnnouncementStatus.Archived,current.Status);Assert.Equal(item.Content,current.Content);
    }

    [Fact]
    public async Task BadgeCapsDisplayButKeepsExactAccessibleCount()
    {
        await using var db=database.CreateContext();var now=DateTimeOffset.UtcNow;
        var items=Enumerable.Range(0,101).Select(i=>new Announcement{ClubId=clubId,Title=$"Bulk-{i}",Content="Thông báo",CreatedAt=now.AddMinutes(-1),PublishedAt=now,Status=AnnouncementStatus.Published}).ToList();
        db.Announcements.AddRange(items);await db.SaveChangesAsync();db.AnnouncementRecipients.AddRange(items.Select(a=>new AnnouncementRecipient{AnnouncementId=a.Id,UserId=student.Id}));await db.SaveChangesAsync();
        using var client=await Login(student);var html=await client.GetStringAsync("/Announcements/Mine");
        Assert.Contains("data-unread-count=\"101\">99+",WebUtility.HtmlDecode(html));Assert.Contains("101 chưa đọc",WebUtility.HtmlDecode(html));Assert.Contains("data-inbox-unread>101</strong>",html);
    }

    [Fact]
    public async Task ReadTimestampIsOnlyAddedColumnAndMigrationCannotDiscardIt()
    {
        var id=await Notice();Assert.True((await Mark(id,student.Id)).Succeeded);
        await using var db=database.CreateContext();
        var columns=await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sys.columns WHERE object_id=OBJECT_ID('AnnouncementRecipients') ORDER BY column_id").ToListAsync();
        Assert.Equal(new[]{"AnnouncementId","UserId","ReadAt"},columns);
        var error=await Assert.ThrowsAsync<SqlException>(()=>db.GetService<IMigrator>().MigrateAsync("20261110151453_AddAnnouncements"));Assert.Equal(51000,error.Number);
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.NotNull(await ReadAt(id,student.Id));Assert.False(db.Database.HasPendingModelChanges());
    }

    private async Task<int> Notice(string title="Thông báo của sinh viên",string? recipient=null,int? targetClub=null)
    {
        await using var db=database.CreateContext();var now=DateTimeOffset.UtcNow;
        var item=new Announcement{ClubId=targetClub??clubId,Title=title,Content="Nội dung riêng của người nhận",CreatedAt=now.AddMinutes(-1),PublishedAt=now,Status=AnnouncementStatus.Published};
        db.Announcements.Add(item);await db.SaveChangesAsync();
        db.AnnouncementRecipients.Add(new(){AnnouncementId=item.Id,UserId=recipient??student.Id});
        if(recipient is null)db.AnnouncementRecipients.Add(new(){AnnouncementId=item.Id,UserId=manager.Id});
        await db.SaveChangesAsync();return item.Id;
    }
    private async Task<DateTimeOffset?> ReadAt(int id,string userId){await using var db=database.CreateContext();return (await db.AnnouncementRecipients.SingleAsync(r=>r.AnnouncementId==id&&r.UserId==userId)).ReadAt;}
    private async Task<int> Unread(string actor){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<AnnouncementService>().UnreadCountAsync(actor);}
    private async Task<WorkflowResult> Mark(int id,string actor){await using var scope=app.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<AnnouncementService>().MarkReadAsync(id,actor);}
    private async Task<Person> User(string role=SystemRoles.Student){await using var scope=app.Services.CreateAsyncScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var tag=Guid.NewGuid().ToString("N");var email=$"inbox-{tag}@example.test";var password=$"Test-Only!9-{tag}";var user=new ApplicationUser{UserName=email,Email=email,FullName="Người kiểm thử",StudentCode=tag[..24]};Assert.True((await users.CreateAsync(user,password)).Succeeded);Assert.True((await users.AddToRoleAsync(user,role)).Succeeded);return new(user.Id,email,password);}
    private async Task<HttpClient> Login(Person person){var client=app.BrowserClient();var token=Extract(await client.GetStringAsync("/Account/Login"));Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",person.Email},{"Password",person.Password},{"__RequestVerificationToken",token}}))).StatusCode);return client;}
    private static string Extract(string html){var value=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;Assert.NotEmpty(value);return WebUtility.HtmlDecode(value);}
    private record Person(string Id,string Email,string Password);
}
