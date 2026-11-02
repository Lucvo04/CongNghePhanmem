using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Services;

public record UpdatePersonalProfile(string FullName, string? PhoneNumber, string? ClassName, string? Faculty, string ConcurrencyStamp);

public class PersonalProfileService(ApplicationDbContext db, UserManager<ApplicationUser> users, ClubOperationLocks locks)
{
    public async Task<IdentityResult> UpdateAsync(string currentUserId, UpdatePersonalProfile request)
    {
        var name = request.FullName?.Trim();
        var phone = Optional(request.PhoneNumber);
        var className = Optional(request.ClassName);
        var faculty = Optional(request.Faculty);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || className?.Length > 50 || faculty?.Length > 100 ||
            (phone is not null && !Regex.IsMatch(phone, @"\A\+?[0-9]{7,15}\z")) ||
            string.IsNullOrWhiteSpace(request.ConcurrencyStamp) || request.ConcurrencyStamp.Length > 100)
            return Failure("InvalidProfile", "Thông tin hồ sơ không hợp lệ. Vui lòng kiểm tra lại.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await locks.AccountAsync(currentUserId))
            return Failure("ConcurrencyFailure", "Tài khoản đang được xử lý. Vui lòng tải lại trang và thử lại.");
        var user = await users.FindByIdAsync(currentUserId);
        if (user is null) return Failure("UnavailableAccount", "Tài khoản hiện không thể sử dụng.");
        // Cookie validation may already have tracked this user before the transaction acquired its lock.
        await db.Entry(user).ReloadAsync();
        if (db.Entry(user).State == EntityState.Detached || user.Status != UserStatus.Active)
            return Failure("UnavailableAccount", "Tài khoản hiện không thể sử dụng.");
        if (!string.Equals(user.ConcurrencyStamp, request.ConcurrencyStamp, StringComparison.Ordinal))
            return Failure("ConcurrencyFailure", "Hồ sơ đã thay đổi từ lần mở form này. Chọn Tải lại hồ sơ để xem dữ liệu mới trước khi sửa.");

        var changed = new List<string>();
        if (user.FullName != name) changed.Add(nameof(user.FullName));
        if (user.PhoneNumber != phone) changed.Add(nameof(user.PhoneNumber));
        if (user.ClassName != className) changed.Add(nameof(user.ClassName));
        if (user.Faculty != faculty) changed.Add(nameof(user.Faculty));
        if (changed.Count == 0) return IdentityResult.Success;

        user.FullName = name!;
        if (user.PhoneNumber != phone) user.PhoneNumberConfirmed = false;
        user.PhoneNumber = phone;
        user.ClassName = className;
        user.Faculty = faculty;
        // UpdateAsync rotates ConcurrencyStamp; credentials, roles and SecurityStamp stay unchanged.
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return result;
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = user.Id, EntityType = nameof(ApplicationUser), EntityId = user.Id,
            Action = "PersonalProfileUpdated", AfterData = JsonSerializer.Serialize(new { Fields = changed })
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IdentityResult Failure(string code, string description) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = description });
}
