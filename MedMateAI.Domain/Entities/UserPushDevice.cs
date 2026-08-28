namespace MedMateAI.Domain.Entities;

public sealed class UserPushDevice : BaseEntity
{
    public Guid UserId { get; set; }

    public string InstallationId { get; set; } = string.Empty;

    public string ExpoPushToken { get; set; } = string.Empty;

    public int TokenVersion { get; set; } = 1;

    public string Platform { get; set; } = string.Empty;

    public string? AppVersion { get; set; }

    public bool IsActive { get; set; }

    public DateTime LastSeenAt { get; set; }

    public ICollection<Notification> Notifications { get; set; } =
        new List<Notification>();
}
