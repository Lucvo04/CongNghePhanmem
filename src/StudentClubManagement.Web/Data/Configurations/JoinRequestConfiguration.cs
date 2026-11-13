using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class JoinRequestConfiguration : IEntityTypeConfiguration<JoinRequest>
{
    public void Configure(EntityTypeBuilder<JoinRequest> builder)
    {
        builder.Property(r => r.UserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.DecidedById).HasMaxLength(450);
        builder.Property(r => r.DecisionReason).HasMaxLength(500);
        builder.HasOne(r => r.DecidedBy).WithMany().HasForeignKey(r => r.DecidedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Membership).WithMany().HasForeignKey(r => r.MembershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.MembershipId).IsUnique().HasFilter("[MembershipId] IS NOT NULL");
        builder.Property(r => r.RowVersion).IsRowVersion();
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Club).WithMany().HasForeignKey(r => r.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.UserId, r.ClubId }).IsUnique()
            .HasDatabaseName("UX_JoinRequests_Pending").HasFilter("[Status] = 1");
        builder.HasIndex(r => new { r.UserId, r.SubmittedAt });
        builder.HasIndex(r => new { r.ClubId, r.Status, r.SubmittedAt });
        builder.ToTable("JoinRequests", table =>
        {
            table.HasCheckConstraint("CK_JoinRequests_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_JoinRequests_Decision",
                "([Status] IN (1,2) AND [DecidedById] IS NULL AND [DecisionReason] IS NULL AND [MembershipId] IS NULL) OR " +
                "([Status] = 3 AND [DecidedById] IS NOT NULL AND [DecisionReason] IS NULL AND [MembershipId] IS NOT NULL) OR " +
                "([Status] = 4 AND [DecidedById] IS NOT NULL AND [MembershipId] IS NULL AND [DecisionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([DecisionReason]))) > 0)");
            table.HasCheckConstraint("CK_JoinRequests_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
            table.HasCheckConstraint("CK_JoinRequests_Period",
                "([Status] = 1 AND [ClosedAt] IS NULL) OR ([Status] IN (2,3,4) AND [ClosedAt] >= [SubmittedAt] AND [ClosedAt] IS NOT NULL)");
        });
    }
}
