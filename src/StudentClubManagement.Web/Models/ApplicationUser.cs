using Microsoft.AspNetCore.Identity;

namespace StudentClubManagement.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    // Tài khoản quản trị hệ thống có thể không có mã sinh viên.
    public string? StudentCode { get; set; }
    public string? ClassName { get; set; }
    public string? Faculty { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Membership> Memberships { get; set; } = [];
}

public enum UserStatus
{
    Active = 1,
    Disabled = 2
}
