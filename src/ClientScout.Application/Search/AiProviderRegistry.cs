using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ClientScout.Application.Search;

public sealed class AiProviderRegistry : IAiProviderRegistry
{
    private readonly IConfiguration _configuration;
    private readonly AiProviderPoolOptions _options;

    public AiProviderRegistry(IConfiguration configuration, IOptions<AiProviderPoolOptions> options)
    {
        _configuration = configuration;
        _options = options.Value;
    }

    public IEnumerable<AiProviderModelCandidate> GetCandidates(AiTaskKind taskKind, int promptChars)
    {
        return GetProviders(taskKind)
            .SelectMany(provider => provider.Models
                .Where(model => IsModelUsableForTask(model, taskKind) && promptChars <= model.MaxInputChars)
                .Select(model => new AiProviderModelCandidate(provider, model)))
            .OrderBy(candidate => candidate.Model.Priority)
            .ThenBy(candidate => candidate.Provider.Priority);
    }

    public IReadOnlyList<AiProviderOptions> GetProviders(AiTaskKind taskKind)
    {
        return BuildProviderList()
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

        AddBluesMindsProvider(providers);
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

    private void AddBluesMindsProvider(List<AiProviderOptions> providers)
    {
        if (providers.Any(provider => provider.Name.Equals("BluesMinds", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var apiKey = _configuration["AI:BluesMinds:ApiKey"]
            ?? _configuration["BluesMinds:ApiKey"]
            ?? Environment.GetEnvironmentVariable("BLUESMINDS_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        providers.Add(NormalizeProvider(new AiProviderOptions
        {
            Name = "BluesMinds",
            BaseUrl = "https://api.bluesminds.com/v1",
            ApiKey = apiKey,
            Priority = 5,
            MaxRequestsPerMinute = 30,
            MaxTokensPerMinute = 90000,
            MaxConcurrency = 3,
            Models =
            [
                new AiModelOptions { Id = "gpt-4o-mini", Priority = 1, MaxInputChars = 60000 },
                new AiModelOptions { Id = "mimo-v2.5", Priority = 100, MaxInputChars = 60000 }
            ]
        }));
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
                MaxInputChars = 60000
            }).ToList()
        }));
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

    private static string[]? ReadCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
