using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Events;

public class EventForm
{
    [Required, StringLength(180), Display(Name = "Tên hoạt động")]
    public string Title { get; set; } = "";
    [StringLength(4000), Display(Name = "Nội dung")]
    public string? Description { get; set; }
    [Required, StringLength(250), Display(Name = "Địa điểm")]
    public string Location { get; set; } = "";
    [Required, Display(Name = "Bắt đầu")]
    public DateTime? StartsAt { get; set; }
    [Required, Display(Name = "Kết thúc")]
    public DateTime? EndsAt { get; set; }
    [Required, Display(Name = "Hạn đăng ký")]
    public DateTime? RegistrationDeadline { get; set; }
    [Range(1, int.MaxValue), Display(Name = "Số người tối đa")]
    public int Capacity { get; set; } = 30;
    [EnumDataType(typeof(EventAudience)), Display(Name = "Đối tượng")]
    public EventAudience Audience { get; set; } = EventAudience.School;
    public string? RowVersion { get; set; }
    [StringLength(500), Display(Name = "Lý do chỉnh sửa")]
    public string? Reason { get; set; }
    [BindNever, ValidateNever] public Club Club { get; set; } = null!;
    [BindNever] public int? Id { get; set; }
    [BindNever] public bool IsPublished { get; set; }
    public static EventForm From(ClubEvent item) => new()
    {
        Id = item.Id, Club = item.Club, Title = item.Title, Description = item.Description, Location = item.Location,
        StartsAt = item.StartsAt.ToOffset(TimeSpan.FromHours(7)).DateTime,
        EndsAt = item.EndsAt.ToOffset(TimeSpan.FromHours(7)).DateTime,
        RegistrationDeadline = item.RegistrationDeadline.ToOffset(TimeSpan.FromHours(7)).DateTime,
        Capacity = item.Capacity, Audience = item.Audience, RowVersion = Convert.ToBase64String(item.RowVersion), IsPublished = item.Status == EventStatus.Published
    };
}

public class EventActionForm
{
    [Required] public string RowVersion { get; set; } = "";
    [Required] public string Operation { get; set; } = "";
    [StringLength(500), Display(Name = "Lý do hủy")]
    public string? Reason { get; set; }
}
public record EventList(Club Club, IReadOnlyList<ClubEvent> Items, string? Search, EventStatus? Status, int Page, int Pages, int Count);
public record EventHistory(string Action, string Actor, string? Reason, DateTimeOffset At);
public record EventNoticeLink(int Id, string Title, DateTimeOffset PublishedAt, int Recipients);
public record EventDetails(ClubEvent Item, bool CanManage, IReadOnlyList<EventHistory> History, RegistrationPanel Registration, IReadOnlyList<EventNoticeLink> Notices);
