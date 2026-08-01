using MedMateAI.Domain.Enums;

namespace MedMateAI.Application.DTOs.LabTests.Requests;

public sealed class LabTestAnalyzeRequest
{
    public string DocumentUrl { get; set; } = string.Empty;

    public Gender? PatientGenderAtTest { get; set; }

    public int? PatientAgeAtTest { get; set; }

    public DateOnly? TestDate { get; set; }
}
