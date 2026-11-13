using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.ViewModels.Announcements;

namespace StudentClubManagement.Web.Services;

public class AnnouncementService(ApplicationDbContext db, ClubAccessService access, ClubOperationLocks locks)
{
    public IQueryable<Announcement> ManagedBy(string actor) => db.Announcements.Where(a =>
        access.ManagedClubs(actor).Any(c => c.Id == a.ClubId));

    public Task<bool> IsRecipientAccountAsync(string actor) => db.Users.AnyAsync(u =>
        u.Id == actor && u.Status == UserStatus.Active && u.RoleId == "system-student");

    public IQueryable<AnnouncementRecipient> ReceivedBy(string actor) => db.AnnouncementRecipients.Where(r =>
        r.UserId == actor && r.User.Status == UserStatus.Active && r.User.RoleId == "system-student" &&
        r.Announcement.PublishedAt != null &&
        (r.Announcement.Status == AnnouncementStatus.Published || r.Announcement.Status == AnnouncementStatus.Archived));

    public Task<int> UnreadCountAsync(string actor) => ReceivedBy(actor).CountAsync(r => r.ReadAt == null);

    public Task<WorkflowResult> MarkReadAsync(int id, string actor) => WorkflowTransaction.RunAsync(db, async () =>
    {
        if (!await locks.AccountAsync(actor)) return WorkflowTransaction.Conflict();
        var own = ReceivedBy(actor).Where(r => r.AnnouncementId == id);
        if (!await own.AnyAsync()) return new("NotFound");
        // Cập nhật lần đầu ngay trong SQL; POST lặp không ghi đè thời điểm đã đọc.
        var now = DateTimeOffset.UtcNow;
        await own.Where(r => r.ReadAt == null).ExecuteUpdateAsync(update => update.SetProperty(r => r.ReadAt, now));
        return new("Success", Id: id);
    });

    // Quyền đọc của người nhận dựa trên snapshot, không dựa trên membership hiện tại.
    public IQueryable<Announcement> VisibleTo(string actor) => db.Announcements.Where(a =>
        access.ManagedClubs(actor).Any(c => c.Id == a.ClubId) ||
        ReceivedBy(actor).Any(r => r.AnnouncementId == a.Id));

    private IQueryable<ApplicationUser> Eligible(Announcement item) => db.Users.Where(u =>
        u.Status == UserStatus.Active && u.RoleId == "system-student" &&
        (item.EventId == null
            ? db.Memberships.Any(m => m.ClubId == item.ClubId && m.UserId == u.Id && m.Status == MembershipStatus.Active && m.EndedAt == null)
            : db.EventRegistrations.Any(r => r.EventId == item.EventId && r.UserId == u.Id && r.CancelledAt == null)));

