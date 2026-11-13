using System.ComponentModel.DataAnnotations;

namespace StudentClubManagement.Web.ViewModels.Account;

public class StudentRegistrationForm
{
    public void Normalize()
    {
        FullName = FullName?.Trim() ?? "";
        Email = Email?.Trim().ToLowerInvariant() ?? "";
        StudentCode = StudentCode?.Trim().ToUpperInvariant() ?? "";
        ClassName = string.IsNullOrWhiteSpace(ClassName) ? null : ClassName.Trim();
        Faculty = string.IsNullOrWhiteSpace(Faculty) ? null : Faculty.Trim();
    }

    [Display(Name = "Họ và tên"), Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Mã sinh viên"), Required(ErrorMessage = "Vui lòng nhập mã sinh viên.")]
    [StringLength(24, ErrorMessage = "Mã sinh viên tối đa 24 ký tự.")]
    [RegularExpression(@"\S+", ErrorMessage = "Mã sinh viên không chứa khoảng trắng.")]
    public string StudentCode { get; set; } = string.Empty;

    [Display(Name = "Email"), Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng."), StringLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Lớp"), StringLength(50, ErrorMessage = "Tên lớp tối đa 50 ký tự.")]
    public string? ClassName { get; set; }

    [Display(Name = "Khoa"), StringLength(100, ErrorMessage = "Tên khoa tối đa 100 ký tự.")]
    public string? Faculty { get; set; }

    [Display(Name = "Mật khẩu"), Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password), MinLength(10, ErrorMessage = "Mật khẩu phải có ít nhất 10 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Nhập lại mật khẩu"), Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận chưa khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}


