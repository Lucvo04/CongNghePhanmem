using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.Web.Services;

public record CreateStudentRequest(string FullName, string StudentCode, string Email,
    string? ClassName, string? Faculty, string Password);

public class StudentAccountService(ApplicationDbContext db, UserManager<ApplicationUser> users, ClubOperationLocks operationLocks)
{
    // Có role Student và không có role Admin, kể cả dữ liệu mang đồng thời hai role.
    public IQueryable<ApplicationUser> Students => db.Users.Where(user =>
        db.UserRoles.Any(ur => ur.UserId == user.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == SystemRoles.Student)) &&
        !db.UserRoles.Any(ur => ur.UserId == user.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == SystemRoles.Admin)));

    public async Task<IdentityResult> CreateAsync(CreateStudentRequest request, string actorId)
    {
        var code = request.StudentCode.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(user => user.StudentCode == code))
            return Failure("DuplicateStudentCode", "Mã sinh viên đã được sử dụng.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            FullName = request.FullName.Trim(), StudentCode = code, Email = email, UserName = email,
            ClassName = Optional(request.ClassName), Faculty = Optional(request.Faculty), Status = UserStatus.Active
        };

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return result;

            result = await users.AddToRoleAsync(user, SystemRoles.Student);
            if (!result.Succeeded) return result;

            db.AuditLogs.Add(new AuditLog
            {
                ActorId = actorId, Action = "StudentCreated", EntityType = nameof(ApplicationUser), EntityId = user.Id,
                AfterData = JsonSerializer.Serialize(new { Status = user.Status.ToString(), Role = SystemRoles.Student })
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return IdentityResult.Success;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Unique index là hàng rào cuối khi hai yêu cầu vượt qua kiểm tra trùng cùng lúc.
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            return Failure("DuplicateAccount", "Email hoặc mã sinh viên đã được sử dụng. Vui lòng kiểm tra lại.");
        }
        // Mọi lỗi khác truyền lên middleware; Dispose rollback cả user, role và audit.
    }

    public async Task<IdentityResult> ChangeStatusAsync(string id, string expectedStamp, UserStatus newStatus,
        string reason, string actorId)
    {
        if (id == actorId) return Failure("NotStudent", "Không thể thay đổi tài khoản quản trị tại đây.");
        if (!Enum.IsDefined(newStatus) || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            return Failure("InvalidChange", "Trạng thái hoặc lý do không hợp lệ.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await operationLocks.AccountAsync(id))
            return Failure("ConcurrencyFailure", "Tài khoản đang được xử lý. Vui lòng tải lại trang và thử lại.");
        var user = await Students.SingleOrDefaultAsync(student => student.Id == id);
        if (user is null) return Failure("NotStudent", "Không tìm thấy tài khoản sinh viên.");
        if (!string.Equals(expectedStamp, user.ConcurrencyStamp, StringComparison.Ordinal) || user.Status == newStatus)
            return IdentityResult.Failed(users.ErrorDescriber.ConcurrencyFailure());

        if (newStatus == UserStatus.Disabled && await db.Memberships.AnyAsync(member =>
            member.UserId == id && member.Role == ClubRole.Manager && member.EndedAt == null))
            return Failure("CurrentClubManager", "Sinh viên đang là chủ nhiệm CLB. Vui lòng chuyển giao tất cả CLB phụ trách trước khi khóa tài khoản.");

        var previous = user.Status;
        user.Status = newStatus;
        // Cùng một UPDATE lưu Status, SecurityStamp mới và kiểm tra ConcurrencyStamp.
        // Luôn thay stamp: cookie cũ không sống lại sau chu kỳ khóa -> mở khóa.
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) return result;

        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, Action = newStatus == UserStatus.Disabled ? "StudentDisabled" : "StudentEnabled",
            EntityType = nameof(ApplicationUser), EntityId = user.Id,
            BeforeData = JsonSerializer.Serialize(new { Status = previous.ToString() }),
            AfterData = JsonSerializer.Serialize(new { Status = newStatus.ToString() }), Reason = reason.Trim()
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IdentityResult Failure(string code, string description) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = description });
}
