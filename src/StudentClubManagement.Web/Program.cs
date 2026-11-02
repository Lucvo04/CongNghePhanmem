using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Health;
using StudentClubManagement.Web.Security;
using StudentClubManagement.Web.Services;

var bootstrapAdmin = args.Contains("--bootstrap-admin", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--bootstrap-admin").ToArray());

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:DefaultConnection. Xem hướng dẫn User Secrets trong README.md.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddClubIdentity(builder.Environment.IsDevelopment());
builder.Services.AddScoped<AdminBootstrapper>();
builder.Services.AddScoped<StudentAccountService>();
builder.Services.AddScoped<PersonalProfileService>();
builder.Services.AddScoped<ClubOperationLocks>();
builder.Services.AddScoped<ClubAccessService>();
builder.Services.AddScoped<ClubManagementService>();

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always);
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);

var app = builder.Build();

if (bootstrapAdmin)
{
    await using var scope = app.Services.CreateAsyncScope();
    var request = app.Configuration.GetSection("BootstrapAdmin").Get<AdminBootstrapRequest>() ?? new();
    try
    {
        var result = await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().CreateAsync(request);
        Console.WriteLine(result.Succeeded
            ? "Đã tạo quản trị viên đầu tiên. Mật khẩu không được ghi vào log."
            : string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Code}: {error.Description}")));
        Environment.ExitCode = result.Succeeded ? 0 : 1;
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Không thể khởi tạo quản trị viên. Kiểm tra kết nối, migration và chạy lại nếu có thao tác đồng thời.");
        Environment.ExitCode = 1;
    }
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program { }
