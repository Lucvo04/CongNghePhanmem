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

public class AccountSchemaTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    private const string Previous = "20261105024917_AddClubEvents";

    [Theory]
    [InlineData("MultipleRoles")]
    [InlineData("SeparateUsername")]
    [InlineData("TwoFactor")]
    [InlineData("ConfirmedEmail")]
    [InlineData("ConfirmedPhone")]
    public async Task MigrationRejectsLossyConversionsAndPreservesPasswords(string condition)
    {
        await using var app = new ClubWebFactory(database.ConnectionString);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); database.AssertOwnsContext(db);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"schema-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Email=email, FullName="Schema preservation", RoleId="system-student" };
        Assert.True((await users.CreateAsync(user,"Test-Only!9-password")).Succeeded);
        var hash=user.PasswordHash; var securityStamp=user.SecurityStamp; var concurrencyStamp=user.ConcurrencyStamp;
        var migrator=db.GetService<IMigrator>(); await migrator.MigrateAsync(Previous); db.ChangeTracker.Clear();
        try
        {
            var sql=condition switch
            {
                "MultipleRoles"=>"INSERT INTO UserRoles(UserId,RoleId) VALUES(@id,'system-admin')",
                "SeparateUsername"=>"UPDATE Users SET UserName='different-login' WHERE Id=@id",
                "TwoFactor"=>"UPDATE Users SET TwoFactorEnabled=1 WHERE Id=@id",
                "ConfirmedEmail"=>"UPDATE Users SET EmailConfirmed=1 WHERE Id=@id",
                _=>"UPDATE Users SET PhoneNumberConfirmed=1 WHERE Id=@id"
            };
            await db.Database.ExecuteSqlRawAsync(sql,new SqlParameter("@id",user.Id));
            var error=await Assert.ThrowsAsync<SqlException>(()=>migrator.MigrateAsync()); Assert.Equal(51000,error.Number);
            Assert.Equal(1,await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables WHERE name='UserRoles'").SingleAsync());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM UserRoles WHERE UserId=@id AND RoleId='system-admin'; UPDATE Users SET UserName=Email,TwoFactorEnabled=0,EmailConfirmed=0,PhoneNumberConfirmed=0 WHERE Id=@id",new SqlParameter("@id",user.Id));
            await migrator.MigrateAsync();
        }
        var restored=await users.FindByIdAsync(user.Id); Assert.NotNull(restored);
        Assert.Equal(hash,restored.PasswordHash); Assert.Equal(securityStamp,restored.SecurityStamp); Assert.Equal(concurrencyStamp,restored.ConcurrencyStamp);
        Assert.Equal("system-student",restored.RoleId); Assert.True(await users.CheckPasswordAsync(restored,"Test-Only!9-password"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>users.AddToRoleAsync(restored,SystemRoles.Admin));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DowngradeCannotDiscardNewWorkflowData(bool proposal)
    {
        await using var db=database.CreateContext(); database.AssertOwnsContext(db);
        var user=new ApplicationUser { Email=$"guard-{Guid.NewGuid():N}@example.test",FullName="Guard",Status=proposal?UserStatus.Active:UserStatus.Pending,RoleId="system-student" };
        user.NormalizedEmail=user.Email.ToUpperInvariant(); db.Users.Add(user); await db.SaveChangesAsync();
        ClubProposal? item=null;
        if(proposal) { item=new(){ApplicantId=user.Id,Name="New proposal",Purpose="Keep history"}; db.ClubProposals.Add(item); await db.SaveChangesAsync(); }
        try
        {
            var error=await Assert.ThrowsAsync<SqlException>(()=>db.GetService<IMigrator>().MigrateAsync(Previous)); Assert.Equal(51000,error.Number);
            Assert.Equal(user.Status,(await db.Users.AsNoTracking().SingleAsync(u=>u.Id==user.Id)).Status);
            if(item is not null) Assert.True(await db.ClubProposals.AnyAsync(p=>p.Id==item.Id));
        }
        finally { if(item is not null) db.ClubProposals.Remove(item); db.Users.Remove(user); await db.SaveChangesAsync(); await db.GetService<IMigrator>().MigrateAsync(); }
    }

    [Fact]
    public async Task SqlStoresOnlySupportedAccountColumns()
    {
        await using var db=database.CreateContext(); database.AssertOwnsContext(db);
        var columns=await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sys.columns WHERE object_id=OBJECT_ID('Users')").ToListAsync();
        Assert.DoesNotContain("UserName",columns); Assert.DoesNotContain("NormalizedUserName",columns);
        Assert.DoesNotContain("EmailConfirmed",columns); Assert.DoesNotContain("PhoneNumberConfirmed",columns);
        Assert.DoesNotContain("TwoFactorEnabled",columns); Assert.DoesNotContain("LockoutEnabled",columns);
        Assert.Contains("RoleId",columns); Assert.Contains("PasswordHash",columns); Assert.Contains("SecurityStamp",columns);
        Assert.Contains("ConcurrencyStamp",columns); Assert.Contains("AccessFailedCount",columns); Assert.Contains("LockoutEnd",columns);
        var roles=await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sys.columns WHERE object_id=OBJECT_ID('Roles') ORDER BY column_id").ToListAsync();
        Assert.Equal(new[]{"Id","Name"},roles); Assert.False(db.Database.HasPendingModelChanges());
    }
}
