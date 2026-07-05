namespace ClientScout.Application.Search;

public enum AiTaskKind
{
    LeadClassification,
    ProfileExpansion,
    DisputedLeadReview
}

public class AiProviderPoolOptions
{
    public string Mode { get; set; } = "free-first";
    public List<AiProviderOptions> Providers { get; set; } = [];
}

public class AiProviderOptions
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public int MaxRequestsPerMinute { get; set; } = 30;
    public int MaxTokensPerMinute { get; set; } = 60000;
    public int MaxConcurrency { get; set; } = 1;
    public int CooldownSeconds { get; set; } = 120;
    public bool UseResponseFormat { get; set; } = true;
    public string[] Tasks { get; set; } = [];
    public List<AiModelOptions> Models { get; set; } = [];
}

public class AiModelOptions
{
    public string Id { get; set; } = string.Empty;
    public int Priority { get; set; } = 100;
    public int MaxInputChars { get; set; } = 12000;
    public bool UseResponseFormat { get; set; } = true;
    public string[] Tasks { get; set; } = [];
}
