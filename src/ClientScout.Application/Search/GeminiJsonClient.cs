using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClientScout.Application.Search;

public enum GeminiFailureKind
{
    None,
    MissingApiKey,
    QuotaExceeded,
    RequestFailed,
    InvalidResponse
}

public class GeminiJsonClient
{
    private static readonly TimeSpan ModelCooldown = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(22);
    private static readonly SemaphoreSlim AiRequestGate = new(1, 1);
    private static DateTimeOffset LastRequestStartedAt = DateTimeOffset.MinValue;
    private static readonly ConcurrentDictionary<string, DateTimeOffset> OpenRouterModelCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiJsonClient> _logger;

    public GeminiJsonClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        IConfiguration configuration,
        ILogger<GeminiJsonClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _options.Provider = configuration["AI:Provider"]
            ?? configuration["Gemini:Provider"]
            ?? Environment.GetEnvironmentVariable("AI_PROVIDER")
            ?? _options.Provider;
        _options.ApiKey ??= configuration["AI:ApiKey"]
            ?? configuration["GeminiApiKey"]
            ?? configuration["Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("AI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GeminiApiKey")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        _options.Model = configuration["AI:Model"]
            ?? configuration["Gemini:Model"]
            ?? Environment.GetEnvironmentVariable("AI_MODEL")
            ?? Environment.GetEnvironmentVariable("GEMINI_MODEL")
            ?? _options.Model;
        _options.ModelFallbacks = configuration.GetSection("AI:ModelFallbacks").Get<string[]>()
            ?? ReadCsv(Environment.GetEnvironmentVariable("AI_MODEL_FALLBACKS"))
            ?? _options.ModelFallbacks;
        _options.BaseUrl = configuration["AI:BaseUrl"]
            ?? configuration["Gemini:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("AI_BASE_URL")
            ?? _options.BaseUrl;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.ApiKey);
    public GeminiFailureKind LastFailureKind { get; private set; } = GeminiFailureKind.None;

    public async Task<T?> GenerateJsonAsync<T>(string prompt, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            LastFailureKind = GeminiFailureKind.MissingApiKey;
            return default;
        }

        LastFailureKind = GeminiFailureKind.None;
        if (IsOpenAiCompatibleProvider(_options.Provider))
        {
            return await GenerateOpenRouterJsonAsync<T>(prompt, cancellationToken);
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey!)}";
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        try
        {
            await WaitForAiRequestSlotAsync(cancellationToken);
            using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LastFailureKind = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                                  body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                    ? GeminiFailureKind.QuotaExceeded
                    : GeminiFailureKind.RequestFailed;
                _logger.LogWarning("Gemini request failed with {StatusCode}: {Body}", response.StatusCode, body);
                return default;
            }

            using var document = JsonDocument.Parse(body);
            var text = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                LastFailureKind = GeminiFailureKind.InvalidResponse;
                return default;
            }

            return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            LastFailureKind = GeminiFailureKind.InvalidResponse;
            _logger.LogWarning(ex, "Gemini JSON generation failed");
            return default;
        }
    }

    private async Task<T?> GenerateOpenRouterJsonAsync<T>(string prompt, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://openrouter.ai/api/v1"
            : _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/chat/completions";

        foreach (var model in GetOpenRouterModelsToTry())
        {
            if (IsModelCoolingDown(model))
            {
                _logger.LogInformation("Skipping OpenRouter model {Model} because it is cooling down after a recent rate-limit failure.", model);
                continue;
            }

            var result = await TryGenerateOpenRouterJsonAsync<T>(url, model, prompt, cancellationToken);
            if (result != null)
            {
                return result;
            }

            if (LastFailureKind == GeminiFailureKind.QuotaExceeded)
            {
                return default;
            }

            if (LastFailureKind != GeminiFailureKind.QuotaExceeded &&
                LastFailureKind != GeminiFailureKind.RequestFailed &&
                LastFailureKind != GeminiFailureKind.InvalidResponse)
            {
                return default;
            }
        }

        return default;
    }

    private static bool IsOpenAiCompatibleProvider(string? provider)
    {
        return string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "Groq", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<T?> TryGenerateOpenRouterJsonAsync<T>(string url, string model, string prompt, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://clientscout.local");
            request.Headers.TryAddWithoutValidation("X-Title", "ClientScout");

            await WaitForAiRequestSlotAsync(cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var isRateOrEndpointFailure = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                                                  response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                                  body.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
                                                  body.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                                                  body.Contains("temporarily", StringComparison.OrdinalIgnoreCase) ||
                                                  body.Contains("No endpoints found", StringComparison.OrdinalIgnoreCase);
                    LastFailureKind = isRateOrEndpointFailure
                        ? GeminiFailureKind.QuotaExceeded
                        : GeminiFailureKind.RequestFailed;
                    if (isRateOrEndpointFailure)
                    {
                        CoolDownModel(model);
                    }

                    _logger.LogWarning("OpenRouter request failed for model {Model} with {StatusCode}: {Body}", model, response.StatusCode, body);
                    return default;
            }

            using var document = JsonDocument.Parse(body);
            var text = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                LastFailureKind = GeminiFailureKind.InvalidResponse;
                _logger.LogWarning("OpenRouter returned empty content for model {Model}", model);
                return default;
            }

            var normalizedText = ExtractJson(text);
            var result = JsonSerializer.Deserialize<T>(normalizedText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            LastFailureKind = GeminiFailureKind.None;
            return result;
        }
        catch (Exception ex)
        {
            LastFailureKind = GeminiFailureKind.InvalidResponse;
            _logger.LogWarning(ex, "OpenRouter JSON generation failed for model {Model}", model);
            return default;
        }
    }

    private static bool IsModelCoolingDown(string model)
    {
        if (!OpenRouterModelCooldowns.TryGetValue(model, out var until))
        {
            return false;
        }

        if (until > DateTimeOffset.UtcNow)
        {
            return true;
        }

        OpenRouterModelCooldowns.TryRemove(model, out _);
        return false;
    }

    private static void CoolDownModel(string model)
    {
        OpenRouterModelCooldowns[model] = DateTimeOffset.UtcNow.Add(ModelCooldown);
    }

    private IEnumerable<string> GetOpenRouterModelsToTry()
    {
        if (!string.IsNullOrWhiteSpace(_options.Model))
        {
            yield return _options.Model.Trim();
        }

        foreach (var model in _options.ModelFallbacks)
        {
            if (!string.IsNullOrWhiteSpace(model) &&
                !string.Equals(model.Trim(), _options.Model, StringComparison.OrdinalIgnoreCase))
            {
                yield return model.Trim();
            }
        }
    }

    private static string[]? ReadCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
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

        return text;
    }

    private static async Task WaitForAiRequestSlotAsync(CancellationToken cancellationToken)
    {
        await AiRequestGate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var nextAllowedAt = LastRequestStartedAt + MinRequestInterval;
            if (nextAllowedAt > now)
            {
                await Task.Delay(nextAllowedAt - now, cancellationToken);
            }

            LastRequestStartedAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            AiRequestGate.Release();
        }
    }
}
