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
public class EventRegistrationsController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    EventService events, EventRegistrationService registrations) : Controller
{
    private string Actor => users.GetUserId(User)!;
    [HttpPost]
    public async Task<IActionResult> Register(int eventId, EventRegistrationForm form)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await registrations.RegisterAsync(eventId, Actor, form);
        if (result.Code == "NotFound") return NotFound();
        if (result.Code == "Forbidden") return Forbid();
        TempData[result.Succeeded ? "EventMessage" : "EventError"] = result.Succeeded ? "Đăng ký tham gia thành công." : result.Message;
        return RedirectToAction("Details", "Events", new { id = eventId });
    }
    [HttpPost]
    public async Task<IActionResult> Cancel(int id, CancelRegistrationForm form)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await registrations.CancelAsync(id, Actor, form.RowVersion);
        if (result.Code == "NotFound") return NotFound();
        if (result.Code == "Forbidden") return Forbid();
        TempData[result.Succeeded ? "EventMessage" : "EventError"] = result.Succeeded ? "Đã hủy đăng ký tham gia." : result.Message;
        return RedirectToAction(nameof(Mine));
    }
    [HttpGet]
    public async Task<IActionResult> Mine(RegistrationFilter filter)
    {
        if (!await registrations.IsStudentAsync(Actor)) return Forbid();
        return await ListAsync(db.EventRegistrations.Where(r => r.UserId == Actor), null, filter);
    }
    [HttpGet]
    public async Task<IActionResult> Index(int eventId, RegistrationFilter filter)
    {
        var item = await events.ManagedBy(Actor).AsNoTracking().SingleOrDefaultAsync(e => e.Id == eventId);
        return item is null ? NotFound() : await ListAsync(db.EventRegistrations.Where(r => r.EventId == eventId), item, filter);
    }
    [HttpGet]
    public async Task<IActionResult> History(int id, int page = 1, string? returnUrl = null)
    {
        if (!await registrations.IsStudentAsync(Actor)) return Forbid();
        if (!ModelState.IsValid) return BadRequest("Thông tin không hợp lệ.");
        // Quyền lịch sử thuộc chủ đăng ký, kể cả đã rời CLB; không cấp lại quyền đọc nội dung nội bộ.
        var row = await Rows(db.EventRegistrations.Where(r => r.Id == id && r.UserId == Actor), true).SingleOrDefaultAsync();
        if (row is null) return NotFound();
        var query = db.AuditLogs.AsNoTracking().Where(a => a.EntityType == nameof(EventRegistration) && a.EntityId == id.ToString() &&
            (a.Action == "EventRegistered" || a.Action == "EventRegistrationCancelled"));
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var history = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((page - 1) * 10).Take(10)
            .Select(a => new RegistrationHistoryRow(a.Id, a.Action, a.ActorId == Actor, a.BeforeData != null, a.Reason, a.OccurredAt)).ToListAsync();
        var back = returnUrl?.Length <= 2048 && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new RegistrationHistory(row, history, page, pages, count, back));
    }

    private async Task<IActionResult> ListAsync(IQueryable<EventRegistration> query, ClubEvent? item, RegistrationFilter filter)
    {
        // Danh mục CLB chỉ lấy từ đăng ký thuộc người đang xem, không lộ CLB của tài khoản khác.
        var clubs = item is null ? await query.Select(r => new { Id = r.Event.ClubId, r.Event.Club.Name })
            .Distinct().OrderBy(c => c.Name).ThenBy(c => c.Id).Select(c => new CalendarClub(c.Id, c.Name)).ToListAsync() : [];
        if (item is null && filter.ClubId.HasValue && !clubs.Any(c => c.Id == filter.ClubId))
            ModelState.AddModelError("ClubId", "CLB không nằm trong lịch sử đăng ký của bạn.");
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            return View("Index", new RegistrationList([], item, 1, 1, 0, filter, clubs));
        }
        filter.Search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(filter.Search))
            query = item is null
                ? query.Where(r => r.Event.Title.Contains(filter.Search) || r.Event.Club.Name.Contains(filter.Search))
                : query.Where(r => r.User.FullName.Contains(filter.Search) || (r.User.StudentCode != null && r.User.StudentCode.Contains(filter.Search)));
        if (filter.Status == "confirmed") query = query.Where(r => r.CancelledAt == null);
        if (filter.Status == "cancelled") query = query.Where(r => r.CancelledAt != null);
        if (item is null)
        {
            if (filter.ClubId.HasValue) query = query.Where(r => r.Event.ClubId == filter.ClubId);
            if (filter.From.HasValue)
            {
                var from = new DateTimeOffset(filter.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
                query = query.Where(r => r.Event.StartsAt >= from);
            }
            if (filter.To.HasValue)
            {
                var until = new DateTimeOffset(filter.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
                query = query.Where(r => r.Event.StartsAt < until);
            }
        }
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d));
        var page = Math.Clamp(filter.Page, 1, pages);
        var rows = await Rows(query.OrderByDescending(r => r.RegisteredAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * 10).Take(10), item is null).ToListAsync();
        return View("Index", new RegistrationList(rows, item, page, pages, count, filter, clubs));
    }

    private IQueryable<RegistrationRow> Rows(IQueryable<EventRegistration> query, bool personal)
    {
        var now = DateTimeOffset.UtcNow;
        var visible = events.VisibleTo(Actor);
        return query.AsNoTracking().Select(r => new RegistrationRow(r.Id, r.EventId, r.Event.Title, r.Event.Club.Name,
                r.Event.StartsAt, r.User.FullName, r.User.StudentCode, r.RegisteredAt, r.CancelledAt, r.RowVersion,
                personal && r.CancelledAt == null && r.Event.Status == EventStatus.Published && r.Event.StartsAt > now,
                visible.Any(e => e.Id == r.EventId),
                r.CancelledAt == null ? null : db.AuditLogs.Where(a => a.EntityType == nameof(EventRegistration) &&
                    a.EntityId == r.Id.ToString() && a.Action == "EventRegistrationCancelled")
                    .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Select(a => a.Reason).FirstOrDefault(), r.Event.Status));
    }
}
