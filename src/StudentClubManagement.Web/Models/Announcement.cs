namespace StudentClubManagement.Web.Models;

public class Announcement
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;
    // Không có EventId: gửi thành viên CLB. Có EventId: gửi người đăng ký hoạt động đó.
    public int? EventId { get; set; }
    public ClubEvent? Event { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public string? SourceKey { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class AnnouncementRecipient
{
    public int AnnouncementId { get; set; }
    public Announcement Announcement { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset? ReadAt { get; set; }
}

public enum AnnouncementStatus { Draft = 1, Published = 2, Archived = 3 }
public static class AnnouncementLabels
{
    public static string Label(this AnnouncementStatus value) => value switch
    { AnnouncementStatus.Draft => "Bản nháp", AnnouncementStatus.Published => "Đã đăng", AnnouncementStatus.Archived => "Đã lưu trữ", _ => "Không hợp lệ" };
}
