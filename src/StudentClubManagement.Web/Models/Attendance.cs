namespace StudentClubManagement.Web.Models;

// Dùng khóa đăng ký làm khóa chính: mỗi đăng ký chỉ có một kết quả điểm danh.
public class Attendance
{
    public int RegistrationId { get; set; }
    public EventRegistration Registration { get; set; } = null!;
    public AttendanceStatus Status { get; set; }
    public bool IsInFinalRoster { get; set; }
    public DateTimeOffset MarkedAt { get; set; }
    public string MarkedById { get; set; } = "";
    public ApplicationUser MarkedBy { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];
}

public enum AttendanceStatus { Unmarked = 0, Present = 1, Absent = 2, Excused = 3 }
public static class AttendanceLabels
{
    public static string Label(this AttendanceStatus status) => status switch
    {
        AttendanceStatus.Unmarked => "Chưa điểm danh", AttendanceStatus.Present => "Có mặt",
        AttendanceStatus.Absent => "Vắng", AttendanceStatus.Excused => "Có phép", _ => "Không hợp lệ"
    };
}
