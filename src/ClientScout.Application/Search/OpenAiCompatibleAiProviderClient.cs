using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClientScout.Application.Search;

public sealed class OpenAiCompatibleAiProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly IAiUsageLogger _usageLogger;
    private readonly ILogger<OpenAiCompatibleAiProviderClient> _logger;

    public OpenAiCompatibleAiProviderClient(
        HttpClient httpClient,
        IAiUsageLogger usageLogger,
        ILogger<OpenAiCompatibleAiProviderClient> logger)
    {
        _httpClient = httpClient;
        _usageLogger = usageLogger;
        _logger = logger;
    }

    public async Task<AiProviderJsonResult<T>> GenerateJsonAsync<T>(
        AiProviderModelCandidate candidate,
        string prompt,
        CancellationToken cancellationToken)
    {
        var url = $"{candidate.Provider.BaseUrl.TrimEnd('/')}/chat/completions";
        var payload = new Dictionary<string, object?>
        {
            ["model"] = candidate.Model.Id,
            ["messages"] = new[]
            {
                new { role = "user", content = prompt }
            },
            ["temperature"] = 0.15
        };

        if (candidate.Model.UseResponseFormat && candidate.Provider.UseResponseFormat)
        {
            payload["response_format"] = new { type = "json_object" };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", candidate.Provider.ApiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://clientscout.local");
        request.Headers.TryAddWithoutValidation("X-Title", "ClientScout");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failureKind = IsRateLimitOrCapacityFailure(response.StatusCode, body)
                    ? AiFailureKind.RateLimited
                    : AiFailureKind.RequestFailed;
                var cooldownSeconds = GetFailureCooldownSeconds(response.StatusCode, body, candidate.Provider);

                _logger.LogWarning(
                    "AI provider {Provider}/{Model} failed with {StatusCode}: {Body}",
                    candidate.Provider.Name,
                    candidate.Model.Id,
                    response.StatusCode,
                    Truncate(body, 800));

                await _usageLogger.LogAsync(candidate, (int)response.StatusCode, body, Truncate(body, 800), cancellationToken);
                return new AiProviderJsonResult<T>(default, failureKind, cooldownSeconds);
            }

            await _usageLogger.LogAsync(candidate, (int)response.StatusCode, body, null, cancellationToken);
            var content = ExtractAssistantContent(body);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AiProviderJsonResult<T>(default, AiFailureKind.InvalidResponse, 0);
            }

            var normalized = ExtractJson(content);
            var value = JsonSerializer.Deserialize<T>(normalized, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new AiProviderJsonResult<T>(value, value == null ? AiFailureKind.InvalidResponse : AiFailureKind.None, 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AI JSON generation failed for provider {Provider}/{Model}", candidate.Provider.Name, candidate.Model.Id);
            await _usageLogger.LogAsync(candidate, 0, string.Empty, ex.Message, cancellationToken);
            return new AiProviderJsonResult<T>(default, AiFailureKind.InvalidResponse, candidate.Provider.CooldownSeconds);
        }
    }

    private static string? ExtractAssistantContent(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private static bool IsRateLimitOrCapacityFailure(HttpStatusCode statusCode, string body)
    {
        return statusCode == HttpStatusCode.TooManyRequests ||
               statusCode == HttpStatusCode.ServiceUnavailable ||
               body.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("capacity", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("temporarily", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("insufficient", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetFailureCooldownSeconds(HttpStatusCode statusCode, string body, AiProviderOptions provider)
    {
        if (statusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden ||
            body.Contains("insufficient balance", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("suspended", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("Unpurchased", StringComparison.OrdinalIgnoreCase))
        {
            return 3600;
        }

        return provider.CooldownSeconds;
    }

    private static string ExtractJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = text.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                text = text[(firstLineEnd + 1)..].Trim();
            }

            if (text.EndsWith("```", StringComparison.Ordinal))
            {
                text = text[..^3].Trim();
            }
        }

        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return text[firstBrace..(lastBrace + 1)];
        }

        return text;
    }

    private static string Truncate(string value, int max)
    {
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
