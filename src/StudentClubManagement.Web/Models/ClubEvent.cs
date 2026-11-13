namespace StudentClubManagement.Web.Models;

public class ClubEvent
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public EventAudience Audience { get; set; } = EventAudience.School;
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? AttendanceFinalizedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public enum EventAudience { School = 1, Club = 2 }
public enum EventStatus { Draft = 1, Published = 2, Cancelled = 3, Completed = 4 }
public static class EventLabels
{
    public static string Label(this EventStatus value) => value switch
    { EventStatus.Draft => "Bản nháp", EventStatus.Published => "Đã công bố", EventStatus.Cancelled => "Đã hủy", EventStatus.Completed => "Đã hoàn thành", _ => "Không hợp lệ" };
    public static string Label(this EventAudience value) => value == EventAudience.School ? "Toàn trường" : "Nội bộ CLB";
    public static string Vietnam(this DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
}
