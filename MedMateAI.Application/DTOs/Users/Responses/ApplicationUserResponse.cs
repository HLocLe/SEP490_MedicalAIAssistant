using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Users.Responses;

public sealed class ApplicationUserResponse
{
    public Guid Id { get; set; }

    public string? UserName { get; set; }

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; }

    public UserStatus Status { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public bool IsFirstLogin { get; set; }

    public bool IsProfileCompleted { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
