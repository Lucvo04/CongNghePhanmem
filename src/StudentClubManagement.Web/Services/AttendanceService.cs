using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.Web.Services;

public class AttendanceService(ApplicationDbContext db, ClubOperationLocks locks, ClubAccessService access)
{
    // Giữ kết quả đã ghi khi hủy cả hoạt động; chỉ được đọc lịch sử, không đổi kết quả.
    public IQueryable<EventRegistration> Roster(int eventId) => db.EventRegistrations.Where(r => r.EventId == eventId &&
        (r.Event.AttendanceFinalizedAt != null ? r.Attendance != null && r.Attendance.IsInFinalRoster :
            ((r.CancelledAt == null && r.RegisteredAt <= r.Event.StartsAt) || r.Attendance != null)));

    private IQueryable<EventRegistration> Eligible(int eventId) => db.EventRegistrations.Where(r => r.EventId == eventId &&
        r.CancelledAt == null && r.RegisteredAt <= r.Event.StartsAt);

    public static bool CanMark(ClubEvent item) => item.PublishedAt != null &&
        item.Status is EventStatus.Published or EventStatus.Completed && item.Club.Status != ClubStatus.Archived &&
        item.StartsAt <= DateTimeOffset.UtcNow;

    public static bool CanFinalize(ClubEvent item) => CanMark(item) && item.AttendanceFinalizedAt == null && item.EndsAt <= DateTimeOffset.UtcNow;

    public static string Availability(ClubEvent item) => item.Club.Status == ClubStatus.Archived ? "CLB đã lưu trữ, chỉ xem kết quả đã ghi." :
        item.Status == EventStatus.Cancelled ? "Hoạt động đã hủy. Kết quả cũ được giữ để tra cứu, không tính là hoạt động tham dự hợp lệ." :
        item.PublishedAt is null || item.Status == EventStatus.Draft ? "Chỉ điểm danh hoạt động đã công bố." :
        item.StartsAt > DateTimeOffset.UtcNow ? "Chưa đến giờ bắt đầu. Danh sách hiện chỉ để kiểm tra; chưa thể điểm danh." :
        item.AttendanceFinalizedAt != null ? "Danh sách đã chốt. Chỉ sửa kết quả trong danh sách này, bắt buộc có lý do; không thêm người hoặc đưa về Chưa điểm danh." :
        "Có thể ghi hoặc sửa kết quả. Chưa điểm danh được giữ riêng với vắng; kết quả hiện chưa chốt.";

