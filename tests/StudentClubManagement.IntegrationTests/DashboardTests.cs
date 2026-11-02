using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.IntegrationTests;

public class DashboardTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task DashboardCountsClubStatesAndMembershipsWithoutCountingAdminAsStudent()
    {
        await using var app = new ClubWebFactory(database.ConnectionString);
        const string email = "dashboard-admin@example.test";
        var password = $"Test-Only!9-{Guid.NewGuid():N}";
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            database.AssertOwnsContext(db);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser { Email = email, UserName = email, FullName = "Quản trị kiểm thử" };
            Assert.True((await users.CreateAsync(admin, password)).Succeeded);
            Assert.True((await users.AddToRolesAsync(admin, [SystemRoles.Admin, SystemRoles.Student])).Succeeded);
            var student = new ApplicationUser { UserName = "active@example.test", Email = "active@example.test", FullName = "Sinh viên hoạt động", StudentCode = "DASH-A" };
            var disabled = new ApplicationUser { UserName = "disabled@example.test", Email = "disabled@example.test", FullName = "Sinh viên bị khóa", StudentCode = "DASH-B", Status = UserStatus.Disabled };
            foreach (var user in new[] { student, disabled })
            {
                Assert.True((await users.CreateAsync(user)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, SystemRoles.Student)).Succeeded);
            }
            var activeClub = new Club { Code = "DASH-A", Name = "CLB hoạt động" };
            var pausedClub = new Club { Code = "DASH-B", Name = "CLB tạm dừng", Status = ClubStatus.Suspended };
            var archivedClub = new Club { Code = "DASH-C", Name = "CLB lưu trữ", Status = ClubStatus.Archived };
            db.Clubs.AddRange(activeClub, pausedClub, archivedClub);
            db.Memberships.AddRange(
                new Membership { Club = activeClub, User = student, Role = ClubRole.Manager },
                new Membership { Club = pausedClub, User = student, Role = ClubRole.Manager },
                new Membership { Club = activeClub, User = disabled, Status = MembershipStatus.Left, EndedAt = DateTimeOffset.UtcNow });
            db.AuditLogs.Add(new AuditLog { ActorId = admin.Id, Action = "ClubCreated", EntityType = "Club", EntityId = "1", AfterData = "sensitive-audit-canary" });
            await db.SaveChangesAsync();
        }
        using var client = app.BrowserClient();
        var loginPage = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(loginPage, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var response = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("data-stat=\"clubs\">3</strong>", html);
        Assert.Contains("data-stat=\"active-clubs\">1</strong>", html);
        Assert.Contains("data-stat=\"students\">2</strong>", html);
        Assert.Contains("data-stat=\"memberships\">2</strong>", html);
        Assert.Contains("Tạo câu lạc bộ", html);
        Assert.DoesNotContain("sensitive-audit-canary", html);
    }
}