using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Clubs;

// Chỉ chứa thông tin CLB công khai; không đưa entity/membership/user vào View.
public record PublicClubCard(int Id, string Code, string Name, string? Category, string? Description, ClubStatus Status);

public class PublicClubListViewModel
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? FilterError { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public IReadOnlyList<string> Categories { get; set; } = [];
    public IReadOnlyList<PublicClubCard> Clubs { get; set; } = [];
}

public record PublicClubDetailsViewModel(string Code, string Name, string? Category, string? Description, ClubStatus Status);
