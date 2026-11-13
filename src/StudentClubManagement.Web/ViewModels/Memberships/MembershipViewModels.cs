using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Memberships;

public record MemberRow(int Id, int ClubId, string ClubName, ClubStatus ClubStatus, string UserId,
    string FullName, string? StudentCode, string? ClassName, string? Faculty, UserStatus AccountStatus,
    ClubRole Role, MembershipStatus Status, DateTimeOffset JoinedAt, DateTimeOffset? EndedAt, byte[] RowVersion);
public record MemberListViewModel(int? ClubId, string? ClubName, ClubStatus? ClubStatus,
    string? Search, string Status, IReadOnlyList<MemberRow> Members, int TotalCount, int Page, int TotalPages);
public record MemberHistoryRow(long Id, string Action, string ActorName, string? Reason, DateTimeOffset OccurredAt);
public record MemberDetailsViewModel(MemberRow Member, bool CanManage, bool IsOwner,
    IReadOnlyList<MemberHistoryRow> History, int TotalCount, int Page, int TotalPages);

public class ChangeMemberStatusViewModel
{
    [Required, StringLength(24)] public string RowVersion { get; set; } = string.Empty;
    [Range(1, 3, ErrorMessage = "Vui lòng chọn trạng thái.")]
    public MembershipStatus NewStatus { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập lý do."), StringLength(500, ErrorMessage = "Lý do tối đa 500 ký tự.")]
    [Display(Name = "Lý do thay đổi")]
    public string Reason { get; set; } = string.Empty;
    [BindNever, ValidateNever] public MemberRow Member { get; set; } = null!;
    [BindNever] public bool SelfLeave { get; set; }
}
