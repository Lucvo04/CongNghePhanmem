using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.Web.Controllers;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AttendancesController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    EventService events, AttendanceService service, EventRegistrationService registrations) : Controller
{
    private string Actor => users.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index(int eventId, string? search, AttendanceStatus? status, int page = 1)
    {
        var item = await events.ManagedBy(Actor).AsNoTracking().Include(e => e.Club).SingleOrDefaultAsync(e => e.Id == eventId);
        if (item is null) return NotFound();
        if (!ModelState.IsValid || search?.Length > 180 || (status.HasValue && !Enum.IsDefined(status.Value)))
            return BadRequest("Bộ lọc không hợp lệ.");
        var query = service.Roster(eventId).AsNoTracking();
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(r => r.User.FullName.Contains(search) || (r.User.StudentCode != null && r.User.StudentCode.Contains(search)));
        if (status.HasValue) query = query.Where(r => (r.Attendance == null ? AttendanceStatus.Unmarked : r.Attendance.Status) == status);
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var rows = await query.OrderBy(r => r.User.FullName).ThenBy(r => r.Id).Skip((page - 1) * 10).Take(10)
            .Select(r => new AttendanceRow(r.Id, r.User.FullName, r.User.StudentCode,
                r.Attendance == null ? AttendanceStatus.Unmarked : r.Attendance.Status,
                r.Attendance == null ? null : r.Attendance.MarkedAt, r.Attendance == null ? null : r.Attendance.MarkedBy.FullName,
                r.RowVersion, r.Attendance == null ? null : r.Attendance.RowVersion, r.CancelledAt != null)).ToListAsync();
        return View(new AttendanceList(item, rows, search, status, count, page, pages, AttendanceService.CanMark(item), AttendanceService.Availability(item),
            await service.SummaryAsync(item), AttendanceService.CanFinalize(item)));
    }

    [HttpPost]
    public async Task<IActionResult> Finalize(int eventId, FinalizeAttendanceForm form)
    {
        if (!ModelState.IsValid) return BadRequest("Thông tin chốt không hợp lệ. Hãy tải lại danh sách.");
        var result = await service.FinalizeAsync(eventId, Actor, form);
        if (result.Code == "NotFound") return NotFound();
        TempData[result.Succeeded ? "AttendanceMessage" : "AttendanceError"] = result.Succeeded ? "Đã chốt danh sách điểm danh." : result.Message;
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpGet]
    public async Task<IActionResult> History(int id, int page = 1, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return BadRequest("Thông tin không hợp lệ.");
        var student = await registrations.IsStudentAsync(Actor);
        var row = await db.EventRegistrations.AsNoTracking().Include(r => r.Event).ThenInclude(e => e.Club)
            .Include(r => r.User).Include(r => r.Attendance).Where(r => r.Id == id &&
                ((student && r.UserId == Actor) || events.ManagedBy(Actor).Any(e => e.Id == r.EventId))).SingleOrDefaultAsync();
        if (row is null) return NotFound();
        var manage = await events.ManagedBy(Actor).AnyAsync(e => e.Id == row.EventId);
        var finalized = row.Event.AttendanceFinalizedAt.HasValue && row.Attendance?.IsInFinalRoster == true;
        var query = db.AuditLogs.AsNoTracking().Where(a => a.EntityType == nameof(Attendance) && a.EntityId == id.ToString() &&
            (a.Action == "AttendanceUpdated" || a.Action == "AttendanceCorrected"));
        // Sinh viên chỉ xem kết quả đã chốt và các lần sửa sau chốt của chính mình.
        if (!manage) query = query.Where(a => finalized && a.Action == "AttendanceCorrected");
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var logs = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((page - 1) * 10).Take(10)
            .Select(a => new { a.Id, a.BeforeData, a.AfterData, a.Reason, a.OccurredAt, Actor = manage ? a.Actor.FullName : "Người quản lý", a.Action }).ToListAsync();
        var history = logs.Select(a => new AttendanceHistoryRow(a.Id, AttendanceService.ReadStatus(a.BeforeData), AttendanceService.ReadStatus(a.AfterData),
            a.Reason, a.OccurredAt, a.Actor, a.Action == "AttendanceCorrected")).ToList();
        var back = returnUrl?.Length <= 2048 && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new AttendanceHistory(id, row.EventId, row.Event.Title, row.Event.Club.Name, row.User.FullName, row.User.StudentCode,
            row.Event.Status, row.Event.AttendanceFinalizedAt, manage || finalized ? row.Attendance?.Status : null,
            row.Attendance?.IsInFinalRoster == true, manage, await events.VisibleTo(Actor).AnyAsync(e => e.Id == row.EventId),
            history, page, pages, count, back));
    }

    [HttpPost]
    public async Task<IActionResult> Save(int id, AttendanceForm form, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return BadRequest("Kiểm tra trạng thái, phiên bản dữ liệu và ghi chú tối đa 500 ký tự.");
        var result = await service.SaveAsync(id, Actor, form);
        if (result.Code == "NotFound") return NotFound();
        TempData[result.Succeeded ? "AttendanceMessage" : "AttendanceError"] = result.Succeeded ? "Đã lưu kết quả điểm danh." : result.Message;
        if (returnUrl?.Length <= 2048 && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        var eventId = await db.EventRegistrations.Where(r => r.Id == id).Select(r => r.EventId).SingleAsync();
        return RedirectToAction(nameof(Index), new { eventId });
    }
}
