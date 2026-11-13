using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
        builder.Ignore(u => u.UserName);
        builder.Ignore(u => u.NormalizedUserName);
        builder.Ignore(u => u.EmailConfirmed);
        builder.Ignore(u => u.PhoneNumberConfirmed);
        builder.Ignore(u => u.TwoFactorEnabled);
        builder.Ignore(u => u.LockoutEnabled);
        builder.Property(u => u.RoleId).HasMaxLength(450);
        builder.HasOne<Microsoft.AspNetCore.Identity.IdentityRole>().WithMany().HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256);
        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.StudentCode).HasMaxLength(24).UseCollation("Latin1_General_100_CI_AS");
        builder.Property(u => u.ClassName).HasMaxLength(50);
        builder.Property(u => u.Faculty).HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.NormalizedEmail).IsRequired();
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        builder.HasIndex(u => u.StudentCode).IsUnique().HasFilter("[StudentCode] IS NOT NULL");

        builder.ToTable("Users", table =>
        {
            table.HasCheckConstraint("CK_Users_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_Users_FullName", "LEN(LTRIM(RTRIM([FullName]))) > 0");
            table.HasCheckConstraint("CK_Users_StudentCode", "[StudentCode] IS NULL OR LEN(LTRIM(RTRIM([StudentCode]))) > 0");
        });
    }
}
