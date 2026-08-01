using System.Text.Json.Serialization;

namespace MedMateAI.Application.DTOs.LabTests.Ocr;

public sealed class StructuredLabOcrDocument
{
    [JsonPropertyName("danh_sach_xet_nghiem")]
    public List<StructuredLabOcrRow>? DanhSachXetNghiem { get; set; }
}
