using ClientScout.Application.Search.Models;
using ClientScout.Domain.Entities;

namespace ClientScout.Application.Search;

public interface IAiLeadClassifier
{
    bool IsAvailable { get; }
    bool IsQuotaExceeded { get; }
    Task<LeadClassificationResult?> ClassifyAsync(
        string rawText,
        Source source,
        SearchSettings settings,
        string[] matchedTerms,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, LeadClassificationResult>?> ClassifyBatchAsync(
        IReadOnlyCollection<BatchLeadClassificationInput> candidates,
        Source source,
        SearchSettings settings,
        CancellationToken cancellationToken = default);
}

public record BatchLeadClassificationInput(
    string ExternalId,
    string RawText,
    string[] MatchedTerms);
