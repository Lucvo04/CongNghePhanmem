using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.StudentCode).HasMaxLength(24).UseCollation("Latin1_General_100_CI_AS");
        builder.Property(u => u.ClassName).HasMaxLength(50);
        builder.Property(u => u.Faculty).HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.NormalizedEmail).IsRequired();
        builder.Property(u => u.UserName).IsRequired();
        builder.Property(u => u.NormalizedUserName).IsRequired();
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        builder.HasIndex(u => u.StudentCode).IsUnique().HasFilter("[StudentCode] IS NOT NULL");

        builder.ToTable("AspNetUsers", table =>
        {
            table.HasCheckConstraint("CK_Users_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint("CK_Users_FullName", "LEN(LTRIM(RTRIM([FullName]))) > 0");
            table.HasCheckConstraint("CK_Users_StudentCode", "[StudentCode] IS NULL OR LEN(LTRIM(RTRIM([StudentCode]))) > 0");
        });
    }
}
