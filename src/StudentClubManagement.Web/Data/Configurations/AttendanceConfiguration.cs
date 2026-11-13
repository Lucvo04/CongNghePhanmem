using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> b)
    {
        b.ToTable("Attendances", t =>
        {
            t.HasCheckConstraint("CK_Attendances_Status", "[Status] IN (0,1,2,3)");
            t.HasCheckConstraint("CK_Attendances_FinalResult", "[IsInFinalRoster] = 0 OR [Status] IN (1,2,3)");
        });
        b.HasKey(a => a.RegistrationId);
        b.Property(a => a.RegistrationId).ValueGeneratedNever();
        b.Property(a => a.MarkedById).HasMaxLength(450);
        b.Property(a => a.RowVersion).IsRowVersion();
        b.HasOne(a => a.Registration).WithOne(r => r.Attendance).HasForeignKey<Attendance>(a => a.RegistrationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.MarkedBy).WithMany().HasForeignKey(a => a.MarkedById).OnDelete(DeleteBehavior.Restrict);
    }
}
