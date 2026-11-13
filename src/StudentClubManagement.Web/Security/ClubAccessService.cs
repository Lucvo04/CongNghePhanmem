using Microsoft.EntityFrameworkCore;
using StudentClubManagement.Web.Data;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.Security;

public class ClubAccessService(ApplicationDbContext db)
{
    public Task<bool> IsAdminAsync(string actorId) => db.Users.AnyAsync(user => user.Id == actorId && user.Status == UserStatus.Active &&
        user.RoleId == "system-admin");

    public IQueryable<Club> ManagedClubs(string actorId) => db.Clubs.Where(club =>
        db.Users.Any(user => user.Id == actorId && user.Status == UserStatus.Active &&
            (user.RoleId == "system-admin" ||
             club.Memberships.Any(member => member.UserId == user.Id && member.Role == ClubRole.Manager &&
                 member.Status == MembershipStatus.Active && member.EndedAt == null &&
                 user.RoleId == "system-student"))));

    public Task<bool> IsManagerAsync(string actorId) => db.Memberships.AnyAsync(m => m.UserId == actorId &&
        m.Role == ClubRole.Manager && m.Status == MembershipStatus.Active && m.EndedAt == null &&
        m.Club.Status != ClubStatus.Archived && m.User.Status == UserStatus.Active && m.User.RoleId == "system-student");

    public Task<bool> CanManageAsync(int clubId, string actorId) => ManagedClubs(actorId).AnyAsync(club => club.Id == clubId);
}
