using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.Property(m => m.RowVersion).IsRowVersion();
        builder.HasOne(m => m.User).WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Club).WithMany(c => c.Memberships)
            .HasForeignKey(m => m.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.UserId, m.ClubId })
            .HasDatabaseName("UX_Memberships_OpenPeriod").IsUnique()
            .HasFilter("[EndedAt] IS NULL");
        // Chỉ đảm bảo tối đa một chủ nhiệm. Tạo CLB/chuyển giao phải dùng transaction ở tầng nghiệp vụ.
        builder.HasIndex(m => m.ClubId)
            .HasDatabaseName("UX_Memberships_CurrentManager").IsUnique()
            .HasFilter("[Role] = 2 AND [EndedAt] IS NULL");
        builder.HasIndex(m => new { m.ClubId, m.Status });
        builder.ToTable("Memberships", table =>
        {
            table.HasCheckConstraint("CK_Memberships_Role", "[Role] IN (1, 2)");
            table.HasCheckConstraint("CK_Memberships_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Memberships_Period",
                "([Status] IN (1, 2) AND [EndedAt] IS NULL) OR " +
                "([Status] = 3 AND [EndedAt] IS NOT NULL AND [EndedAt] >= [JoinedAt])");
            table.HasCheckConstraint("CK_Memberships_ManagerActive",
                "[Role] <> 2 OR [EndedAt] IS NOT NULL OR [Status] = 1");
        });
    }
}
