using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.IntegrationTests;

public class CompactIdentityTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task StoresExposeOnlySupportedFeaturesAndStillIssueRoleClaims()
    {
        await using var app = new ClubWebFactory(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        database.AssertOwnsContext(db);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True(users.SupportsUserPassword && users.SupportsUserRole && users.SupportsUserLockout && users.SupportsUserSecurityStamp);
        Assert.False(users.SupportsUserClaim);
        Assert.False(users.SupportsUserLogin);
        Assert.False(users.SupportsUserAuthenticationTokens);
        Assert.False(users.SupportsUserTwoFactor);
        Assert.False(roles.SupportsRoleClaims);
        var email = $"compact-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = "Compact identity test" };
        Assert.True((await users.CreateAsync(user, $"Test-Only!9-{Guid.NewGuid():N}")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, SystemRoles.Admin)).Succeeded);
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        Assert.True(principal.IsInRole(SystemRoles.Admin));
        Assert.Equal(user.Id, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(new[] { "AnnouncementRecipients", "Announcements", "Attendances", "AuditLogs", "ClubProposals", "Clubs", "EventRegistrations", "Events", "JoinRequests", "Memberships", "Roles", "Users" },
            db.Model.GetEntityTypes().Select(e => e.GetTableName()).OrderBy(n => n).ToArray());
        Assert.False(db.Database.HasPendingModelChanges());
        var tables = await db.Database.SqlQueryRaw<string>("SELECT [name] AS [Value] FROM sys.tables WHERE [name] <> '__EFMigrationsHistory'").ToListAsync();
        Assert.Equal(12, tables.Count);
        Assert.DoesNotContain(tables, name => name.StartsWith("AspNet"));
    }

    [Fact]
    public async Task UnexpectedTwoFactorFlagCannotSilentlyBypassAuthentication()
    {
        await using var app = new ClubWebFactory(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        database.AssertOwnsContext(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var email = $"twofactor-{Guid.NewGuid():N}@example.test";
        var password = $"Test-Only!9-{Guid.NewGuid():N}";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = "Unsupported feature test" };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        user.TwoFactorEnabled = true;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        Assert.True((await signIn.CheckPasswordSignInAsync(user, password, true)).IsNotAllowed);
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        Assert.Null(await signIn.ValidateSecurityStampAsync(principal));
        user.TwoFactorEnabled = false;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
    }

    [Fact]
    public async Task MigrationPreservesAccountsPasswordsRolesClubsAndRejectsNonemptyExtensions()
    {
        await using var app = new ClubWebFactory(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        database.AssertOwnsContext(db);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"migration-{Guid.NewGuid():N}@example.test";
        var password = $"Test-Only!9-{Guid.NewGuid():N}";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = "Migration preservation" };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, SystemRoles.Student)).Succeeded);
        var hash = user.PasswordHash;
        var stamp = user.SecurityStamp;
        var club = new Club { Code = "MIGRATION-" + Guid.NewGuid().ToString("N")[..12], Name = "CLB giữ dữ liệu" };
        var member = new Membership { Club = club, UserId = user.Id, Role = ClubRole.Manager };
        var audit = new AuditLog { ActorId = user.Id, Action = "ClubCreated", EntityType = "Club", EntityId = "migration-test", AfterData = "{}" };
        db.Memberships.Add(member); db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261030030529_AddAccountAuditLogs");
        db.ChangeTracker.Clear();
        try
        {
            // Mỗi bảng mở rộng có dữ liệu đều phải chặn migration trước khi mất dữ liệu.
            var extensionRows = new[]
            {
                "INSERT INTO AspNetUserClaims (UserId,ClaimType,ClaimValue) VALUES (@id,'test','preserve')",
                "INSERT INTO AspNetRoleClaims (RoleId,ClaimType,ClaimValue) VALUES ('system-admin','test','preserve')",
                "INSERT INTO AspNetUserLogins (LoginProvider,ProviderKey,ProviderDisplayName,UserId) VALUES ('test','key','test',@id)",
                "INSERT INTO AspNetUserTokens (UserId,LoginProvider,Name,Value) VALUES (@id,'test','key','preserve')",
                "UPDATE AspNetUsers SET TwoFactorEnabled=1 WHERE Id=@id"
            };
            var cleanups = new[]
            {
                "DELETE FROM AspNetUserClaims WHERE ClaimType='test'",
                "DELETE FROM AspNetRoleClaims WHERE ClaimType='test'",
                "DELETE FROM AspNetUserLogins WHERE LoginProvider='test'",
                "DELETE FROM AspNetUserTokens WHERE LoginProvider='test'",
                "UPDATE AspNetUsers SET TwoFactorEnabled=0 WHERE Id=@id"
            };
            for (var i = 0; i < extensionRows.Length; i++)
            {
                await db.Database.ExecuteSqlRawAsync(extensionRows[i], new SqlParameter("@id", user.Id));
                var error = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync());
                Assert.Equal(51000, error.Number);
                Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name='AspNetUsers'").SingleAsync());
                await db.Database.ExecuteSqlRawAsync(cleanups[i], new SqlParameter("@id", user.Id));
            }
        }
        finally
        {
            // Chỉ database do fixture tạo. Luôn trả schema về mới nhất trước khi dispose.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserClaims; DELETE FROM AspNetRoleClaims; DELETE FROM AspNetUserLogins; DELETE FROM AspNetUserTokens; UPDATE AspNetUsers SET TwoFactorEnabled=0;");
            await migrator.MigrateAsync();
        }
        var restored = await users.FindByIdAsync(user.Id);
        Assert.NotNull(restored);
        Assert.Equal(hash, restored.PasswordHash);
        Assert.Equal(stamp, restored.SecurityStamp);
        Assert.True(await users.CheckPasswordAsync(restored, password));
        Assert.False(await users.IsInRoleAsync(restored, SystemRoles.Admin));
        Assert.True(await users.IsInRoleAsync(restored, SystemRoles.Student));
        Assert.Equal("CLB giữ dữ liệu", (await db.Clubs.SingleAsync(c => c.Id == club.Id)).Name);
        Assert.Equal(user.Id, (await db.Memberships.SingleAsync(m => m.Id == member.Id)).UserId);
        Assert.Equal("{}", (await db.AuditLogs.SingleAsync(a => a.Id == audit.Id)).AfterData);
    }
}
