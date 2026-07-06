using ClientScout.Application.Common.Interfaces;
using ClientScout.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClientScout.Application.Search;

public sealed class AiUsageLogger : IAiUsageLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiUsageLogger> _logger;

    public AiUsageLogger(IServiceScopeFactory scopeFactory, ILogger<AiUsageLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogAsync(
        AiProviderModelCandidate candidate,
        int statusCode,
        string body,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var usage = AiUsageParser.Parse(body);
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            dbContext.AiUsageLogs.Add(new AiUsageLog
            {
                ProviderName = candidate.Provider.Name,
                ModelName = candidate.Model.Id,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                CostUsd = EstimateCostUsd(candidate, usage),
                StatusCode = statusCode,
                ErrorMessage = errorMessage
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI usage log for {Provider}/{Model}", candidate.Provider.Name, candidate.Model.Id);
        }
    }

    private static decimal EstimateCostUsd(AiProviderModelCandidate candidate, AiUsage usage)
    {
        if (!candidate.Provider.Name.Equals("BluesMinds", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        return candidate.Model.Id switch
        {
            "gpt-4o-mini" => (usage.InputTokens / 1_000_000m * 0.30m) + (usage.OutputTokens / 1_000_000m * 0.18m),
            "mimo-v2.5" => (usage.InputTokens / 1_000_000m * 0.10m) + (usage.OutputTokens / 1_000_000m * 0.28m),
            _ => 0m
        };
    }
}
