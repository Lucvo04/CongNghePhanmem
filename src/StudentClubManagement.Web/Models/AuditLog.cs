namespace StudentClubManagement.Web.Models;

// Chỉ lưu các trường nghiệp vụ cần truy vết; không serialize ApplicationUser.
public class AuditLog
{
    public long Id { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public ApplicationUser Actor { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? BeforeData { get; set; }
    public string AfterData { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
