using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;

namespace StudentClubManagement.Web.Services;

public record WorkflowResult(string Code, string Message = "", int? Id = null)
{ public bool Succeeded => Code == "Success"; }

// Dùng chung cho các luồng gửi/duyệt mới: nghiệp vụ và audit phải cùng thành công.
public static class WorkflowTransaction
{
    public static async Task<WorkflowResult> RunAsync(ApplicationDbContext db, Func<Task<WorkflowResult>> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await action();
            if (result.Succeeded) await tx.CommitAsync();
            else { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
            return result;
        }
        catch (DbUpdateConcurrencyException)
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { await tx.RollbackAsync(); db.ChangeTracker.Clear(); return new("Conflict", "Thông tin đã được sử dụng hoặc vừa thay đổi. Vui lòng tải lại và kiểm tra."); }
    }
    public static WorkflowResult Conflict() => new("Conflict", "Dữ liệu vừa thay đổi hoặc đang được xử lý. Vui lòng tải lại trang.");
}
