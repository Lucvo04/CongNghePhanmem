using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Services;

public record JoinRequestResult(string Code, string Message = "")
{
    public bool Succeeded => Code == "Success";
}

public class JoinRequestService(ApplicationDbContext db, StudentAccountService accounts, ClubOperationLocks locks)
{
    public Task<bool> IsStudentAsync(string actorId) => accounts.Students.AnyAsync(u => u.Id == actorId && u.Status == UserStatus.Active);

    public async Task<string?> UnavailableReasonAsync(int clubId, ClubStatus status, string actorId)
    {
        if (status != ClubStatus.Active) return "CLB hiện không tiếp nhận đơn gia nhập mới.";
        if (await db.Memberships.AnyAsync(m => m.ClubId == clubId && m.UserId == actorId && m.EndedAt == null))
            return "Bạn đã có tư cách thành viên tại CLB này, kể cả khi đang tạm đình chỉ.";
        if (await db.JoinRequests.AnyAsync(r => r.ClubId == clubId && r.UserId == actorId && r.Status == JoinRequestStatus.Pending))
            return "Bạn đã có một đơn đang chờ xử lý tại CLB này. Hãy theo dõi trong Đơn của tôi.";
        return null;
    }

    public async Task<JoinRequestResult> SubmitAsync(int clubId, string actorId, string? reason)
    {
        reason = reason?.Trim();
        if (clubId <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return new("Validation", "Vui lòng nhập lý do tham gia từ 1 đến 1.000 ký tự.");
        return await InTransactionAsync(async () =>
        {
            // Cùng thứ tự khóa với nghiệp vụ thành viên: tài khoản trước, CLB sau.
            if (!await locks.AccountAsync(actorId) || !await locks.ClubAsync(clubId)) return Conflict();
            if (!await IsStudentAsync(actorId)) return new("Forbidden");
            var club = await db.Clubs.AsNoTracking().SingleOrDefaultAsync(c => c.Id == clubId);
            if (club is null || club.Status == ClubStatus.Archived) return new("NotFound");
            var unavailable = await UnavailableReasonAsync(clubId, club.Status, actorId);
            if (unavailable is not null) return new("Conflict", unavailable);
            var request = new JoinRequest { ClubId = clubId, UserId = actorId, Reason = reason! };
            db.JoinRequests.Add(request);
            await db.SaveChangesAsync();
            AddAudit(request, actorId, "JoinRequestSubmitted");
            await db.SaveChangesAsync();
            return new("Success");
        });
    }

    public Task<JoinRequestResult> WithdrawAsync(int id, string actorId, string? rowVersion) => InTransactionAsync(async () =>
    {
        if (!await locks.AccountAsync(actorId)) return Conflict();
        if (!await IsStudentAsync(actorId)) return new("Forbidden");
        // Không tiết lộ đơn người khác; không lấy UserId từ dữ liệu form.
        var clubId = await db.JoinRequests.Where(r => r.Id == id && r.UserId == actorId).Select(r => (int?)r.ClubId).SingleOrDefaultAsync();
        if (clubId is null) return new("NotFound");
        if (!await locks.ClubAsync(clubId.Value)) return Conflict();
        var request = await db.JoinRequests.SingleAsync(r => r.Id == id && r.UserId == actorId);
        await db.Entry(request).ReloadAsync();
        if (db.Entry(request).State == EntityState.Detached || request.UserId != actorId) return new("NotFound");
        if (request.Status != JoinRequestStatus.Pending ||
            !string.Equals(Convert.ToBase64String(request.RowVersion), rowVersion, StringComparison.Ordinal))
            return Conflict();
        request.Status = JoinRequestStatus.Withdrawn;
        request.ClosedAt = DateTimeOffset.UtcNow;
        AddAudit(request, actorId, "JoinRequestWithdrawn");
        await db.SaveChangesAsync();
        return new("Success");
    });

    private void AddAudit(JoinRequest request, string actorId, string action) => db.AuditLogs.Add(new AuditLog
    {
        ActorId = actorId, Action = action, EntityType = nameof(JoinRequest), EntityId = request.Id.ToString(),
        // Lý do tham gia chỉ nằm trong đơn, không sao chép nội dung cá nhân vào audit.
        AfterData = JsonSerializer.Serialize(new { request.ClubId, Status = request.Status.ToString() })
    });

    private async Task<JoinRequestResult> InTransactionAsync(Func<Task<JoinRequestResult>> action)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await action();
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
    }

    private static JoinRequestResult Conflict() => new("Conflict", "Đơn hoặc dữ liệu liên quan vừa thay đổi. Vui lòng tải lại trang và kiểm tra Đơn của tôi.");
}