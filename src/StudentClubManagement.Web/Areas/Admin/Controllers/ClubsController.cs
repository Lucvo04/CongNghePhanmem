using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Clubs;

namespace StudentClubManagement.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = SystemRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ClubsController(ApplicationDbContext db, UserManager<ApplicationUser> users, ClubManagementService service) : Controller
{
    [HttpGet]
    public IActionResult Create() => View(new CreateClubViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateClubViewModel model)
    {
        model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        model.ManagerStudentCode = model.ManagerStudentCode?.Trim().ToUpperInvariant() ?? string.Empty;
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Category = model.Category?.Trim(); model.Description = model.Description?.Trim();
        ModelState.Clear();
        if (!TryValidateModel(model)) return View(model);
        var result = await service.CreateAsync(model, users.GetUserId(User)!);
        if (result.Code == "Forbidden") return Forbid();
        if (!result.Succeeded)
        {
            if (result.Code == "Conflict") Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(result.Field, result.Message); return View(model);
        }
        TempData["ClubMessage"] = "Đã tạo CLB và phân công chủ nhiệm đầu tiên. Chủ nhiệm có thể vào mục CLB tôi quản lý.";
        return RedirectToAction("Index", "ClubManagement", new { area = "", search = model.Code });
    }

    [HttpGet]
    public async Task<IActionResult> Transfer(int id)
    {
        var club = await db.Clubs.AsNoTracking().SingleOrDefaultAsync(club => club.Id == id);
        if (club is null) return NotFound();
        if (club.Status == ClubStatus.Archived) return Forbid();
        var model = new TransferManagerViewModel { RowVersion = Convert.ToBase64String(club.RowVersion) };
        await PopulateAsync(club, model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Transfer(int id, TransferManagerViewModel model)
    {
        var club = await db.Clubs.AsNoTracking().SingleOrDefaultAsync(club => club.Id == id);
        if (club is null) return NotFound();
        await PopulateAsync(club, model);
        if (!ModelState.IsValid) return View(model);
        var result = await service.TransferAsync(id, model, users.GetUserId(User)!);
        if (result.Code == "Forbidden") return Forbid();
        if (result.Code == "NotFound") return NotFound();
        if (!result.Succeeded)
        {
            if (result.Code == "Conflict") Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(result.Field, result.Message); return View(model);
        }
        TempData["ClubMessage"] = "Đã chuyển giao chủ nhiệm. Người cũ trở thành thành viên và mất quyền quản lý CLB từ yêu cầu tiếp theo.";
        return RedirectToAction("Index", "ClubManagement", new { area = "", search = club.Code });
    }

    private async Task PopulateAsync(Club club, TransferManagerViewModel model)
    {
        var current = await db.Memberships.AsNoTracking().Where(member => member.ClubId == club.Id && member.Role == ClubRole.Manager && member.EndedAt == null)
            .Select(member => new { member.User.FullName, member.User.StudentCode }).SingleOrDefaultAsync();
        model.Club = new(club.Id, club.Code, club.Name, club.Category, club.Status, current?.FullName, current?.StudentCode);
        model.Candidates = await service.EligibleMembers(club.Id).AsNoTracking().OrderBy(member => member.User.StudentCode)
            .Select(member => new ManagerCandidate(member.UserId, member.User.StudentCode!, member.User.FullName)).ToListAsync();
    }
}