    public async Task<RecipientPreview> PreviewAsync(Announcement item) => Preview(await RecipientIdsAsync(item));
    private Task<List<string>> RecipientIdsAsync(Announcement item) => Eligible(item).Select(u => u.Id).ToListAsync();
    private static RecipientPreview Preview(List<string> ids) => new(ids.Count,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ids.Order(StringComparer.Ordinal))))));

    public Task<WorkflowResult> SaveAsync(int clubId, int? id, AnnouncementForm form, string actor) => WorkflowTransaction.RunAsync(db, async () =>
    {
        if (!await locks.AccountAsync(actor) || !await locks.ClubAsync(clubId)) return WorkflowTransaction.Conflict();
        if (!await access.CanManageAsync(clubId, actor)) return new("NotFound");
        if (!await db.Clubs.AnyAsync(c => c.Id == clubId && c.Status == ClubStatus.Active))
            return new("Validation", "Chỉ soạn hoặc sửa thông báo khi CLB đang hoạt động.");
        Announcement item;
        if (id.HasValue)
        {
            item = (await db.Announcements.SingleOrDefaultAsync(a => a.Id == id && a.ClubId == clubId))!;
            if (item is null) return new("NotFound");
            await db.Entry(item).ReloadAsync();
            if (!Matches(item, form.RowVersion)) return WorkflowTransaction.Conflict();
            if (item.Status != AnnouncementStatus.Draft) return new("Validation", "Thông báo đã đăng không sửa nội dung. Hãy tạo thông báo đính chính mới.");
        }
        else item = new Announcement { ClubId = clubId };
        if (string.IsNullOrWhiteSpace(form.Title) || form.Title.Length > 180 || form.Title.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(form.Content) || form.Content.Length > 4000)
            return new("Validation", "Tiêu đề cần 1–180 ký tự, nội dung cần 1–4.000 ký tự.");
        if (!await ValidEventAsync(clubId, form.EventId)) return InvalidEvent();
        var before = id.HasValue ? Snapshot(item) : null;
        item.Title = form.Title.Trim(); item.Content = form.Content.Trim(); item.EventId = form.EventId;
        if (!id.HasValue) db.Announcements.Add(item);
        await db.SaveChangesAsync();
        Audit(item, actor, id.HasValue ? "AnnouncementUpdated" : "AnnouncementCreated", before);
        await db.SaveChangesAsync();
        return new("Success", Id: item.Id);
    });

    public Task<WorkflowResult> TransitionAsync(int id, string actor, AnnouncementActionForm form) => WorkflowTransaction.RunAsync(db, async () =>
    {
        var clubId = await ManagedBy(actor).Where(a => a.Id == id).Select(a => (int?)a.ClubId).SingleOrDefaultAsync();
        if (clubId is null) return new("NotFound");
        if (!await locks.AccountAsync(actor) || !await locks.ClubAsync(clubId.Value)) return WorkflowTransaction.Conflict();
        if (!await access.CanManageAsync(clubId.Value, actor)) return new("NotFound");
        var item = await db.Announcements.SingleAsync(a => a.Id == id);
        await db.Entry(item).ReloadAsync();
        if (!Matches(item, form.RowVersion)) return WorkflowTransaction.Conflict();
        var clubStatus = await db.Clubs.Where(c => c.Id == clubId).Select(c => c.Status).SingleAsync();
        if (clubStatus == ClubStatus.Archived) return new("Validation", "CLB đã lưu trữ, chỉ được xem lịch sử.");
        var before = Snapshot(item);
        int? count = null;
        string action;
        if (form.Operation == "Publish" && item.Status == AnnouncementStatus.Draft)
        {
            if (clubStatus != ClubStatus.Active) return new("Validation", "Chỉ đăng thông báo khi CLB đang hoạt động.");
            if (!await ValidEventAsync(item.ClubId, item.EventId)) return InvalidEvent();
            var ids = await RecipientIdsAsync(item);
            var preview = Preview(ids);
            if (!string.Equals(form.RecipientVersion, preview.Version, StringComparison.Ordinal))
                return new("Conflict", "Danh sách người nhận đã thay đổi. Hãy kiểm tra lại số người nhận trước khi đăng.");
            if (ids.Count == 0) return new("Validation", "Chưa có người nhận hợp lệ. Thông báo vẫn được giữ ở bản nháp.");
            // Một bản chụp danh sách và một transaction cho nội dung, người nhận, nhật ký.
            db.AnnouncementRecipients.AddRange(ids.Select(userId => new AnnouncementRecipient { AnnouncementId = id, UserId = userId }));
            item.Status = AnnouncementStatus.Published; item.PublishedAt = DateTimeOffset.UtcNow;
            count = ids.Count; action = "AnnouncementPublished";
        }
        else if (form.Operation == "Archive" && item.Status == AnnouncementStatus.Published)
        {
            if (string.IsNullOrWhiteSpace(form.Reason) || form.Reason.Length > 500)
                return new("Validation", "Lưu trữ cần lý do từ 1 đến 500 ký tự.");
            item.Status = AnnouncementStatus.Archived; action = "AnnouncementArchived";
        }
        else return new("Validation", "Thao tác không hợp lệ với trạng thái hiện tại.");
        Audit(item, actor, action, before, form.Operation == "Archive" ? form.Reason : null, count);
        await db.SaveChangesAsync();
        return new("Success", Id: id);
    });

    private Task<bool> ValidEventAsync(int clubId, int? eventId) => eventId is null ? Task.FromResult(true) :
        db.Events.AnyAsync(e => e.Id == eventId && e.ClubId == clubId && e.PublishedAt != null &&
            (e.Status == EventStatus.Published || e.Status == EventStatus.Completed));
    private static WorkflowResult InvalidEvent() => new("Validation", "Chọn hoạt động đã công bố hoặc hoàn thành thuộc chính CLB; hoạt động đã hủy không nhận thông báo mới.");
    private static bool Matches(Announcement item, string? version) => string.Equals(Convert.ToBase64String(item.RowVersion), version, StringComparison.Ordinal);
    private void Audit(Announcement item, string actor, string action, string? before, string? reason = null, int? recipients = null) => db.AuditLogs.Add(new AuditLog
    {
        ActorId = actor, EntityType = nameof(Announcement), EntityId = item.Id.ToString(), Action = action,
        BeforeData = before, AfterData = Snapshot(item, recipients), Reason = reason?.Trim()
    });
    private static string Snapshot(Announcement item, int? recipients = null) => JsonSerializer.Serialize(new
    {
        item.ClubId, item.EventId, item.Title, Status = item.Status.ToString(), item.PublishedAt, RecipientCount = recipients,
        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Content)))
    });
}
