using System.ComponentModel.DataAnnotations;

namespace StudentClubManagement.Web.ViewModels.Account;

public class EditProfileViewModel
{
    [Display(Name = "Họ và tên"), Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Số điện thoại"), StringLength(16, ErrorMessage = "Số điện thoại tối đa 15 chữ số và dấu + ở đầu.")]
    [RegularExpression(@"\+?[0-9]{7,15}", ErrorMessage = "Nhập từ 7 đến 15 chữ số, có thể thêm dấu + ở đầu; không dùng khoảng trắng hoặc dấu gạch.")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Lớp"), StringLength(50, ErrorMessage = "Tên lớp tối đa 50 ký tự.")]
    public string? ClassName { get; set; }

    [Display(Name = "Khoa"), StringLength(100, ErrorMessage = "Tên khoa tối đa 100 ký tự.")]
    public string? Faculty { get; set; }

    [Required(ErrorMessage = "Thiếu phiên bản hồ sơ. Vui lòng tải lại trang.")]
    [StringLength(100, ErrorMessage = "Phiên bản hồ sơ không hợp lệ. Vui lòng tải lại trang.")]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
