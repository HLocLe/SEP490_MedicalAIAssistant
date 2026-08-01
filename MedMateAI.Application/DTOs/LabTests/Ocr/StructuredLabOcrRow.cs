using System.Text.Json.Serialization;

namespace MedMateAI.Application.DTOs.LabTests.Ocr;

public sealed class StructuredLabOcrRow
{
    [JsonPropertyName("ten_xet_nghiem")]
    public string? TenXetNghiem { get; set; }

    [JsonPropertyName("ket_qua")]
    [JsonConverter(typeof(LenientStringJsonConverter))]
    public string? KetQua { get; set; }

    [JsonPropertyName("tri_so_binh_thuong")]
    public string? TriSoBinhThuong { get; set; }
}
