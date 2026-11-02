namespace StudentClubManagement.Web.Models;

// Mỗi bản ghi là một đợt tham gia; tái gia nhập tạo bản ghi mới.
public class Membership
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public ClubRole Role { get; set; } = ClubRole.Member;
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public enum ClubRole
{
    Member = 1,
    Manager = 2
}

public enum MembershipStatus
{
    Active = 1,
    Suspended = 2,
    Left = 3
}
