using System.Text.Json;

namespace ClientScout.Application.Search;

public readonly record struct AiUsage(int InputTokens, int OutputTokens);

public static class AiUsageParser
{
    public static AiUsage Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new AiUsage(0, 0);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("usage", out var usage))
            {
                return new AiUsage(0, 0);
            }

            var inputTokens = usage.TryGetProperty("prompt_tokens", out var promptTokens)
                ? promptTokens.GetInt32()
                : 0;
            var outputTokens = usage.TryGetProperty("completion_tokens", out var completionTokens)
                ? completionTokens.GetInt32()
                : 0;

            return new AiUsage(inputTokens, outputTokens);
        }
        catch (JsonException)
        {
            return new AiUsage(0, 0);
        }
    }
}
