using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using StudentClubManagement.Web.Models;

namespace StudentClubManagement.Web.ViewModels.Events;

public class AttendanceForm
{
    [Required] public string EventVersion { get; set; } = "";
    [Required] public string RegistrationVersion { get; set; } = "";
    public string? RowVersion { get; set; }
    [BindRequired, EnumDataType(typeof(AttendanceStatus))] public AttendanceStatus Status { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
}
public record AttendanceRow(int RegistrationId, string FullName, string? StudentCode, AttendanceStatus Status,
    DateTimeOffset? MarkedAt, string? MarkedBy, byte[] RegistrationVersion, byte[]? RowVersion, bool IsCancelled);
public record AttendanceList(ClubEvent Event, IReadOnlyList<AttendanceRow> Items, string? Search, AttendanceStatus? Status,
    int Count, int Page, int Pages, bool CanMark, string Message, AttendanceSummary Summary, bool CanFinalize);

public class FinalizeAttendanceForm
{
    [Required] public string EventVersion { get; set; } = "";
    [Required] public string RosterVersion { get; set; } = "";
    public bool MarkUnmarkedAbsent { get; set; }
}
public record AttendanceSummary(int Total, int Unmarked, int Present, int Absent, int Excused, string Version);
public record AttendanceHistoryRow(long Id, AttendanceStatus? Before, AttendanceStatus? After,
    string? Reason, DateTimeOffset At, string Actor, bool Corrected);
public record AttendanceHistory(int RegistrationId, int EventId, string Title, string ClubName, string FullName,
    string? StudentCode, EventStatus EventStatus, DateTimeOffset? FinalizedAt, AttendanceStatus? Status,
    bool InFinalRoster, bool CanManage, bool CanViewEvent, IReadOnlyList<AttendanceHistoryRow> Items,
    int Page, int Pages, int Count, string? ReturnUrl);
