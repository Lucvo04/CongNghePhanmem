using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.Web.Services;

public class EventRegistrationService(ApplicationDbContext db, ClubOperationLocks locks)
{
    public Task<bool> IsStudentAsync(string actor) => db.Users.AnyAsync(u =>
        u.Id == actor && u.Status == UserStatus.Active && u.RoleId == "system-student");

    public async Task<RegistrationPanel> PanelAsync(ClubEvent item, string? actor)
    {
        var own = actor is null ? null : await db.EventRegistrations.AsNoTracking().SingleOrDefaultAsync(r => r.EventId == item.Id && r.UserId == actor);
        var count = await db.EventRegistrations.CountAsync(r => r.EventId == item.Id && r.CancelledAt == null);
        var now = DateTimeOffset.UtcNow;
        var student = actor is not null && await IsStudentAsync(actor);
        var eligible = student && (item.Audience == EventAudience.School || await IsMemberAsync(item.ClubId, actor!));
        var message = item.Status != EventStatus.Published ? "Hoạt động chưa mở hoặc đã kết thúc/hủy." :
            item.Club.Status != ClubStatus.Active ? "CLB hiện không nhận đăng ký." :
            item.StartsAt <= now || item.RegistrationDeadline <= now ? "Đã hết hạn đăng ký." :
            actor is null ? "Đăng nhập bằng tài khoản sinh viên đã được duyệt để đăng ký." :
            !eligible ? "Chỉ sinh viên đủ điều kiện tham gia mới được đăng ký." :
            own is { CancelledAt: null } ? "Bạn đã đăng ký tham gia hoạt động này." :
            count >= item.Capacity ? "Hoạt động đã đủ số chỗ." : "Còn chỗ. Bạn có thể đăng ký tham gia.";
        return new(count, own, eligible && item.Status == EventStatus.Published && item.Club.Status == ClubStatus.Active &&
            item.StartsAt > now && item.RegistrationDeadline > now && count < item.Capacity && (own is null || own.CancelledAt != null),
            student && own is { CancelledAt: null } && item.Status == EventStatus.Published && item.StartsAt > now, message);
    }

    public Task<WorkflowResult> RegisterAsync(int eventId, string actor, EventRegistrationForm form) => WorkflowTransaction.RunAsync(db, async () =>
    {
        var clubId = await db.Events.Where(e => e.Id == eventId).Select(e => (int?)e.ClubId).SingleOrDefaultAsync();
        if (clubId is null) return new("NotFound");
        if (!await locks.AccountAsync(actor) || !await locks.ClubAsync(clubId.Value)) return WorkflowTransaction.Conflict();
        if (!await IsStudentAsync(actor)) return new("Forbidden");
        var item = await db.Events.AsNoTracking().Include(e => e.Club).SingleAsync(e => e.Id == eventId);
        if (item.Audience == EventAudience.Club && !await IsMemberAsync(item.ClubId, actor)) return new("NotFound");
        if (item.PublishedAt is null || item.Club.Status == ClubStatus.Archived) return new("NotFound");
        if (Convert.ToBase64String(item.RowVersion) != form.EventVersion) return WorkflowTransaction.Conflict();
        var now = DateTimeOffset.UtcNow;
        if (item.Status != EventStatus.Published || item.Club.Status != ClubStatus.Active || item.RegistrationDeadline <= now || item.StartsAt <= now)
            return new("Validation", "Hoạt động hiện không nhận đăng ký hoặc đã hết hạn.");
        var own = await db.EventRegistrations.SingleOrDefaultAsync(r => r.EventId == eventId && r.UserId == actor);
        if (own is not null) await db.Entry(own).ReloadAsync();
        if (own is not null && (own.CancelledAt is null || Convert.ToBase64String(own.RowVersion) != form.RowVersion))
            return WorkflowTransaction.Conflict();
        if (own is null && !string.IsNullOrEmpty(form.RowVersion)) return WorkflowTransaction.Conflict();
        if (await db.EventRegistrations.CountAsync(r => r.EventId == eventId && r.CancelledAt == null) >= item.Capacity)
            return new("Conflict", "Hoạt động đã đủ số chỗ. Vui lòng chọn hoạt động khác.");
        var before = own is null ? null : Snapshot(own);
        if (own is null) { own = new() { EventId = eventId, UserId = actor }; db.EventRegistrations.Add(own); }
        own.RegisteredAt = now; own.CancelledAt = null;
        await db.SaveChangesAsync();
        Audit(db, own, actor, "EventRegistered", before, null);
        await db.SaveChangesAsync();
        return new("Success", Id: own.Id);
    });

    public Task<WorkflowResult> CancelAsync(int id, string actor, string rowVersion) => WorkflowTransaction.RunAsync(db, async () =>
    {
        var target = await db.EventRegistrations.AsNoTracking().Where(r => r.Id == id && r.UserId == actor).Select(r => new { r.Event.ClubId }).SingleOrDefaultAsync();
        if (target is null) return new("NotFound");
        if (!await locks.AccountAsync(actor) || !await locks.ClubAsync(target.ClubId)) return WorkflowTransaction.Conflict();
        if (!await IsStudentAsync(actor)) return new("Forbidden");
        var own = await db.EventRegistrations.Include(r => r.Event).SingleAsync(r => r.Id == id && r.UserId == actor);
        await db.Entry(own).ReloadAsync();
        await db.Entry(own.Event).ReloadAsync();
        if (own.CancelledAt != null || Convert.ToBase64String(own.RowVersion) != rowVersion) return WorkflowTransaction.Conflict();
        if (own.Event.Status != EventStatus.Published || own.Event.StartsAt <= DateTimeOffset.UtcNow)
            return new("Validation", "Chỉ tự hủy đăng ký trước khi hoạt động bắt đầu.");
        Cancel(db, own, actor, "Sinh viên tự hủy đăng ký.", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(); return new("Success", Id: own.EventId);
    });

    private Task<bool> IsMemberAsync(int clubId, string actor) => db.Memberships.AnyAsync(m =>
        m.ClubId == clubId && m.UserId == actor && m.Status == MembershipStatus.Active && m.EndedAt == null);

    // Người gọi đã giữ khóa CLB và transaction; dùng chung khi hủy sự kiện hoặc đình chỉ/kết thúc thành viên.
    public static async Task CancelMatchingAsync(ApplicationDbContext db, IQueryable<EventRegistration> query, string actor, string reason, DateTimeOffset now)
    {
        foreach (var item in await query.Where(r => r.CancelledAt == null).ToListAsync()) Cancel(db, item, actor, reason, now);
    }
    private static void Cancel(ApplicationDbContext db, EventRegistration item, string actor, string reason, DateTimeOffset now)
    {
        var before = Snapshot(item); item.CancelledAt = now;
        Audit(db, item, actor, "EventRegistrationCancelled", before, reason);
    }
    private static string Snapshot(EventRegistration r) => JsonSerializer.Serialize(new { r.EventId, r.UserId, r.RegisteredAt, r.CancelledAt });
    private static void Audit(ApplicationDbContext db, EventRegistration r, string actor, string action, string? before, string? reason) =>
        db.AuditLogs.Add(new AuditLog { ActorId = actor, EntityType = nameof(EventRegistration), EntityId = r.Id.ToString(),
            Action = action, BeforeData = before, AfterData = Snapshot(r), Reason = reason });
}
