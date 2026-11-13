using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Announcements;

public class AnnouncementForm
{
    [BindNever, ValidateNever] public int? Id { get; set; }
    [BindNever, ValidateNever] public Club Club { get; set; } = null!;
    [BindNever, ValidateNever] public IReadOnlyList<AnnouncementEvent> Events { get; set; } = [];
    [Required(ErrorMessage = "Nhập tiêu đề."), StringLength(180), Display(Name = "Tiêu đề")]
    public string Title { get; set; } = "";
    [Required(ErrorMessage = "Nhập nội dung."), StringLength(4000), Display(Name = "Nội dung")]
    public string Content { get; set; } = "";
    [Display(Name = "Đối tượng nhận")]
    public int? EventId { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public class AnnouncementActionForm
{
    [Required, StringLength(64)] public string RowVersion { get; set; } = "";
    [Required] public string Operation { get; set; } = "";
    [StringLength(64)] public string? RecipientVersion { get; set; }
    [StringLength(500), Display(Name = "Lý do lưu trữ")] public string? Reason { get; set; }
}

public record AnnouncementEvent(int Id, string Title);
public record RecipientPreview(int Count, string Version);
public record AnnouncementHistory(string Action, string Actor, string? Reason, DateTimeOffset At);
public record AnnouncementPerson(string Name, string? StudentCode, DateTimeOffset? ReadAt);
public record AnnouncementList(Club Club, IReadOnlyList<Announcement> Items, string? Search,
    AnnouncementStatus? Status, int Page, int Pages, int Count);
public record AnnouncementDetails(Announcement Item, bool CanManage, RecipientPreview? Preview,
    int RecipientCount, IReadOnlyList<AnnouncementHistory> History, AnnouncementReceipt? Receipt, string? ReturnUrl, bool CanViewEvent);
public record AnnouncementRecipients(Announcement Item, IReadOnlyList<AnnouncementPerson> Items,
    int Page, int Pages, int Count, int ReadCount);

public record AnnouncementReceipt(DateTimeOffset? ReadAt);
public enum InboxStatus { All = 0, Unread = 1, Read = 2 }
public record InboxClub(int Id, string Name);
public record InboxItem(int Id, string Title, string ClubName, DateTimeOffset PublishedAt,
    DateTimeOffset? ReadAt, bool Archived);
public record AnnouncementInbox(IReadOnlyList<InboxItem> Items, IReadOnlyList<InboxClub> Clubs,
    string? Search, int? ClubId, InboxStatus Status, int Page, int Pages, int Count, int UnreadCount);
