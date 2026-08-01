using MedMateAI.Application.DTOs.LabTests.Ocr;

namespace MedMateAI.Application.IService;

public interface ILabTestOcrStructurer
{
    Task<IReadOnlyList<ParsedOcrRow>> StructureAsync(
        string rawOcrText,
        CancellationToken cancellationToken = default);
}
