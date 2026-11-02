using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Clubs;

namespace StudentClubManagement.Web.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ClubManagementController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    ClubAccessService access, ClubManagementService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        search = search?.Trim();
        if (!ModelState.IsValid || search?.Length > 150) return BadRequest("Bộ lọc không hợp lệ.");
        var actorId = users.GetUserId(User)!;
        var query = access.ManagedClubs(actorId).AsNoTracking();
        if (!string.IsNullOrEmpty(search)) query = query.Where(club => club.Name.Contains(search) || club.Code.Contains(search));
        var count = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var clubs = await query.OrderBy(club => club.Code).ThenBy(club => club.Id).Skip((page - 1) * 10).Take(10)
            .Select(club => new ClubManagementRow(club.Id, club.Code, club.Name, club.Category, club.Status,
                club.Memberships.Where(m => m.Role == ClubRole.Manager && m.EndedAt == null).Select(m => m.User.FullName).FirstOrDefault(),
                club.Memberships.Where(m => m.Role == ClubRole.Manager && m.EndedAt == null).Select(m => m.User.StudentCode).FirstOrDefault()))
            .ToListAsync();
        return View(new ClubListViewModel { IsAdmin = await access.IsAdminAsync(actorId), Search = search, Page = page,
            TotalPages = pages, TotalCount = count, Clubs = clubs });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!await access.CanManageAsync(id, users.GetUserId(User)!)) return Forbid();
        var club = await db.Clubs.AsNoTracking().SingleAsync(club => club.Id == id);
        if (club.Status == ClubStatus.Archived) return Forbid();
        return View(new EditClubViewModel { ClubId = club.Id, Code = club.Code, Name = club.Name, Category = club.Category,
            Description = club.Description, RowVersion = Convert.ToBase64String(club.RowVersion) });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, EditClubViewModel model)
    {
        if (!await access.CanManageAsync(id, users.GetUserId(User)!)) return Forbid();
        var club = await db.Clubs.AsNoTracking().SingleAsync(club => club.Id == id);
        model.ClubId = id; model.Code = club.Code;
        if (!ModelState.IsValid) return View(model);
        var result = await service.EditAsync(id, model, users.GetUserId(User)!);
        if (result.Code == "Forbidden") return Forbid();
        if (result.Code == "NotFound") return NotFound();
        if (!result.Succeeded)
        {
            if (result.Code == "Conflict") Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(result.Field, result.Message); return View(model);
        }
        TempData["ClubMessage"] = "Đã lưu thông tin câu lạc bộ.";
        return RedirectToAction(nameof(Index));
    }
}
