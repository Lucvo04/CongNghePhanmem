using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Clubs;

public class ClubProfileForm
{
    [Display(Name = "Tên câu lạc bộ"), Required(ErrorMessage = "Vui lòng nhập tên câu lạc bộ.")]
    [StringLength(150, ErrorMessage = "Tên CLB tối đa 150 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Lĩnh vực"), StringLength(100, ErrorMessage = "Lĩnh vực tối đa 100 ký tự.")]
    public string? Category { get; set; }

    [Display(Name = "Giới thiệu"), StringLength(2000, ErrorMessage = "Giới thiệu tối đa 2.000 ký tự.")]
    public string? Description { get; set; }
}

public class CreateClubViewModel : ClubProfileForm
{
    [Display(Name = "Mã câu lạc bộ"), Required(ErrorMessage = "Vui lòng nhập mã câu lạc bộ.")]
    [StringLength(30, ErrorMessage = "Mã CLB tối đa 30 ký tự.")]
    [RegularExpression(@"[A-Za-z0-9][A-Za-z0-9_-]*", ErrorMessage = "Mã CLB dùng chữ không dấu, số, dấu gạch ngang hoặc gạch dưới.")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Mã sinh viên chủ nhiệm"), Required(ErrorMessage = "Vui lòng nhập mã sinh viên chủ nhiệm.")]
    [StringLength(24, ErrorMessage = "Mã sinh viên tối đa 24 ký tự.")]
    public string ManagerStudentCode { get; set; } = string.Empty;
}

public class EditClubViewModel : ClubProfileForm
{
    [BindNever, ValidateNever]
    public string Code { get; set; } = string.Empty;
    [BindNever, ValidateNever]
    public int ClubId { get; set; }
    [Required(ErrorMessage = "Thiếu phiên bản CLB. Vui lòng tải lại trang.")]
    public string RowVersion { get; set; } = string.Empty;
}

public class TransferManagerViewModel
{
    [BindNever, ValidateNever]
    public ClubManagementRow? Club { get; set; }
    [BindNever, ValidateNever]
    public IReadOnlyList<ManagerCandidate> Candidates { get; set; } = [];
    [Display(Name = "Chủ nhiệm mới"), Required(ErrorMessage = "Vui lòng chọn chủ nhiệm mới.")]
    public string NewManagerUserId { get; set; } = string.Empty;
    [Required(ErrorMessage = "Thiếu phiên bản CLB. Vui lòng tải lại trang.")]
    public string RowVersion { get; set; } = string.Empty;
    [Display(Name = "Lý do chuyển giao"), Required(ErrorMessage = "Vui lòng nhập lý do chuyển giao.")]
    [StringLength(500, ErrorMessage = "Lý do tối đa 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public record ManagerCandidate(string UserId, string StudentCode, string FullName);
public record ClubManagementRow(int Id, string Code, string Name, string? Category, ClubStatus Status,
    string? ManagerName, string? ManagerCode);
public class ClubListViewModel
{
    public bool IsAdmin { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<ClubManagementRow> Clubs { get; set; } = [];
}
