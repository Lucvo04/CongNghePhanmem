using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.JoinRequests;

public class ReviewJoinRequestViewModel
{
    [Required, StringLength(24)] public string RowVersion { get; set; } = string.Empty;
    [Range(3, 4, ErrorMessage = "Vui lòng chọn chấp nhận hoặc từ chối.")]
    public JoinRequestStatus Decision { get; set; }
    [StringLength(500, ErrorMessage = "Lý do từ chối tối đa 500 ký tự.")]
    [Display(Name = "Lý do từ chối")]
    public string? DecisionReason { get; set; }
    [BindNever, ValidateNever] public ReviewRequestRow Request { get; set; } = null!;
}

public record ReviewRequestRow(int Id, int ClubId, string ClubName, ClubStatus ClubStatus,
    string StudentName, string? StudentCode, string Reason, JoinRequestStatus Status,
    DateTimeOffset SubmittedAt, DateTimeOffset? ClosedAt, string? DecidedByName, string? DecisionReason);
public record ReviewRequestListViewModel(int ClubId, string ClubName, ClubStatus ClubStatus,
    JoinRequestStatus? Status, IReadOnlyList<ReviewRequestRow> Requests, int TotalCount, int Page, int TotalPages);
