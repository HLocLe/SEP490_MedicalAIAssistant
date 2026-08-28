namespace MedMateAI.Application.DTOs.PushDevices;

public sealed class RegisterPushDeviceRequest
{
    public string InstallationId { get; set; } = string.Empty;

    public string ExpoPushToken { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string? AppVersion { get; set; }
}

public sealed class PushDeviceResponse
{
    public Guid Id { get; set; }

    public string InstallationId { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime LastSeenAt { get; set; }
}
