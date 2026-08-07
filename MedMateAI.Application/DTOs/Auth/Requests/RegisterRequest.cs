using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.Auth.Requests;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string confirmPassword { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Otp { get; set; } = string.Empty;
}
