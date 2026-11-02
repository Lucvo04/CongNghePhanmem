using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Health;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.IntegrationTests;

public class DataIntegrityTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task MigrationSeedsOnlyGlobalRolesAndHasNoPendingChanges()
    {
        await using var db = fixture.CreateContext();
        Assert.Equal(new[] { SystemRoles.Admin, SystemRoles.Student },
            await db.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToArrayAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task IdentityHashesPasswordAndRejectsEmailWithDifferentCasing()
    {
        await using var provider = CreateServices();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = NewUser();
        var password = $"Test-Only!aA7-{Guid.NewGuid():N}";
        Assert.True((await manager.CreateAsync(user, password)).Succeeded);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(await manager.CheckPasswordAsync(user, password));
        Assert.False(await manager.CheckPasswordAsync(user, "Incorrect-password!8"));

        var duplicate = NewUser();
        duplicate.Email = user.Email!.ToUpperInvariant();
        var result = await manager.CreateAsync(duplicate, password);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "DuplicateEmail");
    }

    [Fact]
    public async Task DatabaseAlsoRejectsDuplicateNormalizedEmail()
    {
        await using var db = fixture.CreateContext();
        var first = NewUser();
        db.Users.Add(first);
        await db.SaveChangesAsync();
        var second = NewUser();
        second.Email = first.Email;
        second.NormalizedEmail = first.NormalizedEmail;
        db.Users.Add(second);
        await AssertSqlFailureAsync(db, 2601, 2627);
    }

    [Fact]
    public async Task StudentCodePreservesLeadingZerosAndIsUniqueIgnoringCase()
    {
        await using var db = fixture.CreateContext();
        var first = NewUser();
        first.StudentCode = $"00ab{Guid.NewGuid():N}"[..24];
        db.Users.Add(first);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(first.StudentCode, (await db.Users.FindAsync(first.Id))!.StudentCode);
        var second = NewUser();
        second.StudentCode = first.StudentCode.ToUpperInvariant();
        db.Users.Add(second);
        await AssertSqlFailureAsync(db, 2601, 2627);
    }

    [Fact]
    public async Task MultipleSystemAccountsCanOmitStudentCode()
    {
        await using var db = fixture.CreateContext();
        var first = NewUser();
        var second = NewUser();
        first.StudentCode = null;
        second.StudentCode = null;
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ClubCodeIsUniqueIgnoringCase()
    {
        await using var db = fixture.CreateContext();
        var first = NewClub();
        db.Clubs.Add(first);
        await db.SaveChangesAsync();
        var second = NewClub();
        second.Code = first.Code.ToLowerInvariant();
        db.Clubs.Add(second);
        await AssertSqlFailureAsync(db, 2601, 2627);
    }

    [Theory]
    [InlineData(MembershipStatus.Active)]
    [InlineData(MembershipStatus.Suspended)]
    public async Task UserCannotHaveTwoOpenPeriodsInSameClub(MembershipStatus status)
    {
        await using var db = fixture.CreateContext();
        var first = NewMembership();
        first.Status = status;
        db.Memberships.Add(first);
        await db.SaveChangesAsync();
        db.Memberships.Add(new Membership { UserId = first.UserId, ClubId = first.ClubId });
        await AssertSqlFailureAsync(db, 2601, 2627);
    }

    [Fact]
    public async Task StudentCanJoinManyClubsAndRejoinWithoutOverwritingHistory()
    {
        await using var db = fixture.CreateContext();
        var oldPeriod = NewMembership();
        oldPeriod.JoinedAt = DateTimeOffset.UtcNow.AddDays(-10);
        oldPeriod.EndedAt = DateTimeOffset.UtcNow.AddDays(-1);
        oldPeriod.Status = MembershipStatus.Left;
        db.Memberships.Add(oldPeriod);
        await db.SaveChangesAsync();
        db.Memberships.AddRange(
            new Membership { UserId = oldPeriod.UserId, ClubId = oldPeriod.ClubId },
            new Membership { UserId = oldPeriod.UserId, Club = NewClub() });
        await db.SaveChangesAsync();
        Assert.Equal(3, await db.Memberships.CountAsync(m => m.UserId == oldPeriod.UserId));
        Assert.Equal(1, await db.Memberships.CountAsync(m => m.UserId == oldPeriod.UserId && m.EndedAt != null));
    }

    [Fact]
    public async Task ClubCannotHaveTwoCurrentManagers()
    {
        await using var db = fixture.CreateContext();
        var manager = NewMembership();
        manager.Role = ClubRole.Manager;
        db.Memberships.Add(manager);
        await db.SaveChangesAsync();
        db.Memberships.Add(new Membership { ClubId = manager.ClubId, User = NewUser(), Role = ClubRole.Manager });
        await AssertSqlFailureAsync(db, 2601, 2627);
    }

    [Fact]
    public async Task CurrentManagerCannotBeSuspendedBeforeTransfer()
    {
        await using var db = fixture.CreateContext();
        var manager = NewMembership();
        manager.Role = ClubRole.Manager;
        db.Memberships.Add(manager);
        await db.SaveChangesAsync();
        manager.Status = MembershipStatus.Suspended;
        await AssertSqlFailureAsync(db, 547);
    }

    [Theory]
    [InlineData(MembershipStatus.Left, null)]
    [InlineData(MembershipStatus.Left, -1)]
    [InlineData(MembershipStatus.Active, 1)]
    public async Task InconsistentMembershipPeriodIsRejected(MembershipStatus status, int? daysAfterJoin)
    {
        await using var db = fixture.CreateContext();
        var membership = NewMembership();
        membership.Status = status;
        membership.EndedAt = daysAfterJoin.HasValue ? membership.JoinedAt.AddDays(daysAfterJoin.Value) : null;
        db.Memberships.Add(membership);
        await AssertSqlFailureAsync(db, 547);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CannotDeleteClubOrUserWithMembershipHistory(bool deleteClub)
    {
        await using var db = fixture.CreateContext();
        var membership = NewMembership();
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        // ExecuteDelete kiểm tra FK thực sự tại SQL Server, không chỉ quan hệ trong ChangeTracker.
        var error = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            if (deleteClub)
            {
                await db.Clubs.Where(c => c.Id == membership.ClubId).ExecuteDeleteAsync();
            }
            else
            {
                await db.Users.Where(u => u.Id == membership.UserId).ExecuteDeleteAsync();
            }
        });
        Assert.Equal(547, error.Number);
    }

    [Fact]
    public async Task MembershipRequiresAnExistingUser()
    {
        await using var db = fixture.CreateContext();
        db.Memberships.Add(new Membership { Club = NewClub(), UserId = Guid.NewGuid().ToString() });
        await AssertSqlFailureAsync(db, 547);
    }

    [Fact]
    public async Task StaleClubUpdateCannotOverwriteAnotherEditorsChange()
    {
        await using var firstContext = fixture.CreateContext();
        var club = NewClub();
        firstContext.Clubs.Add(club);
        await firstContext.SaveChangesAsync();
        await using var secondContext = fixture.CreateContext();
        var staleClub = await secondContext.Clubs.SingleAsync(c => c.Id == club.Id);
        club.Name = "Nội dung đã cập nhật";
        await firstContext.SaveChangesAsync();
        staleClub.Name = "Nội dung từ phiên cũ";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task ReadyCheckAcceptsMigratedDatabase()
    {
        await using var provider = CreateServices();
        var check = new DatabaseReadinessCheck(provider.GetRequiredService<IServiceScopeFactory>());
        Assert.Equal(HealthStatus.Healthy, (await check.CheckHealthAsync(new HealthCheckContext())).Status);
    }

    [Fact]
    public async Task ReadyCheckRejectsMissingDatabaseWithoutCreatingIt()
    {
        var missingDatabase = $"StudentClubManagement_Missing_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = missingDatabase,
            ConnectTimeout = 2
        };
        await using var provider = CreateServices(connection.ConnectionString);
        var check = new DatabaseReadinessCheck(provider.GetRequiredService<IServiceScopeFactory>());
        Assert.Equal(HealthStatus.Unhealthy, (await check.CheckHealthAsync(new HealthCheckContext())).Status);
        connection.InitialCatalog = "master";
        await using var master = new SqlConnection(connection.ConnectionString);
        await master.OpenAsync();
        await using var query = new SqlCommand("SELECT DB_ID(@databaseName)", master);
        query.Parameters.AddWithValue("@databaseName", missingDatabase);
        Assert.IsType<DBNull>(await query.ExecuteScalarAsync());
    }

    [Fact]
    public async Task StaleMembershipUpdateCannotOverwriteNewStatus()
    {
        await using var firstContext = fixture.CreateContext();
        var membership = NewMembership();
        firstContext.Memberships.Add(membership);
        await firstContext.SaveChangesAsync();
        await using var secondContext = fixture.CreateContext();
        var stale = await secondContext.Memberships.SingleAsync(m => m.Id == membership.Id);
        membership.Status = MembershipStatus.Suspended;
        await firstContext.SaveChangesAsync();
        stale.Status = MembershipStatus.Left;
        stale.EndedAt = DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    private ServiceProvider CreateServices(string? connectionString = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString ?? fixture.ConnectionString));
        services.AddClubIdentity();
        return services.BuildServiceProvider();
    }

    private static ApplicationUser NewUser()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"student-{suffix}@example.test";
        return new ApplicationUser
        {
            FullName = "Sinh viên kiểm thử", StudentCode = suffix[..24],
            UserName = email, NormalizedUserName = email.ToUpperInvariant(),
            Email = email, NormalizedEmail = email.ToUpperInvariant()
        };
    }

    private static Club NewClub() => new()
    {
        Code = $"CLB{Guid.NewGuid():N}"[..30].ToUpperInvariant(), Name = "Câu lạc bộ kiểm thử"
    };

    private static Membership NewMembership() => new() { User = NewUser(), Club = NewClub() };

    private static async Task AssertSqlFailureAsync(ApplicationDbContext db, params int[] expectedNumbers)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Contains(sql.Number, expectedNumbers);
    }
}
