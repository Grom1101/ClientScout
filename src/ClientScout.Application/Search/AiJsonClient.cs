using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ClientScout.Application.Search;

public enum AiFailureKind
{
    None,
    MissingProvider,
    RateLimited,
    RequestFailed,
    InvalidResponse
}

public sealed class AiJsonClient
{
    private static readonly ConcurrentDictionary<string, AiProviderRuntimeState> RuntimeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly AiProviderPoolOptions _options;
    private readonly ILogger<AiJsonClient> _logger;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

    public AiJsonClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IOptions<AiProviderPoolOptions> options,
        ILogger<AiJsonClient> logger,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _options = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public bool IsAvailable => GetProviders(AiTaskKind.LeadClassification).Any();
    public AiFailureKind LastFailureKind { get; private set; } = AiFailureKind.None;
    private int LastFailureCooldownSeconds { get; set; }

    public async Task<T?> GenerateJsonAsync<T>(
        string prompt,
        AiTaskKind taskKind,
        CancellationToken cancellationToken = default)
    {
        LastFailureKind = AiFailureKind.None;
        LastFailureCooldownSeconds = 0;
        var estimatedTokens = EstimateTokens(prompt);

        foreach (var candidate in GetProviderModelCandidates(taskKind, prompt.Length))
        {
            var runtime = RuntimeStates.GetOrAdd(candidate.RuntimeKey, _ => new AiProviderRuntimeState(candidate.Provider.MaxConcurrency));
            if (runtime.IsCoolingDown)
            {
                continue;
            }

            if (!runtime.TryReserve(candidate.Provider, estimatedTokens))
            {
                LastFailureKind = AiFailureKind.RateLimited;
                continue;
            }

            if (!await runtime.TryEnterAsync(cancellationToken))
            {
                LastFailureKind = AiFailureKind.RateLimited;
                continue;
            }

            try
            {
                var result = await TryGenerateJsonAsync<T>(candidate, prompt, cancellationToken);
                if (result != null)
                {
                    LastFailureKind = AiFailureKind.None;
                    return result;
                }

                if (LastFailureKind == AiFailureKind.RateLimited || LastFailureKind == AiFailureKind.RequestFailed)
                {
                    runtime.CoolDown(LastFailureCooldownSeconds > 0
                        ? LastFailureCooldownSeconds
                        : candidate.Provider.CooldownSeconds);
                }
            }
            finally
            {
                runtime.Exit();
            }
        }

        if (LastFailureKind == AiFailureKind.None)
        {
            LastFailureKind = AiFailureKind.MissingProvider;
        }

        return default;
    }

    private async Task<T?> TryGenerateJsonAsync<T>(
        AiProviderModelCandidate candidate,
        string prompt,
        CancellationToken cancellationToken)
    {
        var url = $"{candidate.Provider.BaseUrl.TrimEnd('/')}/chat/completions";
        var useResponseFormat = candidate.Model.UseResponseFormat && candidate.Provider.UseResponseFormat;
        var payload = new Dictionary<string, object?>
        {
            ["model"] = candidate.Model.Id,
            ["messages"] = new[]
            {
                new { role = "user", content = prompt }
            },
            ["temperature"] = 0.15
        };

        if (useResponseFormat)
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
                LastFailureKind = IsRateLimitOrCapacityFailure(response.StatusCode, body)
                    ? AiFailureKind.RateLimited
                    : AiFailureKind.RequestFailed;
                LastFailureCooldownSeconds = GetFailureCooldownSeconds(response.StatusCode, body, candidate.Provider);
                _logger.LogWarning(
                    "AI provider {Provider}/{Model} failed with {StatusCode}: {Body}",
                    candidate.Provider.Name,
                    candidate.Model.Id,
                    response.StatusCode,
                    Truncate(body, 800));
                
                await LogUsageAsync(candidate, (int)response.StatusCode, body, Truncate(body, 800), cancellationToken);
                return default;
            }

            await LogUsageAsync(candidate, (int)response.StatusCode, body, null, cancellationToken);

