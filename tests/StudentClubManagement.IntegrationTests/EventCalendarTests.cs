using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.IntegrationTests;

public class EventCalendarTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private int clubId, otherClubId;
    private Person member = null!, outsider = null!;
    private const string Range = "/Events/Calendar?From=2030-01-02&To=2030-01-02";
    private static readonly DateTimeOffset Start = new(2030, 1, 2, 9, 0, 0, TimeSpan.FromHours(7));

    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope(); database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        member = await UserAsync(); outsider = await UserAsync();
        await using var db = database.CreateContext();
        var club = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "CLB lịch " + Guid.NewGuid().ToString("N")[..6] };
        var other = new Club { Code = Guid.NewGuid().ToString("N")[..24], Name = "CLB khác" };
        db.Clubs.AddRange(club, other); await db.SaveChangesAsync(); clubId = club.Id; otherClubId = other.Id;
        db.Memberships.AddRange(new Membership { ClubId = clubId, UserId = member.Id },
            new Membership { ClubId = otherClubId, UserId = outsider.Id, Role = ClubRole.Manager });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task GuestOnlySeesPublishedSchoolEventsAndCountsNeverLeakHiddenEvents()
    {
        var school = await Add("SCHOOL"); var cancelled = await Add("CANCELLED", status: EventStatus.Cancelled);
        var completed = await Add("COMPLETED", status: EventStatus.Completed);
        var draft = await Add("SECRET-DRAFT", status: EventStatus.Draft);
        var internalEvent = await Add("SECRET-INTERNAL", audience: EventAudience.Club);
        var cancelledDraft = await Add("SECRET-CANCELLED-DRAFT", status: EventStatus.Cancelled, wasPublished: false);
        var archived = await Add("SECRET-ARCHIVED", club: otherClubId);
        await using (var db = database.CreateContext()) { var other = await db.Clubs.FindAsync(otherClubId); other!.Status = ClubStatus.Archived; await db.SaveChangesAsync(); }
        using var guest = app.BrowserClient(); var response = await guest.GetAsync(Range + $"&ClubId={clubId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var html = await response.Content.ReadAsStringAsync(); Assert.Equal(new[] { school, cancelled, completed }, Ids(html));
        Assert.Contains("3 hoạt động", WebUtility.HtmlDecode(html)); Assert.Contains("Đã hủy", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("SECRET-", html); Assert.DoesNotContain(member.Email, html);
        foreach (var id in new[] { draft, internalEvent, cancelledDraft, archived })
            Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/Events/Details/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("Suspended")][InlineData("Left")][InlineData("Disabled")][InlineData("RoleRemoved")]
    public async Task InternalVisibilityIsLostImmediatelyWhenMembershipOrAccountChanges(string change)
    {
        var id = await Add("INTERNAL", audience: EventAudience.Club);
        using var own = await Login(member); using var other = await Login(outsider);
        Assert.Contains(id, Ids(await own.GetStringAsync(Range + $"&ClubId={clubId}")));
        Assert.Empty(Ids(await other.GetStringAsync(Range + $"&ClubId={clubId}")));
        await using (var db = database.CreateContext())
        {
            var m = await db.Memberships.SingleAsync(m => m.ClubId == clubId && m.UserId == member.Id);
            if (change == "Suspended") m.Status = MembershipStatus.Suspended;
            if (change == "Left") { m.Status = MembershipStatus.Left; m.EndedAt = DateTimeOffset.UtcNow; }
            if (change == "Disabled") (await db.Users.FindAsync(member.Id))!.Status = UserStatus.Disabled;
            if (change == "RoleRemoved") (await db.Users.FindAsync(member.Id))!.RoleId = null;
            await db.SaveChangesAsync();
        }
        Assert.Empty(Ids(await own.GetStringAsync(Range + $"&ClubId={clubId}")));
        Assert.Equal(HttpStatusCode.NotFound, (await own.GetAsync($"/Events/Details/{id}")).StatusCode);
    }

    [Fact]
    public async Task AdminCalendarExcludesDraftsAndArchivedClubsButManagementKeepsAccess()
    {
        var id = await Add("INTERNAL", audience: EventAudience.Club);
        var draft = await Add("SECRET-DRAFT", status: EventStatus.Draft);
        var archived = await Add("ARCHIVED", club: otherClubId);
        await using (var db = database.CreateContext()) { var other = await db.Clubs.FindAsync(otherClubId); other!.Status = ClubStatus.Archived; await db.SaveChangesAsync(); }
        var admin = await UserAsync(SystemRoles.Admin); using var client = await Login(admin);
        var html = await client.GetStringAsync(Range);
        Assert.Contains(id, Ids(html)); Assert.DoesNotContain(draft, Ids(html)); Assert.DoesNotContain(archived, Ids(html));
        Assert.DoesNotContain("KHÔNG GIAN QUẢN TRỊ", WebUtility.HtmlDecode(html));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Events/Details/{draft}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Events/Details/{archived}")).StatusCode);
    }

    [Fact]
    public async Task DateBoundariesAndGroupingUseVietnamTimeNotUtcOrServerLocalTime()
    {
        await Add("BEFORE", start: new DateTimeOffset(2030, 1, 1, 16, 59, 59, TimeSpan.Zero));
        var midnight = await Add("MIDNIGHT", start: new DateTimeOffset(2030, 1, 1, 17, 0, 0, TimeSpan.Zero));
        var last = await Add("LAST-MINUTE", start: new DateTimeOffset(2030, 1, 2, 16, 59, 59, TimeSpan.Zero));
        await Add("NEXT-DAY", start: new DateTimeOffset(2030, 1, 2, 17, 0, 0, TimeSpan.Zero));
        using var guest = app.BrowserClient(); var html = await guest.GetStringAsync(Range + $"&ClubId={clubId}");
        Assert.Equal(new[] { midnight, last }, Ids(html)); Assert.Single(Regex.Matches(html,"id=\"day-20300102\""));
        Assert.Contains(">00:00</time>",html); Assert.Contains(">23:59</time>",html);
        Assert.Contains("03/01/2030", WebUtility.HtmlDecode(html)); // kết thúc sang ngày hôm sau
    }

    [Fact]
    public async Task CombinedFiltersMatchTitleClubStatusAndInclusiveStartDate()
    {
        var match = await Add("Kết nối công nghệ", status: EventStatus.Cancelled);
        await Add("Kết nối công nghệ", status: EventStatus.Published);
        await Add("Kết nối công nghệ", status: EventStatus.Cancelled, club: otherClubId);
        await Add("Kết nối công nghệ", status: EventStatus.Cancelled, start: Start.AddDays(-1));
        await Add("Một sự kiện khác", status: EventStatus.Cancelled);
        using var guest = app.BrowserClient(); var html = await guest.GetStringAsync(Range + $"&ClubId={clubId}&Status=3&Search={Uri.EscapeDataString("Kết nối")}");
        Assert.Equal(new[] { match }, Ids(html));
        Assert.Contains("value=\"3\" selected=\"selected\"",html);
    }

    [Fact]
    public async Task PaginationHasStableOrderRetainsFiltersAndCountsOnlyVisibleRows()
    {
        var expected = new List<int>();
        for (var i=0;i<12;i++) expected.Add(await Add($"PAGE-{i:D2}"));
        for (var i=0;i<3;i++) await Add($"PAGE-SECRET-{i}", audience: EventAudience.Club);
        using var guest = app.BrowserClient(); var query = Range + $"&ClubId={clubId}&Status=2&Search=PAGE-";
        var first = await guest.GetStringAsync(query); var second = await guest.GetStringAsync(query + "&Page=2");
        Assert.Equal(expected.Take(10), Ids(first)); Assert.Equal(expected.Skip(10), Ids(second));
        Assert.Contains("12 hoạt động",WebUtility.HtmlDecode(first));
        var next = Regex.Match(first,"href=\"([^\"]+)\">Trang sau</a>").Groups[1].Value;
        Assert.NotEmpty(next); next=WebUtility.HtmlDecode(next);
        foreach(var value in new[]{"From=2030-01-02","To=2030-01-02",$"ClubId={clubId}","Status=Published","Search=PAGE-","Page=2"}) Assert.Contains(value,next);
        Assert.Equal(Ids(second), Ids(await guest.GetStringAsync(next)));
        Assert.Equal(Ids(second), Ids(await guest.GetStringAsync(query + "&Page=2147483647")));
        Assert.Equal(Ids(first), Ids(await guest.GetStringAsync(query + "&Page=0")));
    }

    [Theory]
    [InlineData("From=invalid&To=2030-01-02")]
    [InlineData("From=2030-01-03&To=2030-01-02")]
    [InlineData("From=2030-01-01&To=2032-01-01")]
    [InlineData("From=0001-01-01&To=0001-01-02")]
    [InlineData("From=9999-12-31")]
    [InlineData("To=9999-12-31")]
    [InlineData("Status=1")]
    [InlineData("Status=99")]
    [InlineData("Status=invalid")]
    [InlineData("ClubId=-1")]
    [InlineData("ClubId=2147483647")]
    [InlineData("Page=invalid")]
    public async Task InvalidFiltersReturnHelpfulFormWithoutServerErrors(string query)
    {
        using var guest=app.BrowserClient(); var response=await guest.GetAsync("/Events/Calendar?"+query);
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        var html=await response.Content.ReadAsStringAsync(); Assert.Contains("validation-summary-errors",html); Assert.Empty(Ids(html));
        Assert.Contains("Đặt lại bộ lọc",WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task DefaultRangeEmptyStateAndArchivedClubSelectionBehaveClearly()
    {
        using var guest=app.BrowserClient(); var today=DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var html=await guest.GetStringAsync($"/Events/Calendar?ClubId={clubId}");
        Assert.Contains($"value=\"{today:yyyy-MM-dd}\"",html); Assert.Contains("Chưa có hoạt động phù hợp",WebUtility.HtmlDecode(html));
        Assert.Contains("0 hoạt động",WebUtility.HtmlDecode(html));
        await using(var db=database.CreateContext()) { var club=await db.Clubs.FindAsync(clubId); club!.Status=ClubStatus.Archived; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.BadRequest,(await guest.GetAsync(Range+$"&ClubId={clubId}")).StatusCode);
    }

    [Fact]
    public async Task DetailsReturnLinkKeepsCalendarFiltersAndRejectsExternalUrls()
    {
        var id=await Add("<script>alert(1)</script>"); using var guest=app.BrowserClient();
        var calendar=await guest.GetStringAsync(Range+$"&ClubId={clubId}"); Assert.DoesNotContain("<script>alert(1)</script>",calendar);
        var href=Regex.Match(calendar,$"href=\"([^\"]*/Events/Details/{id}[^\"]*)\"").Groups[1].Value;
        Assert.NotEmpty(href); var details=await guest.GetStringAsync(WebUtility.HtmlDecode(href));
        Assert.Contains("Lịch hoạt động đã lọc",WebUtility.HtmlDecode(details)); Assert.Contains("From=2030-01-02",WebUtility.HtmlDecode(details));
        var external=await guest.GetStringAsync($"/Events/Details/{id}?returnUrl=https%3A%2F%2Fevil.example");
        Assert.DoesNotContain("evil.example",external); Assert.DoesNotContain("Lịch hoạt động đã lọc",WebUtility.HtmlDecode(external));
    }

    private async Task<int> Add(string title, EventStatus status=EventStatus.Published, EventAudience audience=EventAudience.School, int? club=null, DateTimeOffset? start=null, bool wasPublished=true)
    {
        await using var db=database.CreateContext(); var at=start??Start;
        var item=new ClubEvent { ClubId=club??clubId, Title=title, Location="Hội trường", Description="Nội dung thử", StartsAt=at, EndsAt=at.AddHours(2), RegistrationDeadline=at.AddDays(-1), Capacity=30, Audience=audience,Status=status,PublishedAt=status==EventStatus.Draft||!wasPublished?null:at.AddDays(-5) };
        db.Events.Add(item); await db.SaveChangesAsync(); return item.Id;
    }
    private async Task<Person> UserAsync(string role=SystemRoles.Student)
    {
        await using var scope=app.Services.CreateAsyncScope(); var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tag=Guid.NewGuid().ToString("N"); var email=$"calendar-{tag}@example.test"; var password=$"Test-Only!9-{tag}";
        var user=new ApplicationUser{UserName=email,Email=email,FullName="Người kiểm thử lịch",StudentCode=tag[..24]};
        Assert.True((await users.CreateAsync(user,password)).Succeeded); Assert.True((await users.AddToRoleAsync(user,role)).Succeeded); return new(user.Id,email,password);
    }
    private async Task<HttpClient> Login(Person person)
    {
        var client=app.BrowserClient(); var html=await client.GetStringAsync("/Account/Login");
        var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",person.Email},{"Password",person.Password},{"__RequestVerificationToken",token}}))).StatusCode); return client;
    }
    private static int[] Ids(string html)=>Regex.Matches(html,"data-event-id=\"(\\d+)\"").Select(m=>int.Parse(m.Groups[1].Value)).ToArray();
    private record Person(string Id,string Email,string Password);
}
