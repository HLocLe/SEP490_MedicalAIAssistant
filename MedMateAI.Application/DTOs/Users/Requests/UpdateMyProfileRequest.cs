using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Users.Requests;

public sealed class UpdateMyProfileRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}
