using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Memberships;

namespace StudentClubManagement.Web.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MembershipsController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    ClubAccessService access, MembershipService service) : Controller
{
    private string ActorId => users.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index(int clubId, string? search, string status = "current", int page = 1)
    {
        var club = await access.ManagedClubs(ActorId).AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId);
        if (club is null) return NotFound();
        return await ListAsync(service.ManagedBy(ActorId).Where(m => m.ClubId == clubId), club, search, status, page);
    }

    [HttpGet]
    public async Task<IActionResult> Mine(string status = "current", int page = 1)
    {
        if (!await service.CanUseOwnAsync(ActorId)) return Forbid();
        return await ListAsync(db.Memberships.Where(m => m.UserId == ActorId), null, null, status, page);
    }

    private async Task<IActionResult> ListAsync(IQueryable<Membership> query, Club? club, string? search, string status, int page)
    {
        search = search?.Trim();
        if (!ModelState.IsValid || search?.Length > 150) return BadRequest("Bộ lọc không hợp lệ.");
        status = status.ToLowerInvariant();
        if (status == "current") query = query.Where(m => m.EndedAt == null);
        else if (status != "all")
        {
            if (!Enum.TryParse<MembershipStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed)) return BadRequest("Trạng thái không hợp lệ.");
            query = query.Where(m => m.Status == parsed);
        }
        if (!string.IsNullOrEmpty(search)) query = query.Where(m => m.User.FullName.Contains(search) || (m.User.StudentCode != null && m.User.StudentCode.Contains(search)));
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var rows = await Rows(query.AsNoTracking().OrderBy(m => m.User.FullName).ThenBy(m => m.Club.Name).ThenByDescending(m => m.Id)
            .Skip((page - 1) * 10).Take(10)).ToListAsync();
        return View("Index", new MemberListViewModel(club?.Id, club?.Name, club?.Status, search, status, rows, count, page, pages));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, int page = 1)
    {
        if (!ModelState.IsValid) return BadRequest("Thông tin không hợp lệ.");
        var row = await Rows(service.VisibleTo(ActorId).AsNoTracking().Where(m => m.Id == id)).SingleOrDefaultAsync();
        if (row is null) return NotFound();
        var query = db.AuditLogs.AsNoTracking().Where(a => a.EntityType == nameof(Membership) && a.EntityId == id.ToString() &&
            (a.Action == "MembershipSuspended" || a.Action == "MembershipRestored" || a.Action == "MembershipEnded"));
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var history = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((page - 1) * 10).Take(10)
            .Select(a => new MemberHistoryRow(a.Id, a.Action, a.Actor.FullName, a.Reason, a.OccurredAt)).ToListAsync();
        return View(new MemberDetailsViewModel(row, await access.CanManageAsync(row.ClubId, ActorId), row.UserId == ActorId, history, count, page, pages));
    }

    [HttpGet]
    public Task<IActionResult> ChangeStatus(int id) => ShowFormAsync(id, false);
    [HttpGet]
    public Task<IActionResult> Leave(int id) => ShowFormAsync(id, true);
    [HttpPost]
    public Task<IActionResult> ChangeStatus(int id, ChangeMemberStatusViewModel model) => SaveAsync(id, model, false);
    [HttpPost]
    public Task<IActionResult> Leave(int id, ChangeMemberStatusViewModel model) => SaveAsync(id, model, true);

    private async Task<IActionResult> ShowFormAsync(int id, bool selfLeave)
    {
        if (!ModelState.IsValid) return NotFound();
        var row = await FormRowAsync(id, selfLeave);
        return row is null ? NotFound() : View("ChangeStatus", new ChangeMemberStatusViewModel
            { Member = row, SelfLeave = selfLeave, RowVersion = Convert.ToBase64String(row.RowVersion), NewStatus = selfLeave ? MembershipStatus.Left : 0 });
    }

    private async Task<IActionResult> SaveAsync(int id, ChangeMemberStatusViewModel model, bool selfLeave)
    {
        var row = await FormRowAsync(id, selfLeave);
        if (row is null) return NotFound();
        model.Member = row; model.SelfLeave = selfLeave;
        if (selfLeave) { model.NewStatus = MembershipStatus.Left; ModelState.Remove(nameof(model.NewStatus)); }
        if (!ModelState.IsValid) return View("ChangeStatus", model);
        var result = await service.ChangeStatusAsync(id, ActorId, model.RowVersion, model.NewStatus, model.Reason, selfLeave);
        if (result.Code == "NotFound") return NotFound();
        if (result.Code == "Forbidden") return Forbid();
        if (!result.Succeeded)
        {
            if (result.Code == "Conflict") Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(string.Empty, result.Message);
            var latest = await FormRowAsync(id, selfLeave);
            if (latest is null) return NotFound();
            model.Member = latest; return View("ChangeStatus", model);
        }
        TempData["MembershipNotice"] = selfLeave ? "Bạn đã rời CLB. Lịch sử tham gia được giữ lại." : "Đã cập nhật trạng thái thành viên và lưu lịch sử.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<MemberRow?> FormRowAsync(int id, bool selfLeave)
    {
        if (selfLeave && !await service.CanUseOwnAsync(ActorId)) return null;
        var query = selfLeave ? db.Memberships.Where(m => m.UserId == ActorId) : service.ManagedBy(ActorId);
        return await Rows(query.AsNoTracking().Where(m => m.Id == id)).SingleOrDefaultAsync();
    }

    private static IQueryable<MemberRow> Rows(IQueryable<Membership> query) => query.Select(m => new MemberRow(
        m.Id, m.ClubId, m.Club.Name, m.Club.Status, m.UserId, m.User.FullName, m.User.StudentCode, m.User.ClassName,
        m.User.Faculty, m.User.Status, m.Role, m.Status, m.JoinedAt, m.EndedAt, m.RowVersion));
}