            using var document = JsonDocument.Parse(body);
            var text = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                LastFailureKind = AiFailureKind.InvalidResponse;
                return default;
            }

            var normalized = ExtractJson(text);
            return JsonSerializer.Deserialize<T>(normalized, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastFailureKind = AiFailureKind.InvalidResponse;
            LastFailureCooldownSeconds = candidate.Provider.CooldownSeconds;
            _logger.LogWarning(ex, "AI JSON generation failed for provider {Provider}/{Model}", candidate.Provider.Name, candidate.Model.Id);
            await LogUsageAsync(candidate, 0, string.Empty, ex.Message, cancellationToken);
            return default;
        }
    }

    private async Task LogUsageAsync(AiProviderModelCandidate candidate, int statusCode, string body, string? errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            int inputTokens = 0;
            int outputTokens = 0;
            decimal costUsd = 0m;

            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    using var document = JsonDocument.Parse(body);
                    if (document.RootElement.TryGetProperty("usage", out var usageElem))
                    {
                        if (usageElem.TryGetProperty("prompt_tokens", out var pTokens))
                            inputTokens = pTokens.GetInt32();
                        if (usageElem.TryGetProperty("completion_tokens", out var cTokens))
                            outputTokens = cTokens.GetInt32();
                    }
                }
                catch
                {
                    // Ignore parse errors for logging
                }
            }

            if (candidate.Provider.Name == "BluesMinds")
            {
                if (candidate.Model.Id == "gpt-4o-mini")
                {
                    costUsd = (inputTokens / 1_000_000m * 0.30m) + (outputTokens / 1_000_000m * 0.18m);
                }
                else if (candidate.Model.Id == "mimo-v2.5")
                {
                    costUsd = (inputTokens / 1_000_000m * 0.10m) + (outputTokens / 1_000_000m * 0.28m);
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ClientScout.Application.Common.Interfaces.IAppDbContext>();
            
            var log = new ClientScout.Domain.Entities.AiUsageLog
            {
                ProviderName = candidate.Provider.Name,
                ModelName = candidate.Model.Id,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CostUsd = costUsd,
                StatusCode = statusCode,
                ErrorMessage = errorMessage
            };

            dbContext.AiUsageLogs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI usage log for {Provider}/{Model}", candidate.Provider.Name, candidate.Model.Id);
        }
    }

    private IEnumerable<AiProviderModelCandidate> GetProviderModelCandidates(AiTaskKind taskKind, int promptChars)
    {
        foreach (var provider in GetProviders(taskKind))
        {
            foreach (var model in provider.Models
                         .Where(model => IsModelUsableForTask(model, taskKind) && promptChars <= model.MaxInputChars)
                         .OrderBy(model => model.Priority))
            {
                yield return new AiProviderModelCandidate(provider, model);
            }
        }
    }

    private IReadOnlyList<AiProviderOptions> GetProviders(AiTaskKind taskKind)
    {
        var providers = BuildProviderList();
        return providers
            .Where(provider => provider.Enabled)
            .Where(provider => !string.IsNullOrWhiteSpace(provider.ApiKey))
            .Where(provider => !string.IsNullOrWhiteSpace(provider.BaseUrl))
            .Where(provider => provider.Models.Any())
            .Where(provider => IsProviderUsableForTask(provider, taskKind))
            .OrderBy(provider => provider.Priority)
            .ToArray();
    }

    private List<AiProviderOptions> BuildProviderList()
    {
        var providers = _options.Providers
            .Select(NormalizeProvider)
            .Where(provider => !string.IsNullOrWhiteSpace(provider.Name))
            .ToList();

        var bluesMindsKey = Environment.GetEnvironmentVariable("BLUESMINDS_API_KEY") 
            ?? "sk-NrYmjNQFIzqcMi0AvDod7DIV0adwh5cQeTGADjFN96fckKfA";
        
        providers.Add(NormalizeProvider(new AiProviderOptions
        {
            Name = "BluesMinds",
            BaseUrl = "https://api.bluesminds.com/v1",
            ApiKey = bluesMindsKey,
            Priority = 5,
            MaxRequestsPerMinute = 30,
            MaxTokensPerMinute = 90000,
            MaxConcurrency = 3,
            Models = new[] { "gpt-4o-mini", "mimo-v2.5" }.Select((m, i) => new AiModelOptions { Id = m, Priority = i }).ToList()
        }));

        AddEnvProvider(providers, "Groq", "GROQ_API_KEY", "https://api.groq.com/openai/v1",
            ["llama-3.1-8b-instant", "llama-3.3-70b-versatile", "mixtral-8x7b-32768"], 10, 20, 45000, 2);
        AddEnvProvider(providers, "OpenRouter", "OPENROUTER_API_KEY", "https://openrouter.ai/api/v1",
            ["qwen/qwen-2.5-72b-instruct:free", "meta-llama/llama-3.1-8b-instruct:free", "google/gemini-2.0-flash-lite-preview-02-05:free", "microsoft/phi-3-mini-128k-instruct:free", "openrouter/auto"], 20, 20, 50000, 2);
        AddEnvProvider(providers, "DeepSeek", "DEEPSEEK_API_KEY", "https://api.deepseek.com",
            ["deepseek-v4-flash", "deepseek-chat"], 30, 30, 90000, 3);
        AddEnvProvider(providers, "ZAI", "ZAI_API_KEY", "https://api.z.ai/api/paas/v4",
            ["glm-4.7-flash", "glm-4.5-flash"], 40, 30, 90000, 3);
        AddEnvProvider(providers, "Kimi", "KIMI_API_KEY", "https://api.moonshot.ai/v1",
            ["kimi-k2.7-code-highspeed", "kimi-k2.6", "moonshot-v1-8k"], 50, 20, 60000, 2);
        AddEnvProvider(providers, "Gemini", "GEMINI_API_KEY", "https://generativelanguage.googleapis.com/v1beta/openai",
            ["gemini-2.5-flash-lite", "gemini-1.5-flash"], 60, 10, 35000, 1);
        AddEnvProvider(providers, "Qwen", "DASHSCOPE_API_KEY", "https://dashscope-intl.aliyuncs.com/compatible-mode/v1",
            ["qwen-flash", "qwen-plus-latest", "qwen-turbo-latest"], 70, 20, 50000, 2);
        AddEnvProvider(providers, "Qwen", "QWEN_API_KEY", "https://dashscope-intl.aliyuncs.com/compatible-mode/v1",
            ["qwen-flash", "qwen-plus-latest", "qwen-turbo-latest"], 70, 20, 50000, 2);

        AddLegacySingleProvider(providers);
        AddLegacyGeminiProvider(providers);

        return providers
            .GroupBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static AiProviderOptions NormalizeProvider(AiProviderOptions provider)
    {
        provider.Models = provider.Models
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .Select(model =>
            {
                model.MaxInputChars = model.MaxInputChars <= 0 ? 12000 : model.MaxInputChars;
                return model;
            })
            .ToList();
        provider.MaxConcurrency = Math.Max(1, provider.MaxConcurrency);
        provider.MaxRequestsPerMinute = Math.Max(1, provider.MaxRequestsPerMinute);
        provider.MaxTokensPerMinute = Math.Max(1000, provider.MaxTokensPerMinute);
        provider.CooldownSeconds = Math.Max(10, provider.CooldownSeconds);
        return provider;
    }

    private void AddLegacySingleProvider(List<AiProviderOptions> providers)
    {
        if (providers.Count > 0 || string.IsNullOrWhiteSpace(_configuration["AI:ApiKey"]))
        {
            return;
        }

        providers.Add(NormalizeProvider(new AiProviderOptions
        {
            Name = _configuration["AI:Provider"] ?? "LegacyAI",
            BaseUrl = _configuration["AI:BaseUrl"] ?? "https://api.groq.com/openai/v1",
            ApiKey = _configuration["AI:ApiKey"],
            Priority = 90,
            MaxRequestsPerMinute = 6,
            MaxTokensPerMinute = 20000,
            MaxConcurrency = 1,
            Models = ReadLegacyModels()
                .Select((model, index) => new AiModelOptions { Id = model, Priority = index + 1 })
                .ToList()
        }));
    }

    private void AddLegacyGeminiProvider(List<AiProviderOptions> providers)
    {
        var apiKey = _configuration["Gemini:ApiKey"] ?? _configuration["GeminiApiKey"];
        if (providers.Any(provider => provider.Name.Equals("Gemini", StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        providers.Add(NormalizeProvider(new AiProviderOptions
        {
            Name = "Gemini",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai",
            ApiKey = apiKey,
            Priority = 60,
            MaxRequestsPerMinute = 10,
            MaxTokensPerMinute = 35000,
            MaxConcurrency = 1,
            Models =
            [
                new AiModelOptions { Id = _configuration["Gemini:Model"] ?? "gemini-2.5-flash-lite" }
            ]
        }));
    }

    private string[] ReadLegacyModels()
    {
        var models = new List<string>();
        var primary = _configuration["AI:Model"];
        if (!string.IsNullOrWhiteSpace(primary))
        {
            models.Add(primary);
        }

        var fallbacks = _configuration.GetSection("AI:ModelFallbacks").Get<string[]>()
            ?? ReadCsv(Environment.GetEnvironmentVariable("AI_MODEL_FALLBACKS"))
            ?? [];
        models.AddRange(fallbacks);
        return models.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddEnvProvider(
        List<AiProviderOptions> providers,
        string name,
        string envKey,
        string baseUrl,
        string[] models,
        int priority,
        int rpm,
        int tpm,
        int concurrency)
    {
        if (providers.Any(provider => provider.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        providers.Add(NormalizeProvider(new AiProviderOptions
        {
            Name = name,
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Priority = priority,
            MaxRequestsPerMinute = rpm,
            MaxTokensPerMinute = tpm,
            MaxConcurrency = concurrency,
            Models = models.Select((model, index) => new AiModelOptions
            {
                Id = model,
                Priority = index + 1,
                MaxInputChars = 16000
            }).ToList()
        }));
    }

    private static bool IsProviderUsableForTask(AiProviderOptions provider, AiTaskKind taskKind)
    {
        return provider.Tasks.Length == 0 ||
               provider.Tasks.Any(task => task.Equals(taskKind.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsModelUsableForTask(AiModelOptions model, AiTaskKind taskKind)
    {
        return model.Tasks.Length == 0 ||
               model.Tasks.Any(task => task.Equals(taskKind.ToString(), StringComparison.OrdinalIgnoreCase));
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

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

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

    private static string[]? ReadCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string Truncate(string value, int max)
    {
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    private sealed record AiProviderModelCandidate(AiProviderOptions Provider, AiModelOptions Model)
    {
        public string RuntimeKey => $"{Provider.Name}:{Model.Id}";
    }

    private sealed class AiProviderRuntimeState
    {
        private readonly object _gate = new();
        private readonly Queue<DateTimeOffset> _requestWindow = new();
        private readonly Queue<(DateTimeOffset At, int Tokens)> _tokenWindow = new();
        private readonly SemaphoreSlim _concurrency;
        private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;

        public AiProviderRuntimeState(int maxConcurrency)
        {
            _concurrency = new SemaphoreSlim(Math.Max(1, maxConcurrency), Math.Max(1, maxConcurrency));
        }

        public bool IsCoolingDown => _cooldownUntil > DateTimeOffset.UtcNow;

        public bool TryReserve(AiProviderOptions provider, int estimatedTokens)
        {
            lock (_gate)
            {
                var now = DateTimeOffset.UtcNow;
                Trim(now);

                if (_cooldownUntil > now)
                {
                    return false;
                }

                var currentTokens = _tokenWindow.Sum(item => item.Tokens);
                if (_requestWindow.Count >= provider.MaxRequestsPerMinute ||
                    currentTokens + estimatedTokens > provider.MaxTokensPerMinute)
                {
                    return false;
                }

                _requestWindow.Enqueue(now);
                _tokenWindow.Enqueue((now, estimatedTokens));
                return true;
            }
        }

        public Task<bool> TryEnterAsync(CancellationToken cancellationToken)
        {
            return _concurrency.WaitAsync(0, cancellationToken);
        }

        public void Exit()
        {
            _concurrency.Release();
        }

        public void CoolDown(int seconds)
        {
            lock (_gate)
            {
                _cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(seconds);
            }
        }

        private void Trim(DateTimeOffset now)
        {
            var cutoff = now.AddMinutes(-1);
            while (_requestWindow.TryPeek(out var requestAt) && requestAt < cutoff)
            {
                _requestWindow.Dequeue();
            }

            while (_tokenWindow.TryPeek(out var tokenItem) && tokenItem.At < cutoff)
            {
                _tokenWindow.Dequeue();
            }
        }
    }
}
