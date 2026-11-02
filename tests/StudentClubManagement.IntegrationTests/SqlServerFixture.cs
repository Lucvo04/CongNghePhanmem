using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;

namespace StudentClubManagement.IntegrationTests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string databaseName = $"StudentClubManagement_Tests_{Guid.NewGuid():N}";
    private bool ownsDatabase;

    public string ConnectionString { get; private set; } = string.Empty;

    public ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);

    public void AssertOwnsContext(ApplicationDbContext db)
    {
        var actual = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        var expected = new SqlConnectionStringBuilder(ConnectionString);
        // EF có thể thêm Application Name; mục tiêu server/database phải giữ nguyên.
        Assert.Equal(expected.DataSource, actual.DataSource);
        Assert.Equal(databaseName, actual.InitialCatalog);
    }

    public async Task InitializeAsync()
    {
        // Chỉ nhận tên server; không nhận database ứng dụng từ cấu hình bên ngoài.
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("STUDENTCLUB_TEST_SQLSERVER") ?? @".\SQLEXPRESS",
            InitialCatalog = "master",
            IntegratedSecurity = true,
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 10
        };

        await using (var master = new SqlConnection(connection.ConnectionString))
        {
            await master.OpenAsync();
            await using var query = new SqlCommand("SELECT DB_ID(@databaseName)", master);
            query.Parameters.AddWithValue("@databaseName", databaseName);
            if (await query.ExecuteScalarAsync() is not DBNull)
            {
                throw new InvalidOperationException("Database kiểm thử đã tồn tại; không được dùng lại hoặc xóa.");
            }
        }

        connection.InitialCatalog = databaseName;
        ConnectionString = connection.ConnectionString;
        ownsDatabase = true;
        try
        {
            await using var db = CreateContext();
            await db.Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (!ownsDatabase)
        {
            return;
        }

        var actualName = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if (actualName != databaseName ||
            !actualName.StartsWith("StudentClubManagement_Tests_", StringComparison.Ordinal) ||
            !Guid.TryParseExact(actualName["StudentClubManagement_Tests_".Length..], "N", out _))
        {
            throw new InvalidOperationException("Từ chối xóa database không thuộc lần chạy kiểm thử này.");
        }

        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        ownsDatabase = false;
    }
}
