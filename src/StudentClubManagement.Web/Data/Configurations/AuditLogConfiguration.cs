using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(log => log.ActorId).HasMaxLength(450).IsRequired();
        builder.Property(log => log.Action).HasMaxLength(64).IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(log => log.EntityId).HasMaxLength(450).IsRequired();
        builder.Property(log => log.BeforeData).HasMaxLength(2000);
        builder.Property(log => log.AfterData).HasMaxLength(2000).IsRequired();
        builder.Property(log => log.Reason).HasMaxLength(500);
        builder.HasOne(log => log.Actor).WithMany().HasForeignKey(log => log.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(log => new { log.EntityType, log.EntityId, log.OccurredAt });
    }
}
