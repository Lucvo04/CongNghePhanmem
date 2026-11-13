using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;

namespace StudentClubManagement.Web.Security;

// Danh mục quyền cố định, chỉ lưu Id/Name trong SQL; không chỉnh sửa qua website.
public sealed class CoreRoleStore(ApplicationDbContext db) : IQueryableRoleStore<IdentityRole>
{
    public IQueryable<IdentityRole> Roles => db.Roles;
    public void Dispose() { }
    public Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken ct) => ReadOnly();
    public Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken ct) => ReadOnly();
    public Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken ct) => ReadOnly();
    public Task<string> GetRoleIdAsync(IdentityRole role, CancellationToken ct) => Task.FromResult(role.Id);
    public Task<string?> GetRoleNameAsync(IdentityRole role, CancellationToken ct) => Task.FromResult(role.Name);
    public Task SetRoleNameAsync(IdentityRole role, string? name, CancellationToken ct) { role.Name = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken ct) => Task.FromResult(role.Name?.ToUpperInvariant());
    public Task SetNormalizedRoleNameAsync(IdentityRole role, string? name, CancellationToken ct) => Task.CompletedTask;
    public Task<IdentityRole?> FindByIdAsync(string id, CancellationToken ct) => db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct);
    public Task<IdentityRole?> FindByNameAsync(string name, CancellationToken ct) => db.Roles.SingleOrDefaultAsync(r => r.Name!.ToUpper() == name, ct);
    private static Task<IdentityResult> ReadOnly() => Task.FromResult(IdentityResult.Failed(new IdentityError
        { Code = "FixedRoles", Description = "Danh mục Admin/Student được quản lý bằng migration, không chỉnh sửa qua website." }));
}
