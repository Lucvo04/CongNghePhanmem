using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Announcements;

namespace StudentClubManagement.Web.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AnnouncementsController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    ClubAccessService access, AnnouncementService service, EventService events) : Controller
{
    private string Actor => users.GetUserId(User)!;

    [HttpGet, Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> Mine(string? search, int? clubId, InboxStatus status = InboxStatus.All, int page = 1)
    {
        if (!ModelState.IsValid || search?.Length > 180 || !Enum.IsDefined(status)) return BadRequest();
        if (!await service.IsRecipientAccountAsync(Actor)) return NotFound();
        var received = service.ReceivedBy(Actor).AsNoTracking();
        var clubs = await received.Select(r => new { Id = r.Announcement.ClubId, r.Announcement.Club.Name })
            .Distinct().OrderBy(c => c.Name).ThenBy(c => c.Id).Select(c => new InboxClub(c.Id, c.Name)).ToListAsync();
        // Chỉ cho lọc những CLB đã gửi thông báo tới chính người đang đăng nhập.
        if (clubId.HasValue && !clubs.Any(c => c.Id == clubId)) return NotFound();
        var unread = await service.UnreadCountAsync(Actor);
        var query = received;
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(r => r.Announcement.Title.Contains(search));
        if (clubId.HasValue) query = query.Where(r => r.Announcement.ClubId == clubId);
        if (status == InboxStatus.Unread) query = query.Where(r => r.ReadAt == null);
        if (status == InboxStatus.Read) query = query.Where(r => r.ReadAt != null);
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var items = await query.OrderByDescending(r => r.Announcement.PublishedAt).ThenByDescending(r => r.AnnouncementId)
            .Skip((page - 1) * 10).Take(10).Select(r => new InboxItem(r.AnnouncementId, r.Announcement.Title,
                r.Announcement.Club.Name, r.Announcement.PublishedAt!.Value, r.ReadAt, r.Announcement.Status == AnnouncementStatus.Archived)).ToListAsync();
        return View(new AnnouncementInbox(items, clubs, search, clubId, status, page, pages, count, unread));
    }

    [HttpPost, Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await service.MarkReadAsync(id, Actor);
        if (result.Code == "NotFound") return NotFound();
        TempData[result.Succeeded ? "AnnouncementMessage" : "AnnouncementError"] = result.Succeeded ? "Đã đánh dấu thông báo là đã đọc." : result.Message;
        return LocalRedirect(SafeReturnUrl(returnUrl) ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? clubId, string? search, AnnouncementStatus? status, int page = 1)
    {
        if (!ModelState.IsValid || search?.Length > 180 || (status.HasValue && !Enum.IsDefined(status.Value))) return BadRequest();
        if (clubId is null)
        {
            var clubs = await access.ManagedClubs(Actor).AsNoTracking().OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync();
            return clubs.Count == 0 ? NotFound() : View("Clubs", clubs);
        }
        var club = await access.ManagedClubs(Actor).AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId);
        if (club is null) return NotFound();
        var query = service.ManagedBy(Actor).AsNoTracking().Where(a => a.ClubId == clubId);
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(a => a.Title.Contains(search));
        if (status.HasValue) query = query.Where(a => a.Status == status);
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var items = await query.Include(a => a.Event).OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * 10).Take(10).ToListAsync();
        return View(new AnnouncementList(club, items, search, status, page, pages, count));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, string? returnUrl = null)
    {
        var item = await service.VisibleTo(Actor).AsNoTracking().Include(a => a.Club).Include(a => a.Event).SingleOrDefaultAsync(a => a.Id == id);
        if (item is null) return NotFound();
        var manage = await access.CanManageAsync(item.ClubId, Actor);
        var preview = manage && item.Status == AnnouncementStatus.Draft ? await service.PreviewAsync(item) : null;
        var count = manage ? await db.AnnouncementRecipients.CountAsync(r => r.AnnouncementId == id) : 0;
        var history = manage ? await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == nameof(Announcement) && a.EntityId == id.ToString())
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(20)
            .Select(a => new AnnouncementHistory(a.Action, a.Actor.FullName, a.Reason, a.OccurredAt)).ToListAsync() : [];
        var receipt = await service.ReceivedBy(Actor).AsNoTracking().Where(r => r.AnnouncementId == id)
            .Select(r => new AnnouncementReceipt(r.ReadAt)).SingleOrDefaultAsync();
        var viewEvent = item.EventId.HasValue && await events.VisibleTo(Actor).AnyAsync(e => e.Id == item.EventId);
        return View(new AnnouncementDetails(item, manage, preview, count, history, receipt, SafeReturnUrl(returnUrl), viewEvent));
    }

    [HttpGet]
    public async Task<IActionResult> Recipients(int id, int page = 1)
    {
        if (!ModelState.IsValid) return BadRequest();
        var item = await service.ManagedBy(Actor).AsNoTracking().Include(a => a.Club).SingleOrDefaultAsync(a => a.Id == id && a.PublishedAt != null);
        if (item is null) return NotFound();
        var query = db.AnnouncementRecipients.AsNoTracking().Where(r => r.AnnouncementId == id);
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var items = await query.OrderBy(r => r.User.FullName).ThenBy(r => r.UserId).Skip((page - 1) * 10).Take(10)
            .Select(r => new AnnouncementPerson(r.User.FullName, r.User.StudentCode, r.ReadAt)).ToListAsync();
        return View(new AnnouncementRecipients(item, items, page, pages, count, await query.CountAsync(r => r.ReadAt != null)));
    }

    [HttpGet]
    public async Task<IActionResult> Create(int clubId)
    {
        var club = await access.ManagedClubs(Actor).AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId && c.Status == ClubStatus.Active);
        if (club is null) return NotFound();
        var form = new AnnouncementForm { Club = club }; await LoadEventsAsync(form);
        return View("Edit", form);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int clubId, AnnouncementForm form)
    {
        var club = await access.ManagedClubs(Actor).AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId);
        if (club is null) return NotFound();
        form.Club = club; form.Id = null;
        return await SaveAsync(form);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await service.ManagedBy(Actor).AsNoTracking().Include(a => a.Club).SingleOrDefaultAsync(a => a.Id == id);
        if (item is null) return NotFound();
        if (item.Status != AnnouncementStatus.Draft || item.Club.Status != ClubStatus.Active) return RedirectToAction(nameof(Details), new { id });
        var form = new AnnouncementForm { Id = id, Club = item.Club, Title = item.Title, Content = item.Content, EventId = item.EventId, RowVersion = Convert.ToBase64String(item.RowVersion) };
        await LoadEventsAsync(form);
        return View(form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AnnouncementForm form)
    {
        var item = await service.ManagedBy(Actor).AsNoTracking().Include(a => a.Club).SingleOrDefaultAsync(a => a.Id == id);
        if (item is null) return NotFound();
        form.Club = item.Club; form.Id = id;
        return await SaveAsync(form);
    }

    private async Task<IActionResult> SaveAsync(AnnouncementForm form)
    {
        if (ModelState.IsValid)
        {
            var result = await service.SaveAsync(form.Club.Id, form.Id, form, Actor);
            if (result.Code == "NotFound") return NotFound();
            if (result.Succeeded)
            { TempData["AnnouncementMessage"] = "Đã lưu bản nháp. Kiểm tra nội dung và đối tượng trước khi đăng."; return RedirectToAction(nameof(Details), new { id = result.Id }); }
            ModelState.AddModelError("", result.Message);
            if (result.Code == "Conflict") Response.StatusCode = 409;
        }
        await LoadEventsAsync(form);
        return View("Edit", form);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, AnnouncementActionForm form)
    {
        if (!await service.ManagedBy(Actor).AnyAsync(a => a.Id == id)) return NotFound();
        if (!ModelState.IsValid)
        { TempData["AnnouncementError"] = "Thông tin thao tác không hợp lệ. Hãy tải lại trang và kiểm tra lý do."; return RedirectToAction(nameof(Details), new { id }); }
        var result = await service.TransitionAsync(id, Actor, form);
        if (result.Code == "NotFound") return NotFound();
        TempData[result.Succeeded ? "AnnouncementMessage" : "AnnouncementError"] = result.Succeeded
            ? (form.Operation == "Publish" ? "Đã đăng thông báo và lưu danh sách người nhận." : "Đã lưu trữ thông báo. Người nhận vẫn xem được nội dung đã nhận.") : result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task LoadEventsAsync(AnnouncementForm form) => form.Events = await db.Events.AsNoTracking()
        .Where(e => e.ClubId == form.Club.Id && e.PublishedAt != null && (e.Status == EventStatus.Published || e.Status == EventStatus.Completed))
        .OrderByDescending(e => e.StartsAt).ThenByDescending(e => e.Id).Select(e => new AnnouncementEvent(e.Id, e.Title)).ToListAsync();

    private string? SafeReturnUrl(string? value) => value?.Length <= 2048 && Url.IsLocalUrl(value) ? value : null;
}
