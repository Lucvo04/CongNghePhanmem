using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.JoinRequests;

namespace StudentClubManagement.Web.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class JoinRequestReviewController(UserManager<ApplicationUser> users,
    ClubAccessService access, JoinRequestReviewService service) : Controller
{
    private string ActorId => users.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index(int clubId, string status = "Pending", int page = 1)
    {
        JoinRequestStatus? filter = null;
        if (!ModelState.IsValid) return BadRequest("Bộ lọc không hợp lệ.");
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<JoinRequestStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed)) return BadRequest("Bộ lọc không hợp lệ.");
            filter = parsed;
        }
        var club = await access.ManagedClubs(ActorId).AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId);
        if (club is null) return NotFound();
        var query = service.VisibleTo(ActorId).Where(r => r.ClubId == clubId);
        if (filter.HasValue) query = query.Where(r => r.Status == filter);
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d));
        page = Math.Clamp(page, 1, pages);
        var rows = await Rows(query.OrderBy(r => r.SubmittedAt).ThenBy(r => r.Id).Skip((page - 1) * 10).Take(10)).ToListAsync();
        return View(new ReviewRequestListViewModel(clubId, club.Name, club.Status, filter, rows, count, page, pages));
    }

    [HttpGet]
    public async Task<IActionResult> Decide(int id)
    {
        if (!ModelState.IsValid) return NotFound();
        var model = await FormAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Decide(int id, ReviewJoinRequestViewModel model)
    {
        var form = await FormAsync(id);
        if (form is null) return NotFound();
        model.Request = form.Request;
        if (model.Decision == JoinRequestStatus.Rejected && string.IsNullOrWhiteSpace(model.DecisionReason))
            ModelState.AddModelError(nameof(model.DecisionReason), "Vui lòng nhập lý do từ chối.");
        if (!ModelState.IsValid) return View(model);
        var result = await service.DecideAsync(id, ActorId, model.RowVersion, model.Decision, model.DecisionReason);
        if (result.Code == "NotFound") return NotFound();
        if (result.Code == "Forbidden") return Forbid();
        if (!result.Succeeded)
        {
            if (result.Code == "Conflict") Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(string.Empty, result.Message);
            var latest = await FormAsync(id);
            if (latest is null) return NotFound();
            model.Request = latest.Request;
            return View(model);
        }
        TempData["ReviewNotice"] = model.Decision == JoinRequestStatus.Approved
            ? "Đã chấp nhận đơn và thêm sinh viên làm thành viên CLB." : "Đã từ chối đơn. Sinh viên có thể xem lý do trong Đơn của tôi.";
        return RedirectToAction(nameof(Index), new { clubId = form.Request.ClubId });
    }

    private async Task<ReviewJoinRequestViewModel?> FormAsync(int id)
    {
        var query = service.VisibleTo(ActorId).AsNoTracking().Where(r => r.Id == id);
        var row = await Rows(query).SingleOrDefaultAsync();
        if (row is null) return null;
        var stamp = await query.Select(r => r.RowVersion).SingleAsync();
        return new() { Request = row, RowVersion = Convert.ToBase64String(stamp) };
    }

    private static IQueryable<ReviewRequestRow> Rows(IQueryable<JoinRequest> query) => query.Select(r => new ReviewRequestRow(
        r.Id, r.ClubId, r.Club.Name, r.Club.Status, r.User.FullName, r.User.StudentCode, r.Reason, r.Status,
        r.SubmittedAt, r.ClosedAt, r.DecidedBy == null ? null : r.DecidedBy.FullName, r.DecisionReason));
}
