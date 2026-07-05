namespace ClientScout.Application.Search;

public class GeminiOptions
{
    public string Provider { get; set; } = "Gemini";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-2.5-flash-lite";
    public string[] ModelFallbacks { get; set; } = [];
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
