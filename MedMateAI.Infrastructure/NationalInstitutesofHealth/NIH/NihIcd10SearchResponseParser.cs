using System.Text.Json;

namespace MedMateAI.Infrastructure.NationalInstitutesofHealth.NIH;

internal static class NihIcd10SearchResponseParser
{
    public static string? ParseFirstCode(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var root = JsonSerializer.Deserialize<JsonElement>(json, options);
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 2)
        {
            return null;
        }

        var codes = JsonSerializer.Deserialize<List<string>>(root[1].GetRawText(), options);
        var firstCode = codes?.FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));

        return string.IsNullOrWhiteSpace(firstCode) ? null : firstCode.Trim();
    }
}
