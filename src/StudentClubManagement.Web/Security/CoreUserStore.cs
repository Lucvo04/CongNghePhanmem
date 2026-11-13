using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Security;

// Chỉ công bố những khả năng có trong schema rút gọn. Không kế thừa UserStore vì
// lớp đó công bố cả claims, external login, token và 2FA mà đồ án không lưu trữ.
// Các thao tác hỗ trợ vẫn ủy quyền cho EF store của Microsoft: giữ nguyên cơ chế
// chuẩn hóa, concurrency và SaveChanges; không tự viết lại băm mật khẩu/cookie.
public sealed class CoreUserStore : IQueryableUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>, IUserEmailStore<ApplicationUser>,
    IUserSecurityStampStore<ApplicationUser>, IUserRoleStore<ApplicationUser>,
    IUserLockoutStore<ApplicationUser>, IUserPhoneNumberStore<ApplicationUser>
{
    private readonly UserStore<ApplicationUser, IdentityRole, ApplicationDbContext, string> store;
    private readonly ApplicationDbContext db;

    public CoreUserStore(ApplicationDbContext db, IdentityErrorDescriber errors)
        { this.db = db; store = new(db, errors); }

    public IQueryable<ApplicationUser> Users => store.Users;
    public void Dispose() => store.Dispose();
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => store.GetUserIdAsync(user, ct);
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => store.GetUserNameAsync(user, ct);
    public Task SetUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) => store.SetUserNameAsync(user, name, ct);
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => store.GetNormalizedUserNameAsync(user, ct);
    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) => store.SetNormalizedUserNameAsync(user, name, ct);
    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) => store.CreateAsync(user, ct);
    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => store.UpdateAsync(user, ct);
    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => store.DeleteAsync(user, ct);
    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => store.FindByIdAsync(id, ct);
    public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == name, ct);

    public Task SetPasswordHashAsync(ApplicationUser user, string? hash, CancellationToken ct) => store.SetPasswordHashAsync(user, hash, ct);
    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken ct) => store.GetPasswordHashAsync(user, ct);
    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken ct) => store.HasPasswordAsync(user, ct);

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken ct) => store.SetEmailAsync(user, email, ct);
    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken ct) => store.GetEmailAsync(user, ct);
    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken ct) => store.GetEmailConfirmedAsync(user, ct);
    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken ct) => store.SetEmailConfirmedAsync(user, confirmed, ct);
    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct) => store.FindByEmailAsync(email, ct);
    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken ct) => store.GetNormalizedEmailAsync(user, ct);
    public Task SetNormalizedEmailAsync(ApplicationUser user, string? email, CancellationToken ct) => store.SetNormalizedEmailAsync(user, email, ct);

    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken ct) => store.SetSecurityStampAsync(user, stamp, ct);
    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken ct) => store.GetSecurityStampAsync(user, ct);

    public async Task AddToRoleAsync(ApplicationUser user, string role, CancellationToken ct)
    {
        var id = await db.Roles.Where(r => r.Name!.ToUpper() == role).Select(r => r.Id).SingleAsync(ct);
        if (user.RoleId is not null && user.RoleId != id)
            throw new InvalidOperationException("Mỗi tài khoản chỉ có một quyền hệ thống. Phải gỡ quyền cũ trước khi gán quyền mới.");
        user.RoleId = id;
    }
    public async Task RemoveFromRoleAsync(ApplicationUser user, string role, CancellationToken ct)
    {
        if (await IsInRoleAsync(user, role, ct)) user.RoleId = null;
    }
    public async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken ct) =>
        await db.Roles.Where(r => r.Id == user.RoleId).Select(r => r.Name!).ToListAsync(ct);
    public Task<bool> IsInRoleAsync(ApplicationUser user, string role, CancellationToken ct) =>
        db.Roles.AnyAsync(r => r.Id == user.RoleId && r.Name!.ToUpper() == role, ct);
    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string role, CancellationToken ct) =>
        await db.Users.Where(u => db.Roles.Any(r => r.Id == u.RoleId && r.Name!.ToUpper() == role)).ToListAsync(ct);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken ct) => store.GetLockoutEndDateAsync(user, ct);
    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? end, CancellationToken ct) => store.SetLockoutEndDateAsync(user, end, ct);
    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken ct) => store.IncrementAccessFailedCountAsync(user, ct);
    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken ct) => store.ResetAccessFailedCountAsync(user, ct);
    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken ct) => store.GetAccessFailedCountAsync(user, ct);
    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken ct) => store.GetLockoutEnabledAsync(user, ct);
    public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken ct) => store.SetLockoutEnabledAsync(user, enabled, ct);

    public Task SetPhoneNumberAsync(ApplicationUser user, string? number, CancellationToken ct) => store.SetPhoneNumberAsync(user, number, ct);
    public Task<string?> GetPhoneNumberAsync(ApplicationUser user, CancellationToken ct) => store.GetPhoneNumberAsync(user, ct);
    public Task<bool> GetPhoneNumberConfirmedAsync(ApplicationUser user, CancellationToken ct) => store.GetPhoneNumberConfirmedAsync(user, ct);
    public Task SetPhoneNumberConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken ct) => store.SetPhoneNumberConfirmedAsync(user, confirmed, ct);
}
