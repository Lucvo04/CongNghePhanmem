using System.ComponentModel.DataAnnotations;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Events;

public class EventRegistrationForm
{
    [Required] public string EventVersion { get; set; } = "";
    public string? RowVersion { get; set; }
}
public class CancelRegistrationForm
{
    [Required] public string RowVersion { get; set; } = "";
}
public record RegistrationPanel(int Count, EventRegistration? Own, bool CanRegister, bool CanCancel, string Message);
public record RegistrationRow(int Id, int EventId, string Title, string ClubName, DateTimeOffset StartsAt,
    string FullName, string? StudentCode, DateTimeOffset RegisteredAt, DateTimeOffset? CancelledAt, byte[] RowVersion,
    bool CanCancel, bool CanView, string? CancellationReason, EventStatus EventStatus);
public record RegistrationList(IReadOnlyList<RegistrationRow> Items, ClubEvent? Event, int Page, int Pages, int Count,
    RegistrationFilter Filter, IReadOnlyList<CalendarClub> Clubs);

public class RegistrationFilter : IValidatableObject
{
    [StringLength(180, ErrorMessage = "Từ khóa tối đa 180 ký tự.")]
    public string? Search { get; set; }
    public string Status { get; set; } = "all";
    [Range(1, int.MaxValue, ErrorMessage = "CLB không hợp lệ.")]
    public int? ClubId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is not ("all" or "confirmed" or "cancelled"))
            yield return new("Chọn tất cả, đã đăng ký hoặc đã hủy đăng ký.", [nameof(Status)]);
        if (From.HasValue && From.Value.Year is < 1900 or > 9998)
            yield return new("Từ ngày cần nằm trong năm 1900–9998.", [nameof(From)]);
        if (To.HasValue && To.Value.Year is < 1900 or > 9998)
            yield return new("Đến ngày cần nằm trong năm 1900–9998.", [nameof(To)]);
        if (From > To)
            yield return new("Đến ngày phải bằng hoặc sau Từ ngày.", [nameof(To)]);
    }
}

public record RegistrationHistoryRow(long Id, string Action, bool IsSelf, bool IsRepeat, string? Reason, DateTimeOffset OccurredAt);
public record RegistrationHistory(RegistrationRow Registration, IReadOnlyList<RegistrationHistoryRow> Items,
    int Page, int Pages, int Count, string? ReturnUrl);
