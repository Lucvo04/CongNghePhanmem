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
public class StudentsController(StudentAccountService accounts, UserManager<ApplicationUser> users) : Controller
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
    public IActionResult Create() => View(new CreateStudentViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateStudentViewModel model)
    {
        // Form chỉ có chuỗi: chuẩn hóa rồi kiểm tra lại toàn bộ, không trim mật khẩu.
        model.FullName = model.FullName?.Trim() ?? string.Empty;
        model.StudentCode = model.StudentCode?.Trim().ToUpperInvariant() ?? string.Empty;
        model.Email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        model.ClassName = model.ClassName?.Trim();
        model.Faculty = model.Faculty?.Trim();
        ModelState.Clear();
        if (!TryValidateModel(model)) return CreateError(model);

        var result = await accounts.CreateAsync(new CreateStudentRequest(model.FullName, model.StudentCode,
            model.Email, model.ClassName, model.Faculty, model.Password), users.GetUserId(User)!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var field = error.Code switch
                {
                    "DuplicateStudentCode" => nameof(model.StudentCode),
                    "DuplicateEmail" or "DuplicateUserName" or "InvalidEmail" or "InvalidUserName" => nameof(model.Email),
                    _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => nameof(model.Password),
                    _ => string.Empty
                };
                ModelState.AddModelError(field, error.Description);
            }
            return CreateError(model);
        }
        TempData["StatusMessage"] = "Đã cấp tài khoản sinh viên. Hãy bàn giao thông tin đăng nhập và hướng dẫn sinh viên đổi mật khẩu.";
        return RedirectToAction(nameof(Index), new { search = model.StudentCode });
    }

    [HttpGet]
    public async Task<IActionResult> ChangeStatus(string id)
    {
        var user = await accounts.Students.AsNoTracking().SingleOrDefaultAsync(student => student.Id == id);
        if (user is null) return NotFound();
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

    private IActionResult CreateError(CreateStudentViewModel model)
    {
        ModelState.SetModelValue(nameof(model.Password), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(model.ConfirmPassword), string.Empty, string.Empty);
        model.Password = model.ConfirmPassword = string.Empty;
        return View(nameof(Create), model);
    }

    private static StudentRow Row(ApplicationUser user) =>
        new(user.Id, user.FullName, user.StudentCode!, user.Email!, user.ClassName, user.Faculty, user.Status);
}
