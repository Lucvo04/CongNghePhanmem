using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Events;

namespace StudentClubManagement.Web.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class EventsController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    ClubAccessService access, EventService service, EventRegistrationService registrations) : Controller
{
    private string Actor => users.GetUserId(User)!;
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Calendar(EventCalendarViewModel model)
    {
        model.Now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(model.Now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        model.From ??= today;
        // Chặn giá trị biên trước AddDays/chuyển UTC để đầu vào sai không gây lỗi 500.
        if (model.From.Value.Year is < 1900 or > 9998)
            ModelState.AddModelError(nameof(model.From), "Ngày bắt đầu cần nằm trong năm 1900–9998.");
        model.To ??= model.From.Value.Year is >= 1900 and <= 9998 ? model.From.Value.AddDays(30) : today.AddDays(30);
        if (model.To.Value.Year is < 1900 or > 9998)
            ModelState.AddModelError(nameof(model.To), "Ngày kết thúc cần nằm trong năm 1900–9998.");
        if (model.From > model.To)
            ModelState.AddModelError(nameof(model.To), "Đến ngày phải bằng hoặc sau Từ ngày.");
        if (model.To.Value.DayNumber - model.From.Value.DayNumber > 365)
            ModelState.AddModelError(nameof(model.To), "Mỗi lần xem tối đa 366 ngày. Hãy chọn khoảng thời gian ngắn hơn.");
        if (model.Status.HasValue && model.Status is not (EventStatus.Published or EventStatus.Cancelled or EventStatus.Completed))
            ModelState.AddModelError(nameof(model.Status), "Chỉ lọc hoạt động đã công bố, đã hủy hoặc đã hoàn thành.");
        model.Clubs = await db.Clubs.AsNoTracking().Where(c => c.Status != ClubStatus.Archived)
            .OrderBy(c => c.Name).ThenBy(c => c.Id).Select(c => new CalendarClub(c.Id, c.Name)).ToListAsync();
        if (model.ClubId.HasValue && !model.Clubs.Any(c => c.Id == model.ClubId))
            ModelState.AddModelError(nameof(model.ClubId), "CLB được chọn không còn trong danh mục. Vui lòng chọn lại.");
        if (!ModelState.IsValid) { Response.StatusCode = 400; return View(model); }

        var fromUtc = new DateTimeOffset(model.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var untilUtc = new DateTimeOffset(model.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var query = service.CalendarFor(users.GetUserId(User)).AsNoTracking().Where(e => e.StartsAt >= fromUtc && e.StartsAt < untilUtc);
        if (model.ClubId.HasValue) query = query.Where(e => e.ClubId == model.ClubId);
        if (model.Status.HasValue) query = query.Where(e => e.Status == model.Status);
        model.Search = model.Search?.Trim();
        if (!string.IsNullOrEmpty(model.Search)) query = query.Where(e => e.Title.Contains(model.Search));
        // Lọc quyền trước cả Count và phân trang để không tiết lộ số hoạt động nội bộ.
        model.Count = await query.CountAsync(); model.Pages = Math.Max(1, (int)Math.Ceiling(model.Count / 10d));
        model.Page = Math.Clamp(model.Page, 1, model.Pages);
        model.Items = await query.OrderBy(e => e.StartsAt).ThenBy(e => e.Id).Skip((model.Page - 1) * 10).Take(10)
            .Select(e => new CalendarEvent(e.Id, e.ClubId, e.Club.Name, e.Title, e.Location, e.StartsAt, e.EndsAt, e.Audience, e.Status)).ToListAsync();
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Index(int clubId, string? search, EventStatus? status, int page = 1)
    {
        if (!ModelState.IsValid || search?.Length > 180 || (status.HasValue && !Enum.IsDefined(status.Value))) return BadRequest();
        var club = await access.ManagedClubs(Actor).AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId);
        if (club is null) return NotFound();
        var query = service.ManagedBy(Actor).AsNoTracking().Where(e => e.ClubId == clubId);
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(e => e.Title.Contains(search));
        if (status.HasValue) query = query.Where(e => e.Status == status);
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        return View(new EventList(club, await query.OrderByDescending(e => e.StartsAt).ThenByDescending(e => e.Id).Skip((page - 1) * 10).Take(10).ToListAsync(), search, status, page, pages, count));
    }
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Details(int id, string? returnUrl = null)
    {
        var item = await service.VisibleTo(users.GetUserId(User)).AsNoTracking().Include(e => e.Club).SingleOrDefaultAsync(e => e.Id == id);
        if (item is null) return NotFound();
        if (returnUrl?.Length <= 2048 && Url.IsLocalUrl(returnUrl)) ViewData["CalendarReturnUrl"] = returnUrl;
        var manage = User.Identity?.IsAuthenticated == true && await access.CanManageAsync(item.ClubId, Actor);
        var history = manage ? await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == nameof(ClubEvent) && a.EntityId == id.ToString())
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(20)
            .Select(a => new EventHistory(a.Action, a.Actor.FullName, a.Reason, a.OccurredAt)).ToListAsync() : [];
        var notices = manage ? await db.Announcements.AsNoTracking().Where(a => a.EventId == id && a.SourceKey != null)
            .OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Take(5)
            .Select(a => new EventNoticeLink(a.Id, a.Title, a.PublishedAt!.Value, db.AnnouncementRecipients.Count(r => r.AnnouncementId == a.Id))).ToListAsync() : [];
        return View(new EventDetails(item, manage, history, await registrations.PanelAsync(item, users.GetUserId(User)), notices));
    }
    [HttpGet]
    public async Task<IActionResult> Create(int clubId)
    {
        var club = await access.ManagedClubs(Actor).AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId && c.Status == ClubStatus.Active);
        return club is null ? NotFound() : View("Edit", new EventForm { Club = club });
    }
    [HttpPost]
    public async Task<IActionResult> Create(int clubId, EventForm form)
    {
        var club = await access.ManagedClubs(Actor).AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId);
        if (club is null) return NotFound();
        form.Club = club; form.Id = null; form.IsPublished = false;
        if (!ModelState.IsValid) return View("Edit", form);
        return await SaveResult(await service.SaveAsync(clubId, null, form, Actor), form);
    }
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await service.ManagedBy(Actor).AsNoTracking().Include(e => e.Club).SingleOrDefaultAsync(e => e.Id == id);
        if (item is null) return NotFound();
        if (!Editable(item)) return RedirectToAction(nameof(Details), new { id });
        return View(EventForm.From(item));
    }
    [HttpPost]
    public async Task<IActionResult> Edit(int id, EventForm form)
    {
        var item = await service.ManagedBy(Actor).AsNoTracking().Include(e => e.Club).SingleOrDefaultAsync(e => e.Id == id);
        if (item is null) return NotFound();
        form.Club = item.Club; form.Id = id; form.IsPublished = item.Status == EventStatus.Published;
        if (!ModelState.IsValid) return View(form);
        return await SaveResult(await service.SaveAsync(item.ClubId, id, form, Actor), form);
    }
    private Task<IActionResult> SaveResult(EventResult result, EventForm form)
    {
        if (result.Code == "NotFound") return Task.FromResult<IActionResult>(NotFound());
        if (result.Succeeded)
        { TempData["EventMessage"] = string.IsNullOrEmpty(result.Message) ? "Đã lưu hoạt động." : result.Message; return Task.FromResult<IActionResult>(RedirectToAction(nameof(Details), new { id = result.Id })); }
        ModelState.AddModelError("", result.Message);
        if (result.Code == "Conflict") Response.StatusCode = 409;
        return Task.FromResult<IActionResult>(View("Edit", form));
    }
    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, EventActionForm form)
    {
        if (!await service.ManagedBy(Actor).AnyAsync(e => e.Id == id)) return NotFound();
        if (!ModelState.IsValid) { TempData["EventError"] = "Thông tin thao tác không hợp lệ. Vui lòng tải lại trang và kiểm tra lý do."; return RedirectToAction(nameof(Details), new { id }); }
        var result = await service.TransitionAsync(id, Actor, form);
        if (result.Code == "NotFound") return NotFound();
        TempData[result.Succeeded ? "EventMessage" : "EventError"] = result.Succeeded && string.IsNullOrEmpty(result.Message) ? "Đã cập nhật trạng thái hoạt động." : result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }
    public static bool Editable(ClubEvent e) => e.Club.Status == ClubStatus.Active && (e.Status == EventStatus.Draft || (e.Status == EventStatus.Published && e.StartsAt > DateTimeOffset.UtcNow));
}
