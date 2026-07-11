using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MedMateAI.Infrastructure.AI.Helpers;

internal static class AiHttpClientHelper
{
    public static void SetJsonContent(HttpRequestMessage request, object payload)
    {
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
    }

    public static async Task<string> SendJsonAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        ILogger logger,
        string serviceName,
        CancellationToken cancellationToken)
    {
        using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var truncatedBody = Truncate(responseBody, 500);
            logger.LogWarning(
                "{ServiceName} request failed with status code {StatusCode}. Response: {ResponseBody}",
                serviceName,
                (int)httpResponse.StatusCode,
                truncatedBody);

            throw new InvalidOperationException(
                $"{serviceName} request failed with status code {(int)httpResponse.StatusCode}. Response: {truncatedBody}");
        }

        return responseBody;
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
