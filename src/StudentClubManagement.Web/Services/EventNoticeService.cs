using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.Web.Services;

// Được EventService gọi trước thay đổi, trong transaction đang giữ khóa CLB.
public class EventNoticeService(ApplicationDbContext db)
{
    public static List<string> ScheduleChanges(ClubEvent item, EventForm form)
    {
        var lines = new List<string>();
        void Time(string label, DateTimeOffset before, DateTime after)
        {
            var utc = new DateTimeOffset(DateTime.SpecifyKind(after, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).ToUniversalTime();
            if (before != utc) lines.Add($"{label}: {before.Vietnam()} → {utc.Vietnam()}");
        }
        Time("Bắt đầu", item.StartsAt, form.StartsAt!.Value);
        Time("Kết thúc", item.EndsAt, form.EndsAt!.Value);
        Time("Hạn đăng ký", item.RegistrationDeadline, form.RegistrationDeadline!.Value);
        if (item.Location != form.Location.Trim()) lines.Add($"Địa điểm: {item.Location} → {form.Location.Trim()}");
        return lines;
    }

    public async Task<int> StageAsync(ClubEvent item, string actor, IReadOnlyList<string>? changes, string reason)
    {
        if (db.Database.CurrentTransaction is null || item.Status != EventStatus.Published || item.PublishedAt is null)
            throw new InvalidOperationException("Thông báo hoạt động cần transaction và hoạt động đã công bố, trước khi thay đổi.");
        var cancelled = changes is null;
        var operation = cancelled ? "Cancel" : "Schedule";
        var source = $"event:{item.Id}:{Convert.ToHexString(item.RowVersion)}:{operation}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        // Lấy tất cả đăng ký còn hiệu lực, không lọc theo trạng thái tài khoản hiện tại.
        // Tài khoản bị khóa giữ snapshot nhưng chỉ đọc được khi đủ quyền trở lại.
        var recipients = await db.EventRegistrations.AsNoTracking().Where(r => r.EventId == item.Id && r.CancelledAt == null)
            .Select(r => r.UserId).Distinct().ToListAsync();
        var heading = cancelled ? "Hủy hoạt động" : "Thay đổi lịch hoạt động";
        var title = $"{heading}: {item.Title}";
        if (title.Length > 180)
        {
            var end = char.IsHighSurrogate(title[175]) ? 175 : 176;
            title = title[..end].TrimEnd() + "…";
        }
        var body = cancelled
            ? $"Hoạt động “{item.Title}” đã bị hủy.\n\nLịch đã công bố (giờ Việt Nam):\nBắt đầu: {item.StartsAt.Vietnam()}\nKết thúc: {item.EndsAt.Vietnam()}\nĐịa điểm: {item.Location}\n\nĐăng ký tham gia của bạn đã được hủy cùng hoạt động."
            : $"Lịch của hoạt động “{item.Title}” đã thay đổi (giờ Việt Nam):\n\n{string.Join("\n", changes!)}\n\nĐăng ký của bạn được giữ nguyên. Hãy kiểm tra kế hoạch mới trước khi tham gia.";
        body += $"\n\nLý do: {reason.Trim()}";
        var now = DateTimeOffset.UtcNow;
        var notice = new Announcement
        {
            ClubId = item.ClubId, EventId = item.Id, Title = title, Content = body,
            Status = AnnouncementStatus.Published, CreatedAt = now, PublishedAt = now, SourceKey = key
        };
        db.Announcements.Add(notice);
        db.AnnouncementRecipients.AddRange(recipients.Select(userId => new AnnouncementRecipient { Announcement = notice, UserId = userId }));
        // Lấy Id để audit; transaction ngoài chưa commit nên lỗi ở bước sau vẫn rollback tất cả.
        await db.SaveChangesAsync();
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor, EntityType = nameof(Announcement), EntityId = notice.Id.ToString(), Action = "EventNoticePublished",
            AfterData = JsonSerializer.Serialize(new { EventId = item.Id, item.ClubId, Operation = operation, RecipientCount = recipients.Count, SourceKey = key }),
            Reason = reason.Trim(), OccurredAt = now
        });
        return recipients.Count;
    }
}
