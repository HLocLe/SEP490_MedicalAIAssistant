using System.Text.Json;
using MedMateAI.Application.DTOs.IcdLookup;
using MedMateAI.Application.IService;
using MedMateAI.Infrastructure.NationalInstitutesofHealth.NIH;
using MedMateAI.Infrastructure.NationalInstitutesofHealth.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.NationalInstitutesofHealth;

public sealed class NihIcdLookupService : IIcdLookupService
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly NihClinicalTablesOptions _options;
    private readonly ILogger<NihIcdLookupService> _logger;

    public NihIcdLookupService(
        HttpClient httpClient,
        IOptions<NihClinicalTablesOptions> options,
        ILogger<NihIcdLookupService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IcdLookupResult?> SearchFirstAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        var requestUri = BuildSearchUri(searchTerm.Trim());

        try
        {
            using var httpResponse = await _httpClient.GetAsync(requestUri, cancellationToken);
            var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "NIH ICD-10 lookup failed with status {StatusCode}. Response: {ResponseBody}",
                    (int)httpResponse.StatusCode,
                    Truncate(responseBody, 500));

                return null;
            }

            var firstCode = NihIcd10SearchResponseParser.ParseFirstCode(responseBody, ResponseJsonOptions);
            if (firstCode is null)
            {
                _logger.LogWarning(
                    "NIH ICD-10 lookup returned no codes for search term {SearchTerm}.",
                    searchTerm);

                return null;
            }

            return new IcdLookupResult
            {
                Icd10Code = firstCode,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "NIH ICD-10 lookup request failed for search term {SearchTerm}.", searchTerm);
            return null;
        }
    }

    private string BuildSearchUri(string searchTerm)
    {
        var baseUrl = _options.BaseUrl.Trim().TrimEnd('/');
        return $"{baseUrl}/search?terms={Uri.EscapeDataString(searchTerm)}&sf=code,name";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
