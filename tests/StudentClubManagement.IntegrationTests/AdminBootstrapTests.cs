using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.IntegrationTests;

public class AdminBootstrapTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task BootstrapRejectsInvalidInputAndExistingUsersThenCreatesExactlyOneAdmin()
    {
        await using var app = new ClubWebFactory(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        database.AssertOwnsContext(db);
        var bootstrapper = scope.ServiceProvider.GetRequiredService<AdminBootstrapper>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var invalid = await bootstrapper.CreateAsync(new AdminBootstrapRequest());
        Assert.False(invalid.Succeeded);
        var request = new AdminBootstrapRequest
        {
            Email = "initial-admin@example.test", FullName = "Quản trị kiểm thử", Password = "weak"
        };
        Assert.False((await bootstrapper.CreateAsync(request)).Succeeded);
        Assert.Empty(await db.Users.ToListAsync());

        var existing = new ApplicationUser { Email = "existing@example.test", UserName = "existing@example.test", FullName = "Sinh viên kiểm thử" };
        Assert.True((await users.CreateAsync(existing, "Test-Only!9-Existing")).Succeeded);
        var collision = await bootstrapper.CreateAsync(new AdminBootstrapRequest
        {
            Email = existing.Email!, FullName = "Không được nâng quyền", Password = "Test-Only!9-Collision"
        });
        Assert.Contains(collision.Errors, e => e.Code == "EmailAlreadyExists");
        Assert.False(await users.IsInRoleAsync(existing, SystemRoles.Admin));

        request.Password = $"Test-Only!9-{Guid.NewGuid():N}";
        Assert.True((await bootstrapper.CreateAsync(request)).Succeeded);
        var admin = (await users.FindByEmailAsync(request.Email))!;
        var originalHash = admin.PasswordHash;
        Assert.True(await users.IsInRoleAsync(admin, SystemRoles.Admin));
        Assert.True(admin.LockoutEnabled);
        Assert.Null(admin.StudentCode);

        request.Password = $"Different!9-{Guid.NewGuid():N}";
        var retry = await bootstrapper.CreateAsync(request);
        Assert.Contains(retry.Errors, error => error.Code == "AdminAlreadyExists");
        Assert.Equal(originalHash, (await users.FindByEmailAsync(request.Email))!.PasswordHash);
        request.Email = "another-admin@example.test";
        Assert.False((await bootstrapper.CreateAsync(request)).Succeeded);
        Assert.Null(await users.FindByEmailAsync(request.Email));
        Assert.Single(await users.GetUsersInRoleAsync(SystemRoles.Admin));
    }
}
