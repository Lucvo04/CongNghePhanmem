using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.Web.Services;

public record EventResult(string Code, string Message = "", int? Id = null)
{ public bool Succeeded => Code == "Success"; }

public class EventService(ApplicationDbContext db, ClubAccessService access, ClubOperationLocks locks, EventNoticeService notices)
{
    public IQueryable<ClubEvent> ManagedBy(string actor) => db.Events.Where(e => access.ManagedClubs(actor).Any(c => c.Id == e.ClubId));
    // Lịch dành cho người xem chỉ gồm hoạt động đã công bố; nháp nằm ở màn hình quản lý.
    public IQueryable<ClubEvent> CalendarFor(string? actor) => VisibleTo(actor).Where(e =>
        e.PublishedAt != null && e.Status != EventStatus.Draft && e.Club.Status != ClubStatus.Archived);
    public IQueryable<ClubEvent> VisibleTo(string? actor) => db.Events.Where(e =>
        (actor != null && access.ManagedClubs(actor).Any(c => c.Id == e.ClubId)) ||
        (e.PublishedAt != null && e.Status != EventStatus.Draft && e.Club.Status != ClubStatus.Archived &&
            (e.Audience == EventAudience.School || (actor != null && db.Memberships.Any(m => m.ClubId == e.ClubId &&
                m.UserId == actor && m.Status == MembershipStatus.Active && m.EndedAt == null && m.User.Status == UserStatus.Active &&
                m.User.RoleId == "system-student")))));

    public Task<EventResult> SaveAsync(int clubId, int? id, EventForm form, string actor) => TransactionAsync(async () =>
    {
        if (!await locks.AccountAsync(actor) || !await locks.ClubAsync(clubId)) return Conflict();
        if (!await access.CanManageAsync(clubId, actor)) return new("NotFound");
        var club = await db.Clubs.AsNoTracking().SingleAsync(c => c.Id == clubId);
        if (club.Status != ClubStatus.Active) return new("Validation", "Chỉ tạo hoặc sửa hoạt động khi CLB đang hoạt động.");
        ClubEvent item;
        if (id.HasValue)
        {
            item = (await db.Events.SingleOrDefaultAsync(e => e.Id == id && e.ClubId == clubId))!;
            if (item is null) return new("NotFound");
            await db.Entry(item).ReloadAsync();
            if (!Matches(item, form.RowVersion)) return Conflict();
            if (item.Status is not (EventStatus.Draft or EventStatus.Published)) return new("Validation", "Hoạt động đã hủy hoặc hoàn thành chỉ được xem lịch sử.");
            if (item.Status == EventStatus.Published && item.StartsAt <= DateTimeOffset.UtcNow)
                return new("Validation", "Hoạt động đã bắt đầu, không thể sửa nội dung hoặc lịch.");
            if (item.Status == EventStatus.Published && (item.Audience != form.Audience || string.IsNullOrWhiteSpace(form.Reason)))
                return new("Validation", "Sau công bố cần lý do chỉnh sửa và không được đổi đối tượng tham gia.");
        }
        else item = new ClubEvent { ClubId = clubId };

        if (!Valid(form)) return new("Validation", "Kiểm tra tên, địa điểm, đối tượng, sức chứa và thời gian: hạn đăng ký ≤ bắt đầu < kết thúc; thời điểm bắt đầu phải ở tương lai (giờ Việt Nam).");
        if (id.HasValue && form.Capacity < await db.EventRegistrations.CountAsync(r => r.EventId == id && r.CancelledAt == null))
            return new("Validation", "Sức chứa không được nhỏ hơn số người đang đăng ký.");
        var before = id.HasValue ? Snapshot(item) : null;
        int? notified = null;
        if (item.Status == EventStatus.Published)
        {
            var changes = EventNoticeService.ScheduleChanges(item, form);
            if (changes.Count > 0) notified = await notices.StageAsync(item, actor, changes, form.Reason!);
        }
        item.Title = form.Title.Trim(); item.Description = form.Description?.Trim() ?? ""; item.Location = form.Location.Trim();
        item.StartsAt = Utc(form.StartsAt!.Value); item.EndsAt = Utc(form.EndsAt!.Value); item.RegistrationDeadline = Utc(form.RegistrationDeadline!.Value);
        item.Capacity = form.Capacity; item.Audience = form.Audience;
        if (!id.HasValue) db.Events.Add(item);
        await db.SaveChangesAsync();
        AddAudit(item, actor, id.HasValue ? "EventUpdated" : "EventCreated", before, form.Reason);
        await db.SaveChangesAsync();
        return new("Success", notified.HasValue ? $"Đã cập nhật hoạt động và lưu thông báo cho {notified} người đăng ký." : "", item.Id);
    });

