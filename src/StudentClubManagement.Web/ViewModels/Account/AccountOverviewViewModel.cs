namespace StudentClubManagement.Web.ViewModels.Account;

public record AccountOverviewViewModel(string FullName, string Email, string? StudentCode, bool IsAdmin,
    string? PhoneNumber, string? ClassName, string? Faculty);
