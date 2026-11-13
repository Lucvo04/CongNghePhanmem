using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Data.Configurations;

public class ClubProposalConfiguration : IEntityTypeConfiguration<ClubProposal>
{
    public void Configure(EntityTypeBuilder<ClubProposal> b)
    {
        b.Property(p => p.ApplicantId).HasMaxLength(450).IsRequired();
        b.Property(p => p.Name).HasMaxLength(150).IsRequired();
        b.Property(p => p.Category).HasMaxLength(100);
        b.Property(p => p.Purpose).HasMaxLength(2000).IsRequired();
        b.Property(p => p.RowVersion).IsRowVersion();
        b.HasOne(p => p.Applicant).WithMany().HasForeignKey(p => p.ApplicantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Club).WithMany().HasForeignKey(p => p.ClubId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.ApplicantId).IsUnique().HasFilter("[Status] = 1");
        b.HasIndex(p => p.ClubId).IsUnique().HasFilter("[ClubId] IS NOT NULL");
        b.HasIndex(p => new { p.Status, p.SubmittedAt });
        b.ToTable("ClubProposals", t =>
        {
            t.HasCheckConstraint("CK_ClubProposals_Status", "[Status] IN (1,2,3,4)");
            t.HasCheckConstraint("CK_ClubProposals_Club", "([Status]=2 AND [ClubId] IS NOT NULL) OR ([Status]<>2 AND [ClubId] IS NULL)");
            t.HasCheckConstraint("CK_ClubProposals_Text", "LEN(LTRIM(RTRIM([Name])))>0 AND LEN(LTRIM(RTRIM([Purpose])))>0");
        });
    }
}
