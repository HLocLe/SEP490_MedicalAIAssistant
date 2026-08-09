namespace MedMateAI.Application.DTOs.Users.Requests;

public sealed class UpdateCurrentUserPhoneRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}
