namespace MedMateAI.Application.DTOs.Auth.Requests;

public sealed class SendRegisterOtpRequest
{
    public string Email { get; set; } = string.Empty;
}
