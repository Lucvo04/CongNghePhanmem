using Microsoft.AspNetCore.Identity;

namespace StudentClubManagement.Web.Models;

public class ApplicationUser : IdentityUser
{
    // Đăng nhập bằng email; Identity vẫn đọc hai tên thuộc tính này nhưng SQL chỉ lưu Email.
    public override string? UserName { get => Email; set => Email = value; }
    public override string? NormalizedUserName { get => NormalizedEmail; set => NormalizedEmail = value; }
    public override bool LockoutEnabled { get => true; set { } }
    public string? RoleId { get; set; }
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
    Disabled = 2,
    Pending = 3,
    Rejected = 4
}

public static class UserStatusLabels
{
    public static string Label(this UserStatus status) => status switch
    { UserStatus.Active => "Đang hoạt động", UserStatus.Disabled => "Đã khóa", UserStatus.Pending => "Chờ duyệt", UserStatus.Rejected => "Đã từ chối", _ => "Không hợp lệ" };
}
