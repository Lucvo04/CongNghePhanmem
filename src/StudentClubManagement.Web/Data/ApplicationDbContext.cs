using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<IdentityRole> Roles => Set<IdentityRole>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();
    public DbSet<ClubEvent> Events => Set<ClubEvent>();
    public DbSet<ClubProposal> ClubProposals => Set<ClubProposal>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementRecipient> AnnouncementRecipients => Set<AnnouncementRecipient>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Mỗi user có một RoleId; vai trò chủ nhiệm vẫn nằm ở Memberships.
        builder.Entity<IdentityRole>(role =>
        {
            role.ToTable("Roles");
            role.HasKey(r => r.Id);
            role.Property(r => r.Name).HasMaxLength(256);
            role.Ignore(r => r.NormalizedName);
            role.Ignore(r => r.ConcurrencyStamp);
            role.HasIndex(r => r.Name).IsUnique().HasDatabaseName("RoleNameIndex");
        });

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
