using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Admin;

public class CreateStudentViewModel
{
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

    [Display(Name = "Mật khẩu ban đầu"), Required(ErrorMessage = "Vui lòng nhập mật khẩu ban đầu.")]
    [DataType(DataType.Password), MinLength(10, ErrorMessage = "Mật khẩu phải có ít nhất 10 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Nhập lại mật khẩu"), Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận chưa khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class StudentListViewModel
{
    public string? Search { get; set; }
    public UserStatus? Status { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<StudentRow> Students { get; set; } = [];
}

public record StudentRow(string Id, string FullName, string StudentCode, string Email,
    string? ClassName, string? Faculty, UserStatus Status);

public class ChangeStudentStatusViewModel
{
    // Thông tin hiển thị luôn được nạp lại từ database, không tin dữ liệu POST.
    [BindNever, ValidateNever]
    public StudentRow? Student { get; set; }

    [Required(ErrorMessage = "Thiếu phiên bản tài khoản. Vui lòng tải lại trang.")]
    public string ConcurrencyStamp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thiếu trạng thái mới. Vui lòng tải lại trang.")]
    [EnumDataType(typeof(UserStatus), ErrorMessage = "Trạng thái không hợp lệ.")]
    public UserStatus? NewStatus { get; set; }

    [Display(Name = "Lý do"), Required(ErrorMessage = "Vui lòng nhập lý do khóa hoặc mở khóa.")]
    [StringLength(500, ErrorMessage = "Lý do tối đa 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}
