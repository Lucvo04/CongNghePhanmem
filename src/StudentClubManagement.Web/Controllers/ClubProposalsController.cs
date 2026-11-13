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

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ClubProposalsController(ApplicationDbContext db, UserManager<ApplicationUser> users, ClubAccessService access, ClubProposalService service) : Controller
{
    private string Actor => users.GetUserId(User)!;
    [HttpGet]
    public async Task<IActionResult> Index(ProposalStatus? status = ProposalStatus.Pending, int page = 1)
    {
        if (!ModelState.IsValid || status.HasValue && !Enum.IsDefined(status.Value)) return BadRequest();
        var admin = await access.IsAdminAsync(Actor);
        if (!admin && !await service.CanSubmitAsync(Actor)) return Forbid();
        var query = service.VisibleTo(Actor).AsNoTracking();
        if (status.HasValue) query = query.Where(p => p.Status == status);
        var count = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(count / 10d)); page = Math.Clamp(page, 1, pages);
        var items = await query.Include(p => p.Applicant).OrderByDescending(p => p.SubmittedAt).ThenByDescending(p => p.Id).Skip((page - 1) * 10).Take(10).ToListAsync();
        return View(new ClubProposalList(items, admin, status, page, pages, count));
    }
    [HttpGet]
    public async Task<IActionResult> Create() => await service.CanSubmitAsync(Actor) ? View(new ClubProposalForm()) : Forbid();
    [HttpPost]
    public async Task<IActionResult> Create(ClubProposalForm form)
    {
        if (!await service.CanSubmitAsync(Actor)) return Forbid();
        if (!ModelState.IsValid) return View(form);
        var result = await service.SubmitAsync(Actor, form);
        if (result.Code == "Forbidden") return Forbid();
        if (result.Succeeded) return RedirectToAction(nameof(Details), new { id = result.Id });
        ModelState.AddModelError("", result.Message); return View(form);
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var model = await LoadAsync(id); return model is null ? NotFound() : View(model);
    }
    [HttpPost]
    public async Task<IActionResult> Decide(int id, ClubProposalDecision form)
    {
        var model = await LoadAsync(id); if (model is null) return NotFound();
        form.Item = model.Item; form.IsAdmin = model.IsAdmin; form.History = model.History;
        if (!ModelState.IsValid) return View("Details", form);
        var result = await service.DecideAsync(id, Actor, form);
        if (result.Code == "NotFound") return NotFound();
        if (result.Code == "Forbidden") return Forbid();
        if (!result.Succeeded)
        { if (result.Code == "Conflict") Response.StatusCode = 409; ModelState.AddModelError("", result.Message); return View("Details", form); }
        TempData["ProposalMessage"] = "Đã xử lý đơn thành lập CLB.";
        return RedirectToAction(nameof(Details), new { id });
    }
    private async Task<ClubProposalDecision?> LoadAsync(int id)
    {
        if (!await access.IsAdminAsync(Actor) && !await service.CanSubmitAsync(Actor)) return null;
        var item = await service.VisibleTo(Actor).AsNoTracking().Include(p => p.Applicant).Include(p => p.Club).SingleOrDefaultAsync(p => p.Id == id);
        if (item is null) return null;
        var history = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == nameof(ClubProposal) && a.EntityId == id.ToString())
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(20).Select(a => new ProposalHistory(a.Action, a.Actor.FullName, a.Reason, a.OccurredAt)).ToListAsync();
        return new() { Item = item, IsAdmin = await access.IsAdminAsync(Actor), RowVersion = Convert.ToBase64String(item.RowVersion), Code = $"CLB-{id:D4}", History = history };
    }
}
