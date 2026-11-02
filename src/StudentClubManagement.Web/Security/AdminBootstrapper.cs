using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Security;

public class AdminBootstrapRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AdminBootstrapper(ApplicationDbContext db, UserManager<ApplicationUser> users)
{
    public async Task<IdentityResult> CreateAsync(AdminBootstrapRequest request)
    {
        request.Email = request.Email.Trim();
        request.FullName = request.FullName.Trim();
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validation, true))
        {
            return Failure("InvalidConfiguration", "Cần cấu hình BootstrapAdmin:Email, FullName và Password hợp lệ trong User Secrets hoặc biến môi trường.");
        }

        // Cùng transaction để không tạo tài khoản dở dang hoặc hai quản trị viên đầu tiên.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var adminRole = await db.Roles.SingleOrDefaultAsync(r => r.NormalizedName == "ADMIN");
        if (adminRole is null)
        {
            return Failure("MissingRole", "Chưa có role Admin. Hãy áp dụng migration trước.");
        }

        if (await db.UserRoles.AnyAsync(r => r.RoleId == adminRole.Id))
        {
            return Failure("AdminAlreadyExists", "Đã có quản trị viên. Lệnh khởi tạo không đổi mật khẩu, không cấp thêm quyền.");
        }

        if (await users.FindByEmailAsync(request.Email) is not null)
        {
            return Failure("EmailAlreadyExists", "Email đã được sử dụng. Không tự nâng quyền tài khoản hiện có.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email, Email = request.Email, FullName = request.FullName,
            Status = UserStatus.Active, LockoutEnabled = true
        };
        var creation = await users.CreateAsync(user, request.Password);
        if (!creation.Succeeded)
        {
            return creation;
        }

        var assignment = await users.AddToRoleAsync(user, SystemRoles.Admin);
        if (!assignment.Succeeded)
        {
            return assignment;
        }

        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    private static IdentityResult Failure(string code, string description) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = description });
}
