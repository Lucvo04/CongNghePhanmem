using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.IntegrationTests;

public class PublicClubTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private ClubWebFactory app = null!;
    private string tag = string.Empty;

    public async Task InitializeAsync()
    {
        app = new(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        database.AssertOwnsContext(db);
        tag = Guid.NewGuid().ToString("N")[..12];
        for (var index = 0; index < 9; index++)
        {
            db.Clubs.Add(new Club
            {
                Code = $"PUB-{tag}-{index:D2}", Name = $"CLB {tag} Lập trình {index:D2}",
                Category = index == 8 ? $"PrivateCategory-{tag}" : $"Lĩnh vực {tag}",
                Description = $"Giới thiệu {tag} {index:D2}\nCùng học và thực hành.",
                Status = index == 8 ? ClubStatus.Archived : index == 7 ? ClubStatus.Suspended : ClubStatus.Active
            });
        }
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task AnonymousNavigationFindsCatalogAndDetailsWithoutLogin()
    {
        using var client = app.BrowserClient();
        var home = await client.GetStringAsync("/");
        Assert.Contains("href=\"/Clubs\"", home);
        Assert.Contains("Khám phá câu lạc bộ", WebUtility.HtmlDecode(home));
        var list = await client.GetAsync($"/Clubs?search={tag}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode); Assert.True(list.Headers.CacheControl!.NoStore);
        var html = await list.Content.ReadAsStringAsync();
        Assert.Equal(6, Ids(html).Count);
        Assert.Contains("8 câu lạc bộ phù hợp", WebUtility.HtmlDecode(html));
        var response = await client.GetAsync($"/Clubs/Details/{Ids(html)[0]}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var details = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Giới thiệu câu lạc bộ", details);
        Assert.Contains("Cùng học và thực hành.", details);
        Assert.DoesNotContain("Đăng ký tham gia", details);
    }

    [Fact]
    public async Task SearchTrimsAndMatchesVietnameseNameOrCodeIgnoringCase()
    {
        using var client = app.BrowserClient();
        var byName = await client.GetStringAsync("/Clubs?search=" + Uri.EscapeDataString($"  {tag} LẬP TRÌNH 03  "));
        var byCode = await client.GetStringAsync("/Clubs?search=" + Uri.EscapeDataString($"pub-{tag}-03"));
        Assert.Single(Ids(byName)); Assert.Equal(Ids(byName), Ids(byCode));
    }

    [Fact]
    public async Task CategoryCombinesWithSearchAndPaginationKeepsBothFilters()
    {
        using var client = app.BrowserClient(); var category = $"Lĩnh vực {tag}";
        var path = $"/Clubs?search={tag}&category={Uri.EscapeDataString(category)}";
        var first = await client.GetStringAsync(path);
        var links = Regex.Matches(first, "href=\"([^\"]+)\"").Select(match => WebUtility.HtmlDecode(match.Groups[1].Value));
        var next = Assert.Single(links, link => link.Contains("page=2", StringComparison.Ordinal));
        Assert.Contains("search=" + tag, next); Assert.Contains("category=", next);
        var second = await client.GetStringAsync(next);
        Assert.Equal(6, Ids(first).Count); Assert.Equal(2, Ids(second).Count);
        Assert.Empty(Ids(first).Intersect(Ids(second)));
        Assert.Equal(Ids(second), Ids(await client.GetStringAsync(path + "&page=2147483647")));
        Assert.Equal(Ids(first), Ids(await client.GetStringAsync(path + "&page=-5")));
        var all = Ids(first).Concat(Ids(second)).ToArray();
        await using var db = database.CreateContext();
        Assert.Equal(await db.Clubs.Where(club => club.Name.Contains(tag) && club.Status != ClubStatus.Archived)
            .OrderBy(club => club.Name).ThenBy(club => club.Id).Select(club => club.Id).ToArrayAsync(), all);
        Assert.Empty(Ids(await client.GetStringAsync($"/Clubs?search={tag}&category=UNKNOWN-{tag}")));
    }

    [Fact]
    public async Task ArchivedClubsDoNotLeakThroughListsCategoriesDirectLinksOrExtraParameters()
    {
        using var client = app.BrowserClient(); var archived = await ClubAsync(8);
        var html = await client.GetStringAsync($"/Clubs?search={tag}&includeArchived=true&status=3");
        Assert.DoesNotContain(archived.Code, html);
        Assert.DoesNotContain(archived.Category!, html);
        var response = await client.GetAsync($"/Clubs/Details/{archived.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(archived.Name, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Contains("Về danh mục CLB", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task SuspendedClubHasClearStatusAndDoesNotOfferRegistration()
    {
        using var client = app.BrowserClient(); var suspended = await ClubAsync(7);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Clubs/Details/{suspended.Id}"));
        Assert.Contains("Tạm dừng", html); Assert.Contains("không tiếp nhận đăng ký mới", html);
        Assert.DoesNotContain("/ClubApplications", html);
        Assert.Contains(suspended.Code, await client.GetStringAsync("/Clubs?search=" + suspended.Code));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483647")]
    [InlineData("not-a-number")]
    public async Task InvalidOrMissingClubReturnsHelpful404(string id)
    {
        using var client = app.BrowserClient(); var response = await client.GetAsync("/Clubs/Details/" + id);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Không tìm thấy câu lạc bộ", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("search", 151)]
    [InlineData("category", 101)]
    [InlineData("page", 0)]
    public async Task InvalidFiltersReturn400WithoutQueryingOrShowingResults(string field, int length)
    {
        using var client = app.BrowserClient();
        var value = length == 0 ? "abc" : new string('x', length);
        var response = await client.GetAsync($"/Clubs?{field}={value}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Bộ lọc không hợp lệ", html); Assert.Empty(Ids(html));
    }

    [Fact]
    public async Task EmptyCategoryAndDescriptionHaveFallbacksAndFiltersAreDeduplicated()
    {
        var club = await ClubAsync(0);
        await using (var db = database.CreateContext())
        {
            var tracked = (await db.Clubs.FindAsync(club.Id))!; tracked.Category = null; tracked.Description = null; await db.SaveChangesAsync();
        }
        using var client = app.BrowserClient();
        var details = WebUtility.HtmlDecode(await client.GetStringAsync($"/Clubs/Details/{club.Id}"));
        Assert.Contains("Chưa phân loại lĩnh vực", details); Assert.Contains("đang cập nhật phần giới thiệu", details);
        var list = WebUtility.HtmlDecode(await client.GetStringAsync($"/Clubs?search={club.Code}"));
        Assert.Contains("Chưa phân loại", list); Assert.Contains("Thông tin giới thiệu đang được cập nhật", list);
        Assert.Single(Regex.Matches(list, "<option value=\"" + Regex.Escape($"Lĩnh vực {tag}") + "\"").Cast<Match>());
    }

    [Fact]
    public async Task EmptyResultsExplainHowToRecoverAndKeepUnknownCategoryVisible()
    {
        using var client = app.BrowserClient(); var unknown = "Không có " + tag;
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Clubs?search=absent-" + tag + "&category=" + Uri.EscapeDataString(unknown)));
        Assert.Contains("Chưa tìm thấy câu lạc bộ phù hợp", html); Assert.Contains("Bỏ lọc", html);
        Assert.Contains($"value=\"{unknown}\" selected", html); Assert.Empty(Ids(html));
    }

    [Fact]
    public async Task MarkupAndQueryStringsAreEncodedAndLikeWildcardsRemainLiteral()
    {
        var club = await ClubAsync(0); var specialName = $"{tag} <script>alert(1)</script> 100%_";
        await using (var db = database.CreateContext())
        {
            var tracked = (await db.Clubs.FindAsync(club.Id))!;
            tracked.Name = specialName; tracked.Category = "<b>" + tag + "</b>";
            tracked.Description = "</p><img src=x onerror=alert(2)>"; await db.SaveChangesAsync();
        }
        using var client = app.BrowserClient();
        var html = await client.GetStringAsync("/Clubs?search=" + Uri.EscapeDataString(specialName));
        Assert.Single(Ids(html)); Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>alert(1)", html);
        var details = await client.GetStringAsync($"/Clubs/Details/{club.Id}");
        Assert.Contains("&lt;img", details); Assert.DoesNotContain("<img src=x", details);
        Assert.Single(Ids(await client.GetStringAsync("/Clubs?search=" + Uri.EscapeDataString($"100%_"))));
        Assert.Empty(Ids(await client.GetStringAsync("/Clubs?search=" + Uri.EscapeDataString("' OR 1=1 --"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicPagesNeverExposeMembershipsPersonalFieldsOrManagementActions(bool signInAdmin)
    {
        var club = await ClubAsync(0);
        var email = $"private-{tag}@example.test"; var code = "PRIVATE-" + tag; var name = "Private person " + tag;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, FullName = name, StudentCode = code,
                ClassName = "PrivateClass-" + tag, Faculty = "PrivateFaculty-" + tag, PhoneNumber = "PrivatePhone-" + tag };
            Assert.True((await users.CreateAsync(user, "Public-Test!9-Password")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, signInAdmin ? SystemRoles.Admin : SystemRoles.Student)).Succeeded);
            db.Memberships.Add(new() { ClubId = club.Id, UserId = user.Id, Role = ClubRole.Manager }); await db.SaveChangesAsync();
        }
        using var client = app.BrowserClient();
        if (signInAdmin)
        {
            var token = Regex.Match(await client.GetStringAsync("/Account/Login"), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Email"] = email, ["Password"] = "Public-Test!9-Password", ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }))).StatusCode);
        }
        foreach (var path in new[] { $"/Clubs?search={tag}", $"/Clubs/Details/{club.Id}" })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(path));
            foreach (var privateValue in new[] { email, code, name, "PrivateClass-" + tag, "PrivateFaculty-" + tag, "PrivatePhone-" + tag,
                "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "RowVersion", "ClubManagement/Edit", "Clubs/Transfer" })
                Assert.DoesNotContain(privateValue, html);
        }
        var archived = await ClubAsync(8);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Clubs/Details/{archived.Id}")).StatusCode);
    }

    [Fact]
    public async Task StatusChangeHidesPreviouslyVisibleClubOnNextRequest()
    {
        var club = await ClubAsync(0); using var client = app.BrowserClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Clubs/Details/{club.Id}")).StatusCode);
        await using (var db = database.CreateContext())
        {
            (await db.Clubs.FindAsync(club.Id))!.Status = ClubStatus.Archived; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Clubs/Details/{club.Id}")).StatusCode);
        Assert.Empty(Ids(await client.GetStringAsync($"/Clubs?search={club.Code}")));
    }

    private async Task<Club> ClubAsync(int index)
    {
        await using var db = database.CreateContext();
        return await db.Clubs.AsNoTracking().SingleAsync(club => club.Code == $"PUB-{tag}-{index:D2}");
    }
    private static IReadOnlyList<int> Ids(string html) => Regex.Matches(html, "data-public-club-id=\"(\\d+)\"")
        .Select(match => int.Parse(match.Groups[1].Value)).ToArray();
}