    // Dấu phiên bản bao gồm toàn bộ danh sách, không chỉ trang hoặc bộ lọc đang xem.
    public async Task<AttendanceSummary> SummaryAsync(ClubEvent item)
    {
        var query = item.AttendanceFinalizedAt.HasValue ? Roster(item.Id) : Eligible(item.Id);
        var rows = await query.AsNoTracking().OrderBy(r => r.Id).Select(r => new
        {
            r.Id, r.RowVersion, Version = r.Attendance == null ? null : r.Attendance.RowVersion,
            Status = r.Attendance == null ? AttendanceStatus.Unmarked : r.Attendance.Status
        }).ToListAsync();
        var stamp = string.Join(";", rows.Select(r => $"{r.Id}:{Convert.ToBase64String(r.RowVersion)}:{(r.Version is null ? "" : Convert.ToBase64String(r.Version))}:{(int)r.Status}"));
        return new(rows.Count, rows.Count(r => r.Status == AttendanceStatus.Unmarked), rows.Count(r => r.Status == AttendanceStatus.Present),
            rows.Count(r => r.Status == AttendanceStatus.Absent), rows.Count(r => r.Status == AttendanceStatus.Excused),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stamp))));
    }

    public Task<WorkflowResult> FinalizeAsync(int eventId, string actor, FinalizeAttendanceForm form) => WorkflowTransaction.RunAsync(db, async () =>
    {
        var clubId = await db.Events.Where(e => e.Id == eventId).Select(e => (int?)e.ClubId).SingleOrDefaultAsync();
        if (clubId is null) return new("NotFound");
        if (!await locks.AccountAsync(actor) || !await locks.ClubAsync(clubId.Value)) return WorkflowTransaction.Conflict();
        if (!await access.CanManageAsync(clubId.Value, actor)) return new("NotFound");
        var item = await db.Events.Include(e => e.Club).SingleAsync(e => e.Id == eventId);
        await db.Entry(item).ReloadAsync(); await db.Entry(item.Club).ReloadAsync();
        if (item.AttendanceFinalizedAt.HasValue || Convert.ToBase64String(item.RowVersion) != form.EventVersion) return WorkflowTransaction.Conflict();
        if (!CanFinalize(item)) return new("Validation", "Chỉ chốt sau giờ kết thúc, khi hoạt động không bị hủy và CLB chưa lưu trữ.");
        var summary = await SummaryAsync(item);
        if (summary.Version != form.RosterVersion) return WorkflowTransaction.Conflict();
        if (summary.Unmarked > 0 && !form.MarkUnmarkedAbsent)
            return new("Validation", $"Còn {summary.Unmarked} người chưa điểm danh. Hãy xử lý hết hoặc xác nhận chuyển những người này sang Vắng.");
        var now = DateTimeOffset.UtcNow;
        foreach (var registration in await Eligible(eventId).Include(r => r.Attendance).ToListAsync())
        {
            var attendance = registration.Attendance;
            if (attendance is not null) await db.Entry(attendance).ReloadAsync();
            if (attendance is null || attendance.Status == AttendanceStatus.Unmarked)
            {
                var before = attendance is null ? null : Snapshot(attendance);
                if (attendance is null) { attendance = new() { RegistrationId = registration.Id }; db.Attendances.Add(attendance); }
                attendance.Status = AttendanceStatus.Absent; attendance.MarkedAt = now; attendance.MarkedById = actor;
                attendance.IsInFinalRoster = true;
                Audit(attendance, actor, "AttendanceUpdated", before, "Chuyển người chưa điểm danh sang Vắng theo xác nhận khi chốt danh sách.");
            }
            attendance.IsInFinalRoster = true;
        }
        item.AttendanceFinalizedAt = now;
        db.AuditLogs.Add(new AuditLog { ActorId = actor, EntityType = nameof(ClubEvent), EntityId = item.Id.ToString(), Action = "AttendanceFinalized",
            AfterData = JsonSerializer.Serialize(new { item.AttendanceFinalizedAt, summary.Total, summary.Present, Absent = summary.Absent + summary.Unmarked, summary.Excused }) });
        await db.SaveChangesAsync();
        return new("Success", Id: eventId);
    });

    public Task<WorkflowResult> SaveAsync(int registrationId, string actor, AttendanceForm form) => WorkflowTransaction.RunAsync(db, async () =>
    {
        var target = await db.EventRegistrations.Where(r => r.Id == registrationId)
            .Select(r => new { r.Event.ClubId }).SingleOrDefaultAsync();
        if (target is null) return new("NotFound");
        if (!await locks.AccountAsync(actor) || !await locks.ClubAsync(target.ClubId)) return WorkflowTransaction.Conflict();
        if (!await access.CanManageAsync(target.ClubId, actor)) return new("NotFound");
        var row = await db.EventRegistrations.AsNoTracking().Include(r => r.Event).ThenInclude(e => e.Club)
            .SingleAsync(r => r.Id == registrationId);
        if (!CanMark(row.Event)) return new("Validation", Availability(row.Event));
        if (row.CancelledAt != null || row.RegisteredAt > row.Event.StartsAt)
            return new("Validation", "Chỉ điểm danh người đã đăng ký hợp lệ trước khi hoạt động bắt đầu.");
        if (!Enum.IsDefined(form.Status) || form.Reason?.Length > 500)
            return new("Validation", "Kết quả không hợp lệ hoặc ghi chú dài quá 500 ký tự.");
        if (Convert.ToBase64String(row.Event.RowVersion) != form.EventVersion ||
            Convert.ToBase64String(row.RowVersion) != form.RegistrationVersion) return WorkflowTransaction.Conflict();
        var attendance = await db.Attendances.SingleOrDefaultAsync(a => a.RegistrationId == registrationId);
        if (attendance is not null) await db.Entry(attendance).ReloadAsync();
        if (attendance is null ? !string.IsNullOrEmpty(form.RowVersion) : Convert.ToBase64String(attendance.RowVersion) != form.RowVersion)
            return WorkflowTransaction.Conflict();
        var finalized = row.Event.AttendanceFinalizedAt.HasValue;
        if (finalized && (attendance?.IsInFinalRoster != true || form.Status == AttendanceStatus.Unmarked || string.IsNullOrWhiteSpace(form.Reason)))
            return new("Validation", "Sau chốt chỉ sửa người trong danh sách, cần lý do và không được đưa về Chưa điểm danh.");
        if ((attendance?.Status ?? AttendanceStatus.Unmarked) == form.Status) return new("Success", Id: row.EventId);
        var before = attendance is null ? null : Snapshot(attendance);
        if (attendance is null) { attendance = new() { RegistrationId = registrationId }; db.Attendances.Add(attendance); }
        attendance.Status = form.Status; attendance.MarkedAt = DateTimeOffset.UtcNow; attendance.MarkedById = actor;
        Audit(attendance, actor, finalized ? "AttendanceCorrected" : "AttendanceUpdated", before, form.Reason);
        await db.SaveChangesAsync();
        return new("Success", Id: row.EventId);
    });

    private void Audit(Attendance item, string actor, string action, string? before, string? reason) => db.AuditLogs.Add(new AuditLog
    {
        ActorId = actor, EntityType = nameof(Attendance), EntityId = item.RegistrationId.ToString(), Action = action,
        BeforeData = before, AfterData = Snapshot(item), Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
    });
    private static string Snapshot(Attendance item) => JsonSerializer.Serialize(new
        { item.RegistrationId, Status = item.Status.ToString(), item.MarkedAt, item.MarkedById, item.IsInFinalRoster });

    public static AttendanceStatus? ReadStatus(string? json)
    {
        if (json is null) return null;
        try
        {
            using var data = JsonDocument.Parse(json);
            return data.RootElement.ValueKind == JsonValueKind.Object && data.RootElement.TryGetProperty("Status", out var status) &&
                status.ValueKind == JsonValueKind.String && Enum.TryParse<AttendanceStatus>(status.GetString(), out var value) && Enum.IsDefined(value) ? value : null;
        }
        catch (JsonException) { return null; }
    }
}
