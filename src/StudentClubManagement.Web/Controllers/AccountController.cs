using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;
using StudentClubManagement.Web.ViewModels.Account;

namespace StudentClubManagement.Web.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, PersonalProfileService profiles,
    StudentRegistrationService registrations) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction(nameof(Overview)) : View(new StudentRegistrationForm());

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Register(StudentRegistrationForm model)
    {
        if (User.Identity?.IsAuthenticated == true) return Forbid();
        model.Normalize();
        ModelState.Clear();
        if (TryValidateModel(model))
        {
            var result = await registrations.RegisterAsync(model);
            if (result.Succeeded)
            {
                TempData["AccountNotice"] = "Đã gửi đăng ký. Tài khoản cần được Admin duyệt trước khi đăng nhập. Mật khẩu là mật khẩu bạn vừa đặt.";
                return RedirectToAction(nameof(Login));
            }
            ModelState.AddModelError("", result.Message);
        }
        ModelState.SetModelValue(nameof(model.Password), "", ""); ModelState.SetModelValue(nameof(model.ConfirmPassword), "", "");
        model.Password = model.ConfirmPassword = "";
        return View(model);
    }
    public const string LoginFailureMessage = "Không thể đăng nhập. Vui lòng kiểm tra thông tin hoặc thử lại sau. Nếu vẫn gặp lỗi, hãy liên hệ quản trị viên.";

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectAfterLogin(returnUrl, User.IsInRole(SystemRoles.Admin));
        }
        return View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        if (ModelState.IsValid)
        {
            var user = await users.FindByEmailAsync(model.Email.Trim());
            if (user is not null)
            {
                var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    return RedirectAfterLogin(model.ReturnUrl, await users.IsInRoleAsync(user, SystemRoles.Admin));
                }
            }
            ModelState.AddModelError(string.Empty, LoginFailureMessage);
        }

        // Không gửi lại mật khẩu trong HTML khi validation hoặc đăng nhập thất bại.
        ModelState.SetModelValue(nameof(model.Password), string.Empty, string.Empty);
        model.Password = string.Empty;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home", new { area = "" });
    }

    [HttpGet]
    public async Task<IActionResult> Overview()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }
        return View(new AccountOverviewViewModel(user.FullName, user.Email!, user.StudentCode, User.IsInRole(SystemRoles.Admin),
            user.PhoneNumber, user.ClassName, user.Faculty));
    }

    [HttpGet]
    public async Task<IActionResult> EditProfile()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(new EditProfileViewModel
        {
            FullName = user.FullName, PhoneNumber = user.PhoneNumber, ClassName = user.ClassName,
            Faculty = user.Faculty, ConcurrencyStamp = user.ConcurrencyStamp!
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditProfile(EditProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        // Actor comes only from the authenticated principal, never from submitted id/email/role fields.
        var userId = users.GetUserId(User);
        if (userId is null) return Challenge();
        var result = await profiles.UpdateAsync(userId, new UpdatePersonalProfile(model.FullName, model.PhoneNumber,
            model.ClassName, model.Faculty, model.ConcurrencyStamp));
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == "UnavailableAccount"))
            {
                await signIn.SignOutAsync();
                return RedirectToAction(nameof(Login));
            }
            if (result.Errors.Any(error => error.Code == "ConcurrencyFailure")) Response.StatusCode = StatusCodes.Status409Conflict;
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        TempData["StatusMessage"] = "Đã lưu hồ sơ cá nhân.";
        return RedirectToAction(nameof(Overview));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PasswordChangeError(model);
        }

        // Chỉ lấy tài khoản từ phiên đã xác thực; không nhận UserId/Email từ form.
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        // ChangePasswordAsync không tự đếm lần sai. Dùng cùng cơ chế lockout với đăng nhập.
        var verification = await signIn.CheckPasswordSignInAsync(user, model.CurrentPassword, lockoutOnFailure: true);
        if (!verification.Succeeded)
        {
            if (verification.IsLockedOut || verification.IsNotAllowed)
            {
                await signIn.SignOutAsync();
                TempData["AccountNotice"] = verification.IsLockedOut
                    ? "Tài khoản tạm khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử đăng nhập lại sau 15 phút."
                    : "Tài khoản hiện không thể sử dụng. Vui lòng liên hệ quản trị viên.";
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
            return PasswordChangeError(model);
        }

        if (string.Equals(model.CurrentPassword, model.NewPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới phải khác mật khẩu hiện tại.");
            return PasswordChangeError(model);
        }

        var result = await users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var field = error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)
                    ? nameof(model.CurrentPassword)
                    : error.Code.StartsWith("Password", StringComparison.Ordinal) ? nameof(model.NewPassword) : string.Empty;
                ModelState.AddModelError(field, error.Description);
            }
            return PasswordChangeError(model);
        }

        // Identity đổi SecurityStamp; phát cookie mới cho phiên này, giữ tùy chọn RememberMe.
        await signIn.RefreshSignInAsync(user);
        TempData["StatusMessage"] = "Đã đổi mật khẩu thành công. Các thiết bị khác cần đăng nhập lại bằng mật khẩu mới.";
        return RedirectToAction(nameof(Overview));
    }

    private IActionResult PasswordChangeError(ChangePasswordViewModel model)
    {
        foreach (var field in new[] { nameof(model.CurrentPassword), nameof(model.NewPassword), nameof(model.ConfirmPassword) })
        {
            ModelState.SetModelValue(field, string.Empty, string.Empty);
        }
        model.CurrentPassword = model.NewPassword = model.ConfirmPassword = string.Empty;
        return View(nameof(ChangePassword), model);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult RedirectAfterLogin(string? returnUrl, bool admin) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : admin
            ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
            : RedirectToAction(nameof(Overview), "Account", new { area = "" });
}
