namespace MedMateAI.Application.DTOs.LabTests.Ocr;

public sealed record ParsedOcrRow(
    string TestName,
    string? ReferenceText,
    double? Value);
