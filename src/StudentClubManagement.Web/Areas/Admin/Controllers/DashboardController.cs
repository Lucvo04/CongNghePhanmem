using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Admin;

namespace StudentClubManagement.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DashboardController(UserManager<ApplicationUser> users, ApplicationDbContext db, StudentAccountService students) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(new DashboardViewModel(user.FullName,
            await students.Students.CountAsync(), await db.Clubs.CountAsync(),
            await db.Clubs.CountAsync(c => c.Status == ClubStatus.Active),
            await db.Memberships.CountAsync(m => m.Status == MembershipStatus.Active && m.EndedAt == null),
            await db.Clubs.AsNoTracking().OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Take(5)
                .Select(c => new DashboardClub(c.Id, c.Code, c.Name, c.Status)).ToListAsync(),
            await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(6)
                .Select(a => new DashboardActivity(a.Action, a.OccurredAt)).ToListAsync()));
    }
}
