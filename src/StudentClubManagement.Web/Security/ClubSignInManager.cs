using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Security;

public class ClubSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.Status == UserStatus.Active && !user.TwoFactorEnabled && await base.CanSignInAsync(user);

    public override async Task<ApplicationUser?> ValidateSecurityStampAsync(ClaimsPrincipal? principal)
    {
        var user = await base.ValidateSecurityStampAsync(principal);
        // Schema rút gọn chưa hỗ trợ 2FA: không cho bỏ qua nếu cờ bị bật ngoài UI.
        return user?.Status == UserStatus.Active && !user.TwoFactorEnabled ? user : null;
    }
}
