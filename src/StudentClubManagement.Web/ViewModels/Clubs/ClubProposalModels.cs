using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Clubs;

public class ClubProposalForm
{
    [Required, StringLength(150), Display(Name = "Tên CLB đề xuất")] public string Name { get; set; } = "";
    [StringLength(100), Display(Name = "Lĩnh vực")] public string? Category { get; set; }
    [Required, StringLength(2000), Display(Name = "Mục đích và kế hoạch hoạt động")] public string Purpose { get; set; } = "";
}
public class ClubProposalDecision
{
    [Required] public string RowVersion { get; set; } = "";
    [Required] public string Operation { get; set; } = "";
    [StringLength(30), Display(Name = "Mã CLB khi chấp nhận")] public string? Code { get; set; }
    [StringLength(500), Display(Name = "Lý do / ghi chú")] public string? Reason { get; set; }
    [BindNever, ValidateNever] public ClubProposal Item { get; set; } = null!;
    [BindNever] public bool IsAdmin { get; set; }
    [BindNever, ValidateNever] public IReadOnlyList<ProposalHistory> History { get; set; } = [];
}
public record ProposalHistory(string Action, string Actor, string? Reason, DateTimeOffset At);
public record ClubProposalList(IReadOnlyList<ClubProposal> Items, bool IsAdmin, ProposalStatus? Status, int Page, int Pages, int Count);