    public Task<EventResult> TransitionAsync(int id, string actor, EventActionForm form) => TransactionAsync(async () =>
    {
        var clubId = await ManagedBy(actor).Where(e => e.Id == id).Select(e => (int?)e.ClubId).SingleOrDefaultAsync();
        if (clubId is null) return new("NotFound");
        if (!await locks.AccountAsync(actor) || !await locks.ClubAsync(clubId.Value)) return Conflict();
        if (!await access.CanManageAsync(clubId.Value, actor)) return new("NotFound");
        var item = await db.Events.Include(e => e.Club).SingleAsync(e => e.Id == id);
        await db.Entry(item).ReloadAsync();
        if (!Matches(item, form.RowVersion)) return Conflict();
        var clubStatus = await db.Clubs.AsNoTracking().Where(c => c.Id == clubId).Select(c => c.Status).SingleAsync();
        if (clubStatus == ClubStatus.Archived) return new("Validation", "CLB đã lưu trữ, chỉ được xem lịch sử.");
        var now = DateTimeOffset.UtcNow;
        var before = Snapshot(item);
        string action;
        int? notified = null;
        switch (form.Operation)
        {
            case "Publish" when item.Status == EventStatus.Draft:
                if (clubStatus != ClubStatus.Active || item.StartsAt <= now || item.RegistrationDeadline <= now)
                    return new("Validation", "Công bố cần CLB đang hoạt động, thời gian bắt đầu và hạn đăng ký ở tương lai.");
                item.Status = EventStatus.Published; item.PublishedAt = now; action = "EventPublished"; break;
            case "Cancel" when item.Status is EventStatus.Draft or EventStatus.Published:
                if (string.IsNullOrWhiteSpace(form.Reason) || form.Reason.Length > 500)
                    return new("Validation", "Hủy hoạt động cần lý do từ 1 đến 500 ký tự.");
                if (item.Status == EventStatus.Published) notified = await notices.StageAsync(item, actor, null, form.Reason);
                item.Status = EventStatus.Cancelled; action = "EventCancelled"; break;
            case "Complete" when item.Status == EventStatus.Published:
                if (item.EndsAt > now) return new("Validation", "Chỉ đánh dấu hoàn thành sau thời gian kết thúc.");
                item.Status = EventStatus.Completed; action = "EventCompleted"; break;
            default: return new("Validation", "Thao tác không hợp lệ với trạng thái hiện tại.");
        }
        if (item.Status == EventStatus.Cancelled)
            await EventRegistrationService.CancelMatchingAsync(db, db.EventRegistrations.Where(r => r.EventId == id), actor,
                form.Reason!.Trim(), now);
        AddAudit(item, actor, action, before, form.Operation == "Cancel" ? form.Reason : null);
        await db.SaveChangesAsync();
        return new("Success", notified.HasValue ? $"Đã hủy hoạt động và lưu thông báo cho {notified} người đăng ký." : "", id);
    });

    private async Task<EventResult> TransactionAsync(Func<Task<EventResult>> operation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await operation();
            if (result.Succeeded) await transaction.CommitAsync();
            else { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
            return result;
        }
        catch (DbUpdateConcurrencyException)
        { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); return Conflict(); }
    }
    private void AddAudit(ClubEvent item, string actor, string action, string? before, string? reason) => db.AuditLogs.Add(new AuditLog
    {
        ActorId = actor, EntityType = nameof(ClubEvent), EntityId = item.Id.ToString(), Action = action,
        BeforeData = before, AfterData = Snapshot(item), Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
    });
    private static bool Matches(ClubEvent item, string? value) => string.Equals(Convert.ToBase64String(item.RowVersion), value, StringComparison.Ordinal);
    // datetime-local gửi giờ không có múi giờ: luôn hiểu là UTC+7, không phụ thuộc múi giờ máy chủ.
    private static DateTimeOffset Utc(DateTime value) => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).ToUniversalTime();
    private static bool Valid(EventForm f) => !string.IsNullOrWhiteSpace(f.Title) && f.Title.Length <= 180 && !f.Title.Any(char.IsControl) &&
        !string.IsNullOrWhiteSpace(f.Location) && f.Location.Length <= 250 && !f.Location.Any(char.IsControl) && (f.Description?.Length ?? 0) <= 4000 &&
        (f.Reason?.Length ?? 0) <= 500 && Enum.IsDefined(f.Audience) && f.Capacity > 0 &&
        f.StartsAt.HasValue && f.EndsAt.HasValue && f.RegistrationDeadline.HasValue &&
        f.StartsAt.Value.Year >= 2000 && f.EndsAt.Value.Year >= 2000 && f.RegistrationDeadline.Value.Year >= 2000 &&
        f.RegistrationDeadline <= f.StartsAt && f.StartsAt < f.EndsAt && Utc(f.StartsAt.Value) > DateTimeOffset.UtcNow;
    private static string Snapshot(ClubEvent item) => JsonSerializer.Serialize(new
    {
        item.ClubId, item.Title, item.Location, item.StartsAt, item.EndsAt, item.RegistrationDeadline, item.Capacity,
        Audience = item.Audience.ToString(), Status = item.Status.ToString(), item.PublishedAt,
        DescriptionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Description)))
    }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    private static EventResult Conflict() => new("Conflict", "Hoạt động hoặc quyền quản lý vừa thay đổi. Vui lòng tải lại trang và thử lại.");
}
