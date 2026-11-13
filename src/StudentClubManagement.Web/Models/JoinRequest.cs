namespace StudentClubManagement.Web.Models;

// Một lần nộp đơn. Rút rồi nộp lại tạo bản ghi mới để giữ lịch sử.
public class JoinRequest
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public string? DecidedById { get; set; }
    public ApplicationUser? DecidedBy { get; set; }
    public string? DecisionReason { get; set; }
    public int? MembershipId { get; set; }
    public Membership? Membership { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public enum JoinRequestStatus
{
    Pending = 1,
    Withdrawn = 2,
    Approved = 3,
    Rejected = 4
}

public static class JoinRequestStatusText
{
    public static string Label(this JoinRequestStatus status) => status switch
    {
        JoinRequestStatus.Pending => "Chờ xử lý",
        JoinRequestStatus.Withdrawn => "Đã rút",
        JoinRequestStatus.Approved => "Đã chấp nhận",
        JoinRequestStatus.Rejected => "Đã từ chối",
        _ => "Không xác định"
    };
}
