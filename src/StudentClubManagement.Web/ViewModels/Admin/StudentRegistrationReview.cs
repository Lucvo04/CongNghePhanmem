using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace StudentClubManagement.Web.ViewModels.Admin;

public class StudentRegistrationReview
{
    [Required] public string ConcurrencyStamp { get; set; } = "";
    [Required, Display(Name = "Quyết định")] public bool? Approve { get; set; }
    [StringLength(500), Display(Name = "Lý do / ghi chú")] public string? Reason { get; set; }
    [BindNever, ValidateNever] public StudentRow Student { get; set; } = null!;
}
