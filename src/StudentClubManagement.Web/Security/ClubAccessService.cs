using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Security;

public class ClubAccessService(ApplicationDbContext db)
{
    public Task<bool> IsAdminAsync(string actorId) => db.Users.AnyAsync(user => user.Id == actorId && user.Status == UserStatus.Active &&
        db.UserRoles.Any(ur => ur.UserId == user.Id && db.Roles.Any(role => role.Id == ur.RoleId && role.Name == SystemRoles.Admin)));

    public IQueryable<Club> ManagedClubs(string actorId) => db.Clubs.Where(club =>
        db.Users.Any(user => user.Id == actorId && user.Status == UserStatus.Active &&
            (db.UserRoles.Any(ur => ur.UserId == user.Id && db.Roles.Any(role => role.Id == ur.RoleId && role.Name == SystemRoles.Admin)) ||
             club.Memberships.Any(member => member.UserId == user.Id && member.Role == ClubRole.Manager &&
                 member.Status == MembershipStatus.Active && member.EndedAt == null &&
                 db.UserRoles.Any(ur => ur.UserId == user.Id && db.Roles.Any(role => role.Id == ur.RoleId && role.Name == SystemRoles.Student))))));

    public Task<bool> CanManageAsync(int clubId, string actorId) => ManagedClubs(actorId).AnyAsync(club => club.Id == clubId);
}
