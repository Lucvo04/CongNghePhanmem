using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class EventRegistrationConfiguration : IEntityTypeConfiguration<EventRegistration>
{
    public void Configure(EntityTypeBuilder<EventRegistration> b)
    {
        b.ToTable("EventRegistrations", t => t.HasCheckConstraint("CK_EventRegistrations_Time", "[CancelledAt] IS NULL OR [CancelledAt] >= [RegisteredAt]"));
        b.HasKey(r => r.Id);
        b.Property(r => r.UserId).HasMaxLength(450);
        b.Property(r => r.RowVersion).IsRowVersion();
        b.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Event).WithMany().HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.EventId, r.UserId }).IsUnique();
        b.HasIndex(r => new { r.UserId, r.RegisteredAt });
    }
}
