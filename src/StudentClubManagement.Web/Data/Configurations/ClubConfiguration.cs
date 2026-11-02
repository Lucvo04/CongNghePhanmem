using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.Property(c => c.Code).HasMaxLength(30).UseCollation("Latin1_General_100_CI_AS").IsRequired();
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Category).HasMaxLength(100);
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.ToTable("Clubs", table =>
        {
            table.HasCheckConstraint("CK_Clubs_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Clubs_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_Clubs_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
    }
}
