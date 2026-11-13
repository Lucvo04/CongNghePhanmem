using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.ViewModels.Clubs;

namespace StudentClubManagement.Web.Controllers;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ClubsController(ApplicationDbContext db) : Controller
{
    private const int PageSize = 6;
    private const string TextCollation = "Latin1_General_100_CI_AS";

    // Cùng một chính sách cho danh sách, bộ lọc và truy cập trực tiếp trang chi tiết.
    private IQueryable<Club> PublishedClubs => db.Clubs.AsNoTracking()
        .Where(club => club.Status == ClubStatus.Active || club.Status == ClubStatus.Suspended);

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? category, int page = 1)
    {
        search = search?.Trim(); category = category?.Trim();
        var model = new PublicClubListViewModel
        {
            Search = search, Category = category,
            Categories = await PublishedClubs.Where(club => club.Category != null && club.Category.Trim() != "")
                .Select(club => EF.Functions.Collate(club.Category!.Trim(), TextCollation)).Distinct().OrderBy(value => value).ToListAsync()
        };
        if (!ModelState.IsValid || search?.Length > 150 || category?.Length > 100)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            model.Search = search is { Length: > 150 } ? search[..150] : search;
            model.Category = category is { Length: > 100 } ? category[..100] : category;
            model.FilterError = "Bộ lọc không hợp lệ. Từ khóa tối đa 150 ký tự, lĩnh vực tối đa 100 ký tự và số trang phải là số nguyên.";
            return View(model);
        }

        var query = PublishedClubs;
        if (!string.IsNullOrEmpty(search))
            query = query.Where(club => EF.Functions.Collate(club.Name, TextCollation).Contains(search) || club.Code.Contains(search));
        if (!string.IsNullOrEmpty(category))
            query = query.Where(club => club.Category != null && EF.Functions.Collate(club.Category.Trim(), TextCollation) == category);

        model.TotalCount = await query.CountAsync();
        model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalCount / (double)PageSize));
        model.Page = Math.Clamp(page, 1, model.TotalPages);
        model.Clubs = await query.OrderBy(club => club.Name).ThenBy(club => club.Id)
            .Skip((model.Page - 1) * PageSize).Take(PageSize)
            .Select(club => new PublicClubCard(club.Id, club.Code, club.Name, club.Category, club.Description, club.Status))
            .ToListAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (!ModelState.IsValid || id <= 0) return MissingClub();
        var club = await PublishedClubs.Where(club => club.Id == id)
            .Select(club => new PublicClubDetailsViewModel(club.Id, club.Code, club.Name, club.Category, club.Description, club.Status))
            .SingleOrDefaultAsync();
        return club is null ? MissingClub() : View(club);
    }

    private IActionResult MissingClub()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }
}
