using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.Property(a => a.Title).HasMaxLength(180).IsRequired();
        builder.Property(a => a.Content).HasMaxLength(4000).IsRequired();
        builder.Property(a => a.RowVersion).IsRowVersion();
        builder.Property(a => a.SourceKey).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(a => a.SourceKey).IsUnique().HasFilter("[SourceKey] IS NOT NULL");
        builder.HasOne(a => a.Club).WithMany().HasForeignKey(a => a.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Event).WithMany().HasForeignKey(a => a.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.ClubId, a.CreatedAt });
        builder.ToTable("Announcements", table =>
        {
            table.HasCheckConstraint("CK_Announcements_Text", "LEN(LTRIM(RTRIM([Title]))) > 0 AND LEN(LTRIM(RTRIM([Content]))) > 0");
            table.HasCheckConstraint("CK_Announcements_Source", "[SourceKey] IS NULL OR (LEN([SourceKey]) = 64 AND [EventId] IS NOT NULL AND [Status] IN (2,3))");
            table.HasCheckConstraint("CK_Announcements_Publication", "([Status] = 1 AND [PublishedAt] IS NULL) OR ([Status] IN (2,3) AND [PublishedAt] IS NOT NULL AND [PublishedAt] >= [CreatedAt])");
        });
    }
}

public class AnnouncementRecipientConfiguration : IEntityTypeConfiguration<AnnouncementRecipient>
{
    public void Configure(EntityTypeBuilder<AnnouncementRecipient> builder)
    {
        builder.ToTable("AnnouncementRecipients");
        builder.HasKey(r => new { r.AnnouncementId, r.UserId });
        builder.HasOne(r => r.Announcement).WithMany().HasForeignKey(r => r.AnnouncementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
