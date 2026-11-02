using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.ViewModels.Clubs;

namespace StudentClubManagement.Web.Services;

public record ClubResult(string Code, string Message = "", string Field = "", int? ClubId = null)
{
    public bool Succeeded => Code == "Success";
}

public class ClubManagementService(ApplicationDbContext db, StudentAccountService accounts,
    ClubAccessService access, ClubOperationLocks operationLocks)
{
    public IQueryable<Membership> EligibleMembers(int clubId) => db.Memberships.Where(member =>
        member.ClubId == clubId && member.Role == ClubRole.Member && member.Status == MembershipStatus.Active &&
        member.EndedAt == null && accounts.Students.Any(user => user.Id == member.UserId && user.Status == UserStatus.Active));

    public Task<ClubResult> CreateAsync(CreateClubViewModel form, string actorId) => InTransactionAsync(async () =>
    {
        if (!await access.IsAdminAsync(actorId)) return Forbidden();
        var code = form.Code.Trim().ToUpperInvariant();
        if (code.Length > 30 || !Regex.IsMatch(code, @"\A[A-Z0-9][A-Z0-9_-]*\z") || !ValidProfile(form))
            return new("Validation", "Thông tin CLB không hợp lệ.");
        var studentCode = form.ManagerStudentCode.Trim().ToUpperInvariant();
        var candidateId = await accounts.Students.AsNoTracking().Where(user => user.StudentCode == studentCode)
            .Select(user => user.Id).SingleOrDefaultAsync();
        if (candidateId is null) return InvalidManager();
        if (!await operationLocks.AccountAsync(candidateId)) return Conflict();
        // Đọc lại sau khóa; có thể Admin khác vừa khóa tài khoản trong lúc chờ.
        if (!await accounts.Students.AnyAsync(user => user.Id == candidateId && user.Status == UserStatus.Active)) return InvalidManager();
        if (await db.Clubs.AnyAsync(club => club.Code == code)) return DuplicateCode();

        var club = new Club { Code = code, Name = form.Name.Trim(), Category = Optional(form.Category), Description = Optional(form.Description) };
        var membership = new Membership { Club = club, UserId = candidateId, Role = ClubRole.Manager, Status = MembershipStatus.Active };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, Action = "ClubCreated", EntityType = nameof(Club), EntityId = club.Id.ToString(),
            AfterData = JsonSerializer.Serialize(new { club.Code, ManagerMembershipId = membership.Id, ManagerUserId = candidateId, Status = "Active" })
        });
        await db.SaveChangesAsync();
        return new("Success", ClubId: club.Id);
    });

    public Task<ClubResult> EditAsync(int id, EditClubViewModel form, string actorId) => InTransactionAsync(async () =>
    {
        if (!await operationLocks.ClubAsync(id)) return Conflict();
        var club = await db.Clubs.SingleOrDefaultAsync(club => club.Id == id);
        if (club is null) return Missing();
        // Quyền được kiểm tra sau khóa CLB để không dùng quyền cũ khi vừa chuyển giao.
        if (!await access.CanManageAsync(id, actorId)) return Forbidden();
        if (club.Status == ClubStatus.Archived) return new("Validation", "CLB đã lưu trữ không thể chỉnh sửa.");
        if (!Matches(form.RowVersion, club.RowVersion)) return Conflict();
        if (!ValidProfile(form)) return new("Validation", "Thông tin CLB không hợp lệ.");
        var before = ProfileSnapshot(club);
        club.Name = form.Name.Trim(); club.Category = Optional(form.Category); club.Description = Optional(form.Description);
        if (before == ProfileSnapshot(club)) return new("Success", ClubId: id);
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, Action = "ClubProfileUpdated", EntityType = nameof(Club), EntityId = id.ToString(),
            BeforeData = before, AfterData = ProfileSnapshot(club)
        });
        await db.SaveChangesAsync();
        return new("Success", ClubId: id);
    });

    public Task<ClubResult> TransferAsync(int id, TransferManagerViewModel form, string actorId) => InTransactionAsync(async () =>
    {
        if (!await access.IsAdminAsync(actorId)) return Forbidden();
        if (string.IsNullOrWhiteSpace(form.NewManagerUserId) || string.IsNullOrWhiteSpace(form.Reason) || form.Reason.Length > 500)
            return new("Validation", "Vui lòng chọn người nhận và nhập lý do hợp lệ.");
        if (!await operationLocks.AccountAsync(form.NewManagerUserId) || !await operationLocks.ClubAsync(id)) return Conflict();
        var club = await db.Clubs.SingleOrDefaultAsync(club => club.Id == id);
        if (club is null) return Missing();
        if (club.Status == ClubStatus.Archived) return new("Validation", "CLB đã lưu trữ không thể chuyển chủ nhiệm.");
        if (!Matches(form.RowVersion, club.RowVersion)) return Conflict();
        var current = await db.Memberships.SingleOrDefaultAsync(member => member.ClubId == id && member.Role == ClubRole.Manager && member.EndedAt == null);
        if (current is null) return new("Validation", "CLB thiếu chủ nhiệm hiện tại. Cần kiểm tra dữ liệu trước khi chuyển giao.");
        var next = await EligibleMembers(id).SingleOrDefaultAsync(member => member.UserId == form.NewManagerUserId);
        if (next is null)
            return new("Validation", "Chỉ được chọn thành viên đang hoạt động của CLB, có tài khoản sinh viên đang hoạt động và khác chủ nhiệm hiện tại.", nameof(form.NewManagerUserId));

        var before = JsonSerializer.Serialize(new { ManagerMembershipId = current.Id, ManagerUserId = current.UserId });
        current.Role = ClubRole.Member;
        // Unique index được SQL kiểm tra ngay: hạ người cũ trước, nâng người mới sau, cùng transaction.
        await db.SaveChangesAsync();
        next.Role = ClubRole.Manager;
        // Đổi phiên bản của toàn CLB, kể cả khi chỉ thay người phụ trách.
        db.Entry(club).Property(value => value.Status).IsModified = true;
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, Action = "ClubManagerTransferred", EntityType = nameof(Club), EntityId = id.ToString(),
            BeforeData = before, AfterData = JsonSerializer.Serialize(new { ManagerMembershipId = next.Id, ManagerUserId = next.UserId }),
            Reason = form.Reason.Trim()
        });
        await db.SaveChangesAsync();
        return new("Success", ClubId: id);
    });

    private async Task<ClubResult> InTransactionAsync(Func<Task<ClubResult>> operation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await operation();
            if (result.Succeeded) await transaction.CommitAsync();
            else { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear(); return Conflict();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await transaction.RollbackAsync(); db.ChangeTracker.Clear();
            return new("Conflict", "Mã CLB hoặc phân công vừa được dùng bởi một yêu cầu khác. Vui lòng tải lại và kiểm tra.");
        }
    }

    private static bool Matches(string encoded, byte[] current)
    {
        Span<byte> parsed = stackalloc byte[8];
        return !string.IsNullOrEmpty(encoded) && Convert.TryFromBase64String(encoded, parsed, out var written) &&
            written == 8 && parsed.SequenceEqual(current);
    }
    private static bool ValidProfile(ClubProfileForm form) => !string.IsNullOrWhiteSpace(form.Name) && form.Name.Length <= 150 &&
        (form.Category?.Length ?? 0) <= 100 && (form.Description?.Length ?? 0) <= 2000;
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    // Giữ audit hữu hạn; hash chứng minh phần giới thiệu thay đổi, không phải bản sao phục hồi nội dung.
    private static string ProfileSnapshot(Club club) => JsonSerializer.Serialize(new
    {
        club.Name, club.Category, DescriptionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(club.Description ?? "")))
    });
    private static ClubResult Forbidden() => new("Forbidden", "Bạn không có quyền quản lý CLB này.");
    private static ClubResult Missing() => new("NotFound", "Không tìm thấy câu lạc bộ.");
    private static ClubResult Conflict() => new("Conflict", "CLB hoặc phân công vừa thay đổi hoặc đang được xử lý. Vui lòng tải lại thông tin và thử lại.");
    private static ClubResult DuplicateCode() => new("Validation", "Mã CLB đã được sử dụng.", "Code");
    private static ClubResult InvalidManager() => new("Validation", "Không tìm thấy sinh viên đang hoạt động với mã này. Tài khoản Admin không được chọn làm chủ nhiệm.", "ManagerStudentCode");
}
