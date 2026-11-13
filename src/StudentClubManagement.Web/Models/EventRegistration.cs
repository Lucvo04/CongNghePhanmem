namespace StudentClubManagement.Web.Models;

// Một dòng/người/hoạt động. CancelledAt null = còn đăng ký; lịch sử các lần đổi nằm ở AuditLogs.
public class EventRegistration
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public ClubEvent Event { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CancelledAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Attendance? Attendance { get; set; }
}
