using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.JoinRequests;

namespace StudentClubManagement.Web.Controllers;

[Authorize(Roles = SystemRoles.Student)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class JoinRequestsController(ApplicationDbContext db, UserManager<ApplicationUser> users, JoinRequestService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var actorId = users.GetUserId(User)!;
        if (!await service.IsStudentAsync(actorId)) return Forbid();
        if (!ModelState.IsValid) return BadRequest("Số trang không hợp lệ.");
        var query = db.JoinRequests.AsNoTracking().Where(r => r.UserId == actorId);
        var count = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(count / 10d));
        page = Math.Clamp(page, 1, totalPages);
        var rows = await query.OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * 10).Take(10)
            .Select(r => new JoinRequestRow(r.Id, r.ClubId, r.Club.Name, r.Club.Status,
                r.Reason, r.Status, r.SubmittedAt, r.ClosedAt, r.DecisionReason,
                r.DecidedBy == null ? null : r.DecidedBy.FullName)).ToListAsync();
        return View(new JoinRequestListViewModel(rows, count, page, totalPages));
    }

    [HttpGet]
    public async Task<IActionResult> Create(int clubId)
    {
        var actorId = users.GetUserId(User)!;
        if (!await service.IsStudentAsync(actorId)) return Forbid();
        if (!ModelState.IsValid || clubId <= 0) return NotFound();
        var model = await CreateFormAsync(clubId, actorId);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateJoinRequestViewModel model)
    {
        var actorId = users.GetUserId(User)!;
        if (!await service.IsStudentAsync(actorId)) return Forbid();
        var form = await CreateFormAsync(model.ClubId, actorId);
        if (form is null) return NotFound();
        form.Reason = model.Reason;
        if (!ModelState.IsValid) return View(form);
        var result = await service.SubmitAsync(model.ClubId, actorId, model.Reason);
        if (result.Code == "Forbidden") return Forbid();
        if (result.Code == "NotFound") return NotFound();
        if (!result.Succeeded)
        {
            if (result.Code == "Conflict") Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(string.Empty, result.Message);
            form = await CreateFormAsync(model.ClubId, actorId);
            if (form is null) return NotFound();
            form.Reason = model.Reason;
            return View(form);
        }
        TempData["JoinRequestNotice"] = "Đã gửi đơn gia nhập. Đơn đang chờ xử lý; bạn chưa trở thành thành viên CLB.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Withdraw(int id)
    {
        var actorId = users.GetUserId(User)!;
        if (!await service.IsStudentAsync(actorId)) return Forbid();
        if (!ModelState.IsValid) return NotFound();
        var form = await WithdrawFormAsync(id, actorId);
        return form is null ? NotFound() : View(form);
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id, WithdrawJoinRequestViewModel model)
    {
        var actorId = users.GetUserId(User)!;
        if (!await service.IsStudentAsync(actorId)) return Forbid();
        var form = await WithdrawFormAsync(id, actorId);
        if (form is null) return NotFound();
        if (!ModelState.IsValid) { Response.StatusCode = 400; return View(form); }
        var result = await service.WithdrawAsync(id, actorId, model.RowVersion);
        if (result.Code == "Forbidden") return Forbid();
        if (result.Code == "NotFound") return NotFound();
        if (!result.Succeeded)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(string.Empty, result.Message);
            form = await WithdrawFormAsync(id, actorId);
            return form is null ? NotFound() : View(form);
        }
        TempData["JoinRequestNotice"] = "Đã rút đơn. Lịch sử được giữ lại; bạn có thể nộp đơn mới nếu đủ điều kiện.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CreateJoinRequestViewModel?> CreateFormAsync(int id, string actorId)
    {
        var club = await db.Clubs.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.Status != ClubStatus.Archived);
        if (club is null) return null;
        return new() { ClubId = id, ClubName = club.Name,
            UnavailableReason = await service.UnavailableReasonAsync(id, club.Status, actorId) };
    }

    private async Task<WithdrawJoinRequestViewModel?> WithdrawFormAsync(int id, string actorId)
    {
        var request = await db.JoinRequests.AsNoTracking().Include(r => r.Club)
            .SingleOrDefaultAsync(r => r.Id == id && r.UserId == actorId);
        return request is null ? null : new() { Id = id, ClubName = request.Club.Name, Reason = request.Reason,
            Status = request.Status, RowVersion = Convert.ToBase64String(request.RowVersion) };
    }
}
