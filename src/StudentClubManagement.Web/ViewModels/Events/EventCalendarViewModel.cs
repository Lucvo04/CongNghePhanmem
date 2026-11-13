using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Events;

public class EventCalendarViewModel
{
    [Display(Name = "Từ ngày")]
    public DateOnly? From { get; set; }
    [Display(Name = "Đến ngày")]
    public DateOnly? To { get; set; }
    [Display(Name = "Câu lạc bộ")]
    public int? ClubId { get; set; }
    [Display(Name = "Trạng thái")]
    public EventStatus? Status { get; set; }
    [StringLength(180, ErrorMessage = "Tên tìm kiếm tối đa 180 ký tự."), Display(Name = "Tên hoạt động")]
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    [BindNever, ValidateNever] public IReadOnlyList<CalendarClub> Clubs { get; set; } = [];
    [BindNever, ValidateNever] public IReadOnlyList<CalendarEvent> Items { get; set; } = [];
    [BindNever] public int Count { get; set; }
    [BindNever] public int Pages { get; set; } = 1;
    [BindNever] public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
}

public record CalendarClub(int Id, string Name);
public record CalendarEvent(int Id, int ClubId, string ClubName, string Title, string Location,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, EventAudience Audience, EventStatus Status);
