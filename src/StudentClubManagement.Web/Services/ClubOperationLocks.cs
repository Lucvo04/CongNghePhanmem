using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;

namespace StudentClubManagement.Web.Services;

// Khóa thuộc transaction SQL, có hiệu lực qua nhiều request/tiến trình.
// Khi cần cả hai: luôn khóa tài khoản người nhận trước, rồi khóa CLB.
public class ClubOperationLocks(ApplicationDbContext db)
{
    public Task<bool> AccountAsync(string id) => AcquireAsync("Account:" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToUpperInvariant()))));

    public Task<bool> ClubAsync(int id) => AcquireAsync($"Club:{id}");

    private async Task<bool> AcquireAsync(string resource)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Khóa nghiệp vụ cần transaction đang hoạt động.");
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var name = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = "StudentClub:" + resource };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000;",
            result, name);
        return (int)result.Value >= 0;
    }
}
