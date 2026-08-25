namespace MedMateAI.Application.Common.Validation;

public static class DateOfBirthValidationPolicy
{
    public const string RequiredDateError = "Date of birth is required.";
    public const string FutureDateError = "Date of birth cannot be in the future.";
    public const string MinimumDateError =
        "Date of birth must be on or after January 1, 1890.";
    public const string MinimumAgeError = "User must be at least 16 years old.";

    private static readonly DateOnly MinimumDate = new(1890, 1, 1);

    public static DateOfBirthValidationResult ValidateForRegistration(
        DateOnly? dateOfBirth,
        DateOnly today)
    {
        return ValidateRequired(dateOfBirth, today);
    }

    public static DateOfBirthValidationResult ValidateRequired(
        DateOnly? dateOfBirth,
        DateOnly today)
    {
        if (!dateOfBirth.HasValue)
        {
            return new DateOfBirthValidationResult(false, RequiredDateError);
        }

        return ValidateValue(dateOfBirth.Value, today);
    }

    public static DateOfBirthValidationResult ValidateForProfileUpdate(
        DateOnly? dateOfBirth,
        DateOnly today)
    {
        return dateOfBirth.HasValue
            ? ValidateValue(dateOfBirth.Value, today)
            : new DateOfBirthValidationResult(true, null);
    }

    private static DateOfBirthValidationResult ValidateValue(
        DateOnly dateOfBirth,
        DateOnly today)
    {
        if (dateOfBirth > today)
        {
            return new DateOfBirthValidationResult(false, FutureDateError);
        }

        if (dateOfBirth < MinimumDate)
        {
            return new DateOfBirthValidationResult(false, MinimumDateError);
        }

        if (dateOfBirth > today.AddYears(-16))
        {
            return new DateOfBirthValidationResult(false, MinimumAgeError);
        }

        return new DateOfBirthValidationResult(true, null);
    }
}
