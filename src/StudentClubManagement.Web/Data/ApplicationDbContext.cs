using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Dữ liệu tham chiếu cố định; không tạo tài khoản hoặc mật khẩu mặc định.
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "system-admin", Name = SystemRoles.Admin, NormalizedName = "ADMIN",
                ConcurrencyStamp = "seed-admin-v1"
            },
            new IdentityRole
            {
                Id = "system-student", Name = SystemRoles.Student, NormalizedName = "STUDENT",
                ConcurrencyStamp = "seed-student-v1"
            });
    }
}
