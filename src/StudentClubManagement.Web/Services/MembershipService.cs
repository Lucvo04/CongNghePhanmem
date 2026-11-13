using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;

namespace StudentClubManagement.Web.Services;

public record MembershipResult(string Code, string Message = "")
{
    public bool Succeeded => Code == "Success";
}

public class MembershipService(ApplicationDbContext db, ClubAccessService access,
    StudentAccountService accounts, ClubOperationLocks locks)
{
    public IQueryable<Membership> ManagedBy(string actorId) => db.Memberships.Where(m =>
        access.ManagedClubs(actorId).Any(c => c.Id == m.ClubId));
    public Task<bool> CanUseOwnAsync(string actorId) => accounts.Students.AnyAsync(u => u.Id == actorId && u.Status == UserStatus.Active);
    public IQueryable<Membership> VisibleTo(string actorId) => db.Memberships.Where(m =>
        access.ManagedClubs(actorId).Any(c => c.Id == m.ClubId) ||
        (m.UserId == actorId && accounts.Students.Any(u => u.Id == actorId && u.Status == UserStatus.Active)));

    public async Task<MembershipResult> ChangeStatusAsync(int id, string actorId, string? rowVersion,
        MembershipStatus newStatus, string? reason, bool selfLeave = false)
    {
        reason = reason?.Trim();
        if (!Enum.IsDefined(newStatus) || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return new("Validation", "Vui lòng chọn trạng thái hợp lệ và nhập lý do từ 1 đến 500 ký tự.");
        if (selfLeave && newStatus != MembershipStatus.Left) return new("Forbidden");
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await ApplyAsync();
            if (result.Succeeded) await transaction.CommitAsync();
            else { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
            return result;
        }
        catch (DbUpdateConcurrencyException)
        { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); return Conflict(); }

        async Task<MembershipResult> ApplyAsync()
        {
            var scope = selfLeave ? VisibleTo(actorId).Where(m => m.UserId == actorId) : ManagedBy(actorId);
            var target = await scope.AsNoTracking().Where(m => m.Id == id).Select(m => new { m.UserId, m.ClubId }).SingleOrDefaultAsync();
            if (target is null) return new("NotFound");
            foreach (var accountId in new[] { actorId, target.UserId }.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                if (!await locks.AccountAsync(accountId)) return Conflict();
            if (!await locks.ClubAsync(target.ClubId)) return Conflict();
            if (selfLeave ? !await CanUseOwnAsync(actorId) : !await access.CanManageAsync(target.ClubId, actorId)) return new("NotFound");
            var member = await db.Memberships.SingleOrDefaultAsync(m => m.Id == id);
            if (member is null) return new("NotFound");
            await db.Entry(member).ReloadAsync();
            if (db.Entry(member).State == EntityState.Detached || member.UserId != target.UserId || member.ClubId != target.ClubId) return Conflict();
            if (!string.Equals(Convert.ToBase64String(member.RowVersion), rowVersion, StringComparison.Ordinal)) return Conflict();
            if (member.Role == ClubRole.Manager && member.EndedAt == null)
                return new("Conflict", "Cần chuyển chủ nhiệm cho một thành viên đang hoạt động trước khi thay đổi trạng thái người phụ trách.");
            if (member.Status == MembershipStatus.Left || member.EndedAt is not null)
                return new("Conflict", "Đợt tham gia đã kết thúc. Muốn gia nhập lại cần gửi và xét duyệt đơn mới.");
            if (member.Status == newStatus) return Conflict();
            var clubStatus = await db.Clubs.Where(c => c.Id == member.ClubId).Select(c => c.Status).SingleAsync();
            if (clubStatus == ClubStatus.Archived) return new("Conflict", "CLB đã lưu trữ, chỉ được xem lịch sử thành viên.");
            if (newStatus == MembershipStatus.Active)
            {
                if (member.Status != MembershipStatus.Suspended || clubStatus != ClubStatus.Active)
                    return new("Conflict", "Chỉ khôi phục thành viên tạm đình chỉ khi CLB đang hoạt động.");
                if (!await accounts.Students.AnyAsync(u => u.Id == target.UserId && u.Status == UserStatus.Active))
                    return new("Conflict", "Tài khoản cần là sinh viên đang hoạt động trước khi khôi phục tư cách thành viên.");
            }
            var before = Snapshot(member);
            var now = DateTimeOffset.UtcNow;
            var futureInternal = db.EventRegistrations.Where(r => r.UserId == member.UserId && r.CancelledAt == null &&
                r.Event.ClubId == member.ClubId && r.Event.Audience == EventAudience.Club &&
                r.Event.Status == EventStatus.Published && r.Event.StartsAt > now);
            if (selfLeave && await futureInternal.AnyAsync())
                return new("Conflict", "Bạn còn đăng ký hoạt động nội bộ chưa diễn ra. Vào Hoạt động đã đăng ký để hủy trước khi rời CLB.");
            if (newStatus is MembershipStatus.Suspended or MembershipStatus.Left)
                await EventRegistrationService.CancelMatchingAsync(db, futureInternal, actorId,
                    reason, now);
            member.Status = newStatus;
            member.EndedAt = newStatus == MembershipStatus.Left ? now : null;
            db.AuditLogs.Add(new AuditLog
            {
                ActorId = actorId, EntityType = nameof(Membership), EntityId = member.Id.ToString(),
                Action = newStatus switch { MembershipStatus.Active => "MembershipRestored", MembershipStatus.Suspended => "MembershipSuspended", _ => "MembershipEnded" },
                BeforeData = before, AfterData = Snapshot(member), Reason = reason, OccurredAt = now
            });
            await db.SaveChangesAsync();
            return new("Success");
        }
    }

    private static string Snapshot(Membership member) => JsonSerializer.Serialize(new
        { member.ClubId, Status = member.Status.ToString(), Role = member.Role.ToString(), member.JoinedAt, member.EndedAt });
    private static MembershipResult Conflict() => new("Conflict", "Thành viên hoặc phân công vừa thay đổi. Vui lòng tải lại trang trước khi thao tác.");
}
