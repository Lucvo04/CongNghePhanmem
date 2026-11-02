using StudentClubManagement.Web.Models;
namespace StudentClubManagement.Web.ViewModels.Admin;
public record DashboardViewModel(string FullName, int StudentCount, int ClubCount, int ActiveClubCount,
    int ActiveMembershipCount, IReadOnlyList<DashboardClub> RecentClubs, IReadOnlyList<DashboardActivity> RecentActivity);
public record DashboardClub(int Id, string Code, string Name, ClubStatus Status);
public record DashboardActivity(string Action, DateTimeOffset OccurredAt);
