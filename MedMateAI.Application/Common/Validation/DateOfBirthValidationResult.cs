namespace MedMateAI.Application.Common.Validation;

public readonly record struct DateOfBirthValidationResult(
    bool IsValid,
    string? ErrorMessage);
