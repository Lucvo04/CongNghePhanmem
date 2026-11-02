namespace StudentClubManagement.Web.Models;

public class Club
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public ClubStatus Status { get; set; } = ClubStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<Membership> Memberships { get; set; } = [];
}

public enum ClubStatus
{
    Active = 1,
    Suspended = 2,
    Archived = 3
}
