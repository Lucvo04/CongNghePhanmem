using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Admin;

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
