using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.JoinRequests;

public class CreateJoinRequestViewModel
{
    [Range(1, int.MaxValue)]
    public int ClubId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập lý do muốn tham gia CLB.")]
    [StringLength(1000, ErrorMessage = "Lý do tối đa 1.000 ký tự.")]
    [Display(Name = "Lý do bạn muốn tham gia")]
    public string Reason { get; set; } = string.Empty;
    [BindNever] public string ClubName { get; set; } = string.Empty;
    [BindNever] public string? UnavailableReason { get; set; }
}

public class WithdrawJoinRequestViewModel
{
    [Required, StringLength(24)]
    public string RowVersion { get; set; } = string.Empty;
    [BindNever] public int Id { get; set; }
    [BindNever] public string ClubName { get; set; } = string.Empty;
    [BindNever] public string Reason { get; set; } = string.Empty;
    [BindNever] public JoinRequestStatus Status { get; set; }
}

public record JoinRequestRow(int Id, int ClubId, string ClubName, ClubStatus ClubStatus,
    string Reason, JoinRequestStatus Status, DateTimeOffset SubmittedAt, DateTimeOffset? ClosedAt,
    string? DecisionReason, string? DecidedByName);
public record JoinRequestListViewModel(IReadOnlyList<JoinRequestRow> Requests, int TotalCount, int Page, int TotalPages);
