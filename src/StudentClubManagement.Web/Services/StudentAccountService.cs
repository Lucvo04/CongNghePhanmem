using System.Text.Json;
using Microsoft.AspNetCore.Identity;

using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.Web.Services;

public class StudentAccountService(ApplicationDbContext db, UserManager<ApplicationUser> users, ClubOperationLocks operationLocks)
{
    public IQueryable<ApplicationUser> Students => db.Users.Where(user => user.RoleId == "system-student");

    public async Task<IdentityResult> ChangeStatusAsync(string id, string expectedStamp, UserStatus newStatus,
        string reason, string actorId)
    {
        if (id == actorId) return Failure("NotStudent", "Không thể thay đổi tài khoản quản trị tại đây.");
        if (newStatus is not (UserStatus.Active or UserStatus.Disabled) || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            return Failure("InvalidChange", "Trạng thái hoặc lý do không hợp lệ.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await operationLocks.AccountAsync(id))
            return Failure("ConcurrencyFailure", "Tài khoản đang được xử lý. Vui lòng tải lại trang và thử lại.");
        var user = await Students.SingleOrDefaultAsync(student => student.Id == id);
        if (user is null) return Failure("NotStudent", "Không tìm thấy tài khoản sinh viên.");
        if (user.Status is UserStatus.Pending or UserStatus.Rejected)
            return Failure("ReviewRequired", "Tài khoản chưa được phê duyệt. Hãy xử lý tại màn hình duyệt đăng ký.");
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

    private static IdentityResult Failure(string code, string description) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = description });
}
