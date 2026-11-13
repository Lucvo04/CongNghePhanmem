using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.Web.Services;

public class JoinRequestReviewService(ApplicationDbContext db, ClubAccessService access,
    StudentAccountService accounts, ClubOperationLocks locks)
{
    public IQueryable<JoinRequest> VisibleTo(string actorId) => db.JoinRequests.Where(r =>
        access.ManagedClubs(actorId).Any(c => c.Id == r.ClubId));

    public async Task<JoinRequestResult> DecideAsync(int id, string actorId, string? rowVersion,
        JoinRequestStatus decision, string? reason)
    {
        if (decision is not (JoinRequestStatus.Approved or JoinRequestStatus.Rejected))
            return new("Validation", "Vui lòng chọn chấp nhận hoặc từ chối.");
        reason = reason?.Trim();
        if (decision == JoinRequestStatus.Rejected && (string.IsNullOrWhiteSpace(reason) || reason.Length > 500))
            return new("Validation", "Vui lòng nhập lý do từ chối từ 1 đến 500 ký tự.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await ApplyAsync();
            if (result.Succeeded) await transaction.CommitAsync();
            else { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear(); return Conflict();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear(); return Conflict();
        }

        async Task<JoinRequestResult> ApplyAsync()
        {
            var target = await VisibleTo(actorId).AsNoTracking().Where(r => r.Id == id)
                .Select(r => new { r.UserId, r.ClubId }).SingleOrDefaultAsync();
            if (target is null) return new("NotFound");
            if (target.UserId == actorId) return new("Forbidden", "Bạn không được tự xét duyệt đơn của mình.");
            // Khóa tài khoản theo thứ tự cố định rồi mới khóa CLB; cùng giao thức gửi/rút/chuyển chủ nhiệm.
            foreach (var accountId in new[] { actorId, target.UserId }.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                if (!await locks.AccountAsync(accountId)) return Conflict();
            if (!await locks.ClubAsync(target.ClubId)) return Conflict();
            // Quyền được đọc lại sau khóa: chủ nhiệm cũ không xử lý được sau khi chuyển giao.
            if (!await access.CanManageAsync(target.ClubId, actorId)) return new("NotFound");
            var request = await db.JoinRequests.SingleOrDefaultAsync(r => r.Id == id);
            if (request is null) return new("NotFound");
            await db.Entry(request).ReloadAsync();
            if (db.Entry(request).State == EntityState.Detached || request.UserId != target.UserId || request.ClubId != target.ClubId)
                return Conflict();
            if (request.Status != JoinRequestStatus.Pending ||
                !string.Equals(Convert.ToBase64String(request.RowVersion), rowVersion, StringComparison.Ordinal)) return Conflict();
            var clubStatus = await db.Clubs.Where(c => c.Id == target.ClubId).Select(c => c.Status).SingleAsync();
            if (clubStatus == ClubStatus.Archived) return new("Conflict", "CLB đã lưu trữ, chỉ được xem lịch sử đơn.");
            var now = DateTimeOffset.UtcNow;
            if (decision == JoinRequestStatus.Approved)
            {
                if (clubStatus != ClubStatus.Active) return new("Conflict", "CLB đang tạm dừng, chưa thể tiếp nhận thành viên.");
                if (!await accounts.Students.AnyAsync(u => u.Id == target.UserId && u.Status == UserStatus.Active))
                    return new("Conflict", "Người gửi không còn là sinh viên đang hoạt động. Hãy kiểm tra lại hoặc từ chối đơn với lý do phù hợp.");
                if (await db.Memberships.AnyAsync(m => m.UserId == target.UserId && m.ClubId == target.ClubId && m.EndedAt == null))
                    return new("Conflict", "Người gửi đã có tư cách thành viên tại CLB này.");
                var member = new Membership { UserId = target.UserId, ClubId = target.ClubId, JoinedAt = now };
                db.Memberships.Add(member);
                await db.SaveChangesAsync();
                request.MembershipId = member.Id;
            }
            request.Status = decision;
            request.ClosedAt = now;
            request.DecidedById = actorId;
            request.DecisionReason = decision == JoinRequestStatus.Rejected ? reason : null;
            db.AuditLogs.Add(new AuditLog
            {
                ActorId = actorId, EntityType = nameof(JoinRequest), EntityId = id.ToString(),
                Action = decision == JoinRequestStatus.Approved ? "JoinRequestApproved" : "JoinRequestRejected",
                BeforeData = JsonSerializer.Serialize(new { Status = "Pending" }),
                // Nội dung riêng tư nằm trong đơn, audit chỉ lưu trạng thái và liên kết thành viên.
                AfterData = JsonSerializer.Serialize(new { request.ClubId, Status = decision.ToString(), request.MembershipId })
            });
            await db.SaveChangesAsync();
            return new("Success");
        }
    }

    private static JoinRequestResult Conflict() => new("Conflict", "Đơn hoặc dữ liệu liên quan vừa thay đổi. Vui lòng tải lại trước khi quyết định.");
}
