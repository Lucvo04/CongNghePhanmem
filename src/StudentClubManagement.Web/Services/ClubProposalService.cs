using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.ViewModels.Clubs;

namespace StudentClubManagement.Web.Services;

public class ClubProposalService(ApplicationDbContext db, ClubAccessService access, ClubOperationLocks locks)
{
    public Task<bool> CanSubmitAsync(string actor) => db.Users.AnyAsync(u => u.Id == actor && u.Status == UserStatus.Active && u.RoleId == "system-student");
    public IQueryable<ClubProposal> VisibleTo(string actor) => db.ClubProposals.Where(p => p.ApplicantId == actor ||
        db.Users.Any(u => u.Id == actor && u.Status == UserStatus.Active && u.RoleId == "system-admin"));

    public Task<WorkflowResult> SubmitAsync(string actor, ClubProposalForm form) => WorkflowTransaction.RunAsync(db, async () =>
    {
        if (!await locks.AccountAsync(actor)) return WorkflowTransaction.Conflict();
        if (!await CanSubmitAsync(actor)) return new("Forbidden");
        if (string.IsNullOrWhiteSpace(form.Name) || form.Name.Length > 150 || string.IsNullOrWhiteSpace(form.Purpose) || form.Purpose.Length > 2000 || form.Category?.Length > 100)
            return new("Validation", "Vui lòng nhập tên CLB và mục đích/kế hoạch trong giới hạn cho phép.");
        if (await db.ClubProposals.AnyAsync(p => p.ApplicantId == actor && p.Status == ProposalStatus.Pending))
            return new("Conflict", "Bạn đã có một đơn thành lập đang chờ. Hãy chờ xử lý hoặc rút đơn cũ trước khi gửi lại.");
        var proposal = new ClubProposal { ApplicantId = actor, Name = form.Name.Trim(), Category = form.Category?.Trim(), Purpose = form.Purpose.Trim() };
        db.ClubProposals.Add(proposal); await db.SaveChangesAsync();
        Audit(proposal, actor, "ClubProposalSubmitted", null, null); await db.SaveChangesAsync();
        return new("Success", Id: proposal.Id);
    });

    public Task<WorkflowResult> DecideAsync(int id, string actor, ClubProposalDecision form) => WorkflowTransaction.RunAsync(db, async () =>
    {
        var target = await VisibleTo(actor).AsNoTracking().Where(p => p.Id == id).Select(p => p.ApplicantId).SingleOrDefaultAsync();
        if (target is null) return new("NotFound");
        foreach (var account in new[] { actor, target }.Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (!await locks.AccountAsync(account)) return WorkflowTransaction.Conflict();
        var item = await db.ClubProposals.SingleAsync(p => p.Id == id); await db.Entry(item).ReloadAsync();
        var admin = await access.IsAdminAsync(actor);
        if (form.Operation == "Withdraw" ? item.ApplicantId != actor || !await CanSubmitAsync(actor) : !admin) return new("NotFound");
        if (item.Status != ProposalStatus.Pending || Convert.ToBase64String(item.RowVersion) != form.RowVersion) return WorkflowTransaction.Conflict();
        if (form.Reason?.Length > 500) return new("Validation", "Lý do tối đa 500 ký tự.");
        if (form.Operation is "Approve" or "Reject" && actor == item.ApplicantId) return new("Forbidden");
        var before = Snapshot(item);
        switch (form.Operation)
        {
            case "Withdraw": item.Status = ProposalStatus.Withdrawn; break;
            case "Reject":
                if (string.IsNullOrWhiteSpace(form.Reason)) return new("Validation", "Vui lòng nhập lý do từ chối.");
                item.Status = ProposalStatus.Rejected; break;
            case "Approve":
                if (!await CanSubmitAsync(item.ApplicantId)) return new("Validation", "Người gửi cần là sinh viên đang hoạt động để trở thành chủ nhiệm.");
                var code = form.Code?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code) || code.Length > 30 || !Regex.IsMatch(code, @"\A[A-Z0-9][A-Z0-9_-]*\z"))
                    return new("Validation", "Mã CLB cần 1–30 ký tự chữ không dấu, số, gạch ngang hoặc gạch dưới.");
                if (await db.Clubs.AnyAsync(c => c.Code == code)) return new("Validation", "Mã CLB đã được sử dụng. Vui lòng chọn mã khác.");
                var club = new Club { Code = code, Name = item.Name, Category = item.Category, Description = item.Purpose };
                var manager = new Membership { Club = club, UserId = item.ApplicantId, Role = ClubRole.Manager };
                db.Memberships.Add(manager); await db.SaveChangesAsync();
                item.Status = ProposalStatus.Approved; item.ClubId = club.Id;
                db.AuditLogs.Add(new AuditLog { ActorId = actor, Action = "ClubCreated", EntityType = nameof(Club), EntityId = club.Id.ToString(),
                    AfterData = JsonSerializer.Serialize(new { club.Code, ProposalId = id, ManagerMembershipId = manager.Id, ManagerUserId = item.ApplicantId }) });
                break;
            default: return new("Validation", "Quyết định không hợp lệ.");
        }
        Audit(item, actor, "ClubProposal" + (form.Operation == "Approve" ? "Approved" : form.Operation == "Reject" ? "Rejected" : "Withdrawn"), before, form.Reason);
        await db.SaveChangesAsync(); return new("Success", Id: id);
    });

    private static string Snapshot(ClubProposal item) => JsonSerializer.Serialize(new { Status = item.Status.ToString(), item.ClubId });
    private void Audit(ClubProposal item, string actor, string action, string? before, string? reason) => db.AuditLogs.Add(new AuditLog
    { ActorId = actor, Action = action, EntityType = nameof(ClubProposal), EntityId = item.Id.ToString(), BeforeData = before, AfterData = Snapshot(item), Reason = reason?.Trim() });
}
