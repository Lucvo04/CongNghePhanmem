using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class ClubEventConfiguration : IEntityTypeConfiguration<ClubEvent>
{
    public void Configure(EntityTypeBuilder<ClubEvent> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(180).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.Location).HasMaxLength(250).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasOne(e => e.Club).WithMany().HasForeignKey(e => e.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ClubId, e.StartsAt });
        builder.ToTable("Events", table =>
        {
            table.HasCheckConstraint("CK_Events_Text", "LEN(LTRIM(RTRIM([Title]))) > 0 AND LEN(LTRIM(RTRIM([Location]))) > 0");
            table.HasCheckConstraint("CK_Events_Period", "[StartsAt] < [EndsAt] AND [RegistrationDeadline] <= [StartsAt]");
            table.HasCheckConstraint("CK_Events_Capacity", "[Capacity] > 0");
            table.HasCheckConstraint("CK_Events_Audience", "[Audience] IN (1,2)");
            table.HasCheckConstraint("CK_Events_Status", "[Status] IN (1,2,3,4)");
            table.HasCheckConstraint("CK_Events_Publication", "([Status] = 1 AND [PublishedAt] IS NULL) OR ([Status] IN (2,4) AND [PublishedAt] IS NOT NULL) OR [Status] = 3");
            table.HasCheckConstraint("CK_Events_FinalizedTime", "[AttendanceFinalizedAt] IS NULL OR ([AttendanceFinalizedAt] >= [EndsAt] AND [PublishedAt] IS NOT NULL AND [Status] IN (2,3,4))");
        });
    }
}
