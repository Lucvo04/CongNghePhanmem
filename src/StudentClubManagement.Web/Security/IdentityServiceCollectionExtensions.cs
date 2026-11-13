using Microsoft.AspNetCore.Identity;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Security;

public static class IdentityServiceCollectionExtensions
{
    public static IdentityBuilder AddClubIdentity(this IServiceCollection services, bool development = false)
    {
        // Đồ án quy mô nhỏ: kiểm tra stamp/trạng thái/quyền mới ở mỗi request có cookie.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "StudentClub.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        return services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddUserStore<CoreUserStore>()
            .AddRoleStore<CoreRoleStore>()
            .AddErrorDescriber<VietnameseIdentityErrorDescriber>()
            .AddSignInManager<ClubSignInManager>();
    }
}
