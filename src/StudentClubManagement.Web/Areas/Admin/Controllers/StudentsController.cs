using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Admin;

namespace StudentClubManagement.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class StudentsController(StudentAccountService accounts, UserManager<ApplicationUser> users, StudentRegistrationService registrations) : Controller
{
    private const int PageSize = 10;

    [HttpGet]
    public async Task<IActionResult> Index(string? search, UserStatus? status, int page = 1)
    {
        search = search?.Trim();
        if (!ModelState.IsValid || search?.Length > 256 || status.HasValue && !Enum.IsDefined(status.Value))
            return BadRequest("Bộ lọc không hợp lệ.");

        var query = accounts.Students.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(user => user.FullName.Contains(search) || user.StudentCode!.Contains(search) || user.Email!.Contains(search));
        if (status.HasValue) query = query.Where(user => user.Status == status);

        var count = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        var students = await query.OrderBy(user => user.StudentCode).ThenBy(user => user.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(user => new StudentRow(user.Id, user.FullName, user.StudentCode!, user.Email!, user.ClassName, user.Faculty, user.Status))
            .ToListAsync();
        return View(new StudentListViewModel
        {
            Search = search, Status = status, Page = page, TotalPages = pages, TotalCount = count, Students = students
        });
    }

    [HttpGet]
    public IActionResult Create() => RedirectToAction(nameof(Index), new { status = UserStatus.Pending });

    // Endpoint cũ không được phép bỏ qua quy trình sinh viên tự đăng ký/chờ duyệt.
    [HttpPost, ActionName("Create")]
    public IActionResult CreateDisabled() => NotFound();

    [HttpGet]
    public async Task<IActionResult> Review(string id)
    {
        var user = await accounts.Students.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        return user is null ? NotFound() : View(new StudentRegistrationReview { Student = Row(user), ConcurrencyStamp = user.ConcurrencyStamp! });
    }

    [HttpPost]
    public async Task<IActionResult> Review(string id, StudentRegistrationReview model)
    {
        var user = await accounts.Students.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        model.Student = Row(user);
        if (!ModelState.IsValid) return View(model);
        var result = await registrations.ReviewAsync(id, users.GetUserId(User)!, model.ConcurrencyStamp, model.Approve!.Value, model.Reason);
        if (result.Code == "NotFound") return NotFound();
        if (!result.Succeeded)
        {
            if (result.Code == "Conflict") Response.StatusCode = 409;
            ModelState.AddModelError("", result.Message); return View(model);
        }
        TempData["StatusMessage"] = model.Approve.Value ? "Đã duyệt tài khoản. Sinh viên có thể đăng nhập bằng mật khẩu đã tự đặt." : "Đã từ chối đăng ký tài khoản.";
        return RedirectToAction(nameof(Index), new { status = UserStatus.Pending });
    }
    [HttpGet]
    public async Task<IActionResult> ChangeStatus(string id)
    {
        var user = await accounts.Students.AsNoTracking().SingleOrDefaultAsync(student => student.Id == id);
        if (user is null) return NotFound();
        if (user.Status is UserStatus.Pending or UserStatus.Rejected) return RedirectToAction(nameof(Review), new { id });
        return View(new ChangeStudentStatusViewModel
        {
            Student = Row(user), ConcurrencyStamp = user.ConcurrencyStamp!,
            NewStatus = user.Status == UserStatus.Active ? UserStatus.Disabled : UserStatus.Active
        });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(string id, ChangeStudentStatusViewModel model)
    {
        var user = await accounts.Students.AsNoTracking().SingleOrDefaultAsync(student => student.Id == id);
        if (user is null || id == users.GetUserId(User)) return NotFound();
        model.Student = Row(user);
        if (!ModelState.IsValid) return View(model);

        var result = await accounts.ChangeStatusAsync(id, model.ConcurrencyStamp, model.NewStatus!.Value,
            model.Reason, users.GetUserId(User)!);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == "NotStudent")) return NotFound();
            if (result.Errors.Any(error => error.Code == "ConcurrencyFailure")) Response.StatusCode = StatusCodes.Status409Conflict;
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        TempData["StatusMessage"] = model.NewStatus == UserStatus.Disabled
            ? "Đã khóa tài khoản. Các phiên đang đăng nhập bị từ chối từ yêu cầu tiếp theo."
            : "Đã mở khóa tài khoản. Sinh viên cần đăng nhập lại; thời hạn tạm khóa do nhập sai mật khẩu (nếu có) vẫn được giữ.";
        return RedirectToAction(nameof(Index), new { search = user.StudentCode });
    }

    private static StudentRow Row(ApplicationUser user) =>
        new(user.Id, user.FullName, user.StudentCode!, user.Email!, user.ClassName, user.Faculty, user.Status);
}
