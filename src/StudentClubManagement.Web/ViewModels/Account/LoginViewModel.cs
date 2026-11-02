using System.ComponentModel.DataAnnotations;

namespace StudentClubManagement.Web.ViewModels.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được dài quá 256 ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Duy trì đăng nhập trên thiết bị này")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
