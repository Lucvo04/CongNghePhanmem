-- Truy vấn chỉ đọc để trình bày cấu trúc database rút gọn.
USE [StudentClubManagement];
SET NOCOUNT ON;

SELECT 'Users' AS TableName, COUNT(*) AS Total FROM Users
UNION ALL SELECT 'Roles',COUNT(*) FROM Roles
UNION ALL SELECT 'ClubProposals',COUNT(*) FROM ClubProposals
UNION ALL SELECT 'Clubs',COUNT(*) FROM Clubs
UNION ALL SELECT 'Memberships',COUNT(*) FROM Memberships
UNION ALL SELECT 'JoinRequests',COUNT(*) FROM JoinRequests
UNION ALL SELECT 'Events',COUNT(*) FROM Events
UNION ALL SELECT 'EventRegistrations',COUNT(*) FROM EventRegistrations
UNION ALL SELECT 'Attendances',COUNT(*) FROM Attendances
UNION ALL SELECT 'Announcements',COUNT(*) FROM Announcements
UNION ALL SELECT 'AnnouncementRecipients',COUNT(*) FROM AnnouncementRecipients
UNION ALL SELECT 'AuditLogs',COUNT(*) FROM AuditLogs;

-- Đếm người nhận tại thời điểm đăng, không đếm membership hiện tại.
SELECT a.Id,a.Title,a.Status,a.PublishedAt,COUNT(r.UserId) AS RecipientCount,
       COUNT(r.ReadAt) AS ReadCount,COUNT(r.UserId)-COUNT(r.ReadAt) AS UnreadCount
FROM Announcements a
LEFT JOIN AnnouncementRecipients r ON r.AnnouncementId=a.Id
GROUP BY a.Id,a.Title,a.Status,a.PublishedAt;

-- PB18: thông báo tự động; người nhận vẫn giữ nguyên sau khi hủy đăng ký.
SELECT a.Id,a.EventId,a.Title,a.PublishedAt,COUNT(r.UserId) AS RecipientCount,
       COUNT(r.ReadAt) AS ReadCount
FROM Announcements a
LEFT JOIN AnnouncementRecipients r ON r.AnnouncementId=a.Id
WHERE a.SourceKey IS NOT NULL
GROUP BY a.Id,a.EventId,a.Title,a.PublishedAt
ORDER BY a.PublishedAt DESC,a.Id DESC;

-- Số người cố định trong danh sách đã chốt; không đếm thành viên CLB hiện tại.
SELECT e.Id,e.Title,e.AttendanceFinalizedAt,COUNT(a.RegistrationId) AS FinalRosterCount
FROM Events e
LEFT JOIN EventRegistrations r ON r.EventId=e.Id
LEFT JOIN Attendances a ON a.RegistrationId=r.Id AND a.IsInFinalRoster=1
WHERE e.AttendanceFinalizedAt IS NOT NULL
GROUP BY e.Id,e.Title,e.AttendanceFinalizedAt;

-- Quyền toàn hệ thống: Users.RoleId -> Roles.Id -> tên quyền.
SELECT u.FullName,u.StudentCode,r.Name AS SystemRole
FROM Users AS u
JOIN Roles AS r ON r.Id=u.RoleId
ORDER BY u.FullName,r.Name;

-- Quyền trong từng CLB: Manager = 2, Active = 1, chưa kết thúc.
SELECT c.Code,c.Name AS ClubName,u.FullName AS ManagerName
FROM Clubs AS c
JOIN Memberships AS m ON m.ClubId=c.Id
JOIN Users AS u ON u.Id=m.UserId
WHERE m.Role=2 AND m.Status=1 AND m.EndedAt IS NULL
ORDER BY c.Name;

-- Kết quả đơn gia nhập: chỉ đếm, không lộ nội dung lý do cá nhân.
SELECT c.Code,c.Name AS ClubName,
 SUM(CASE WHEN j.Status=1 THEN 1 ELSE 0 END) AS Pending,
 SUM(CASE WHEN j.Status=2 THEN 1 ELSE 0 END) AS Withdrawn,
 SUM(CASE WHEN j.Status=3 THEN 1 ELSE 0 END) AS Approved,
 SUM(CASE WHEN j.Status=4 THEN 1 ELSE 0 END) AS Rejected
FROM Clubs AS c LEFT JOIN JoinRequests AS j ON j.ClubId=c.Id
GROUP BY c.Id,c.Code,c.Name ORDER BY c.Code;
