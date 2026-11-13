namespace StudentClubManagement.Web.Models;

public class ClubProposal
{
    public int Id { get; set; }
    public string ApplicantId { get; set; } = "";
    public ApplicationUser Applicant { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string Purpose { get; set; } = "";
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public int? ClubId { get; set; }
    public Club? Club { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public enum ProposalStatus { Pending = 1, Approved = 2, Rejected = 3, Withdrawn = 4 }
public static class ProposalLabels
{
    public static string Label(this ProposalStatus value) => value switch
    { ProposalStatus.Pending => "Chờ duyệt", ProposalStatus.Approved => "Đã chấp nhận", ProposalStatus.Rejected => "Đã từ chối", _ => "Đã rút đơn" };
}
