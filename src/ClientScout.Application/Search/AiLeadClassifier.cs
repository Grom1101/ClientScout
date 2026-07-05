using System.Text.Json;
using ClientScout.Application.Search.Models;
using ClientScout.Domain.Entities;

namespace ClientScout.Application.Search;

public class AiLeadClassifier : IAiLeadClassifier
{
    private const int MaxBatchSize = 10;
    private const int MaxBatchInputChars = 10000;
    private const int MaxCandidateTextLength = 1200;
    private const int MaxProfileSummaryLength = 2600;
    private readonly AiJsonClient _ai;

    public AiLeadClassifier(AiJsonClient ai)
    {
        _ai = ai;
    }

    public bool IsAvailable => _ai.IsAvailable;
    public bool IsQuotaExceeded => _ai.LastFailureKind == AiFailureKind.RateLimited;

    public async Task<LeadClassificationResult?> ClassifyAsync(
        string rawText,
        Source source,
        SearchSettings settings,
        string[] matchedTerms,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        var input = new
        {
            rawText = Trim(rawText, MaxCandidateTextLength),
            source = new { source.Type, source.Name },
            settings.UserKeywords,
            settings.NegativeKeywords,
            SearchProfileSummary = Trim(settings.SearchProfileSummary, MaxProfileSummaryLength),
            MustIncludeSignals = settings.MustIncludeSignals.Take(16).ToArray(),
            RejectSignals = settings.RejectSignals.Take(24).ToArray(),
            StrongTerms = settings.StrongTerms.Take(24).ToArray(),
            matchedTerms
        };

        var prompt = $$"""
Classify whether this text is a real paid freelance/job lead relevant to the user's hidden search profile.
Return STRICT JSON only:
{
  "isRelevant": true,
  "confidence": 0,
  "summary": "short Russian summary, max 180 chars",
  "category": "short category",
  "reason": "short Russian reason"
}

Decision target:
- The hidden profile is authoritative. UserKeywords are short hints, not the whole intent.
- Decide by requested deliverable/profession/result, not simple keyword presence.
- Respect NegativeKeywords and RejectSignals strictly.
- Reject spam, tutorials, news, discussions, portfolios, resumes, and adjacent work with a different deliverable.
- Reject service-provider/self-promotion messages like "я разработчик", "предлагаю услуги", "портфолио", "коротко обо мне", unless the text also clearly asks to hire someone for a concrete task.
- Short Telegram-style requests can be relevant. Do NOT reject only because the message is short if it clearly contains buyer intent and a matching deliverable, for example "нужно разработать сайт", "ищу frontend-разработчика", "требуется доработать React".
- Use isRelevant=true only for paid/service work matching the profile.
- confidence is 0-100.

Input JSON:
{{JsonSerializer.Serialize(input)}}
""";

        var result = await _ai.GenerateJsonAsync<LeadClassificationResult>(prompt, AiTaskKind.LeadClassification, cancellationToken);
        return NormalizeResult(result);
    }

    public async Task<IReadOnlyDictionary<string, LeadClassificationResult>?> ClassifyBatchAsync(
        IReadOnlyCollection<BatchLeadClassificationInput> candidates,
        Source source,
        SearchSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || candidates.Count == 0)
        {
            return null;
        }

        var batch = BuildDynamicBatch(candidates).ToArray();
        if (batch.Length == 0)
        {
            return null;
        }

        var input = new
        {
            source = new { source.Type, source.Name },
            settings.UserKeywords,
            settings.NegativeKeywords,
            SearchProfileSummary = Trim(settings.SearchProfileSummary, MaxProfileSummaryLength),
            MustIncludeSignals = settings.MustIncludeSignals.Take(16).ToArray(),
            RejectSignals = settings.RejectSignals.Take(24).ToArray(),
            StrongTerms = settings.StrongTerms.Take(24).ToArray(),
            candidates = batch
                .Select(candidate => new
                {
                    candidate.ExternalId,
                    rawText = Trim(candidate.RawText, MaxCandidateTextLength),
                    MatchedTerms = candidate.MatchedTerms.Take(10).ToArray()
                })
                .ToArray()
        };

        var prompt = $$"""
Classify a batch of freelance/job lead candidates against the user's hidden search profile.
Return STRICT JSON only:
{
  "items": [
    {
      "externalId": "candidate id",
      "isRelevant": true,
      "confidence": 0,
      "summary": "short Russian summary, max 180 chars",
      "category": "short category",
      "reason": "short Russian reason"
    }
  ]
}

Decision target:
- Return one result for every candidate externalId from the input.
- The hidden profile is authoritative. UserKeywords are short hints, not the whole intent.
- Compare each candidate independently by requested deliverable/profession/result.
- Do not classify by simple keyword presence.
- Respect NegativeKeywords and RejectSignals strictly.
- Reject spam, tutorials, news, discussions, portfolios, resumes, and adjacent work with a different deliverable.
- Reject service-provider/self-promotion messages like "я разработчик", "предлагаю услуги", "портфолио", "коротко обо мне", unless the text also clearly asks to hire someone for a concrete task.
- Short Telegram-style requests can be relevant. Do NOT reject only because the message is short if it clearly contains buyer intent and a matching deliverable, for example "нужно разработать сайт", "ищу frontend-разработчика", "требуется доработать React".
- Use isRelevant=true only for paid/service work matching the profile.
- confidence is 0-100.

Input JSON:
{{JsonSerializer.Serialize(input)}}
""";

        var result = await _ai.GenerateJsonAsync<LeadBatchClassificationResult>(prompt, AiTaskKind.LeadClassification, cancellationToken);
        if (result?.Items == null)
        {
            return null;
        }

        return result.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ExternalId))
            .GroupBy(item => item.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => NormalizeBatchItem(group.First()),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<BatchLeadClassificationInput> BuildDynamicBatch(IReadOnlyCollection<BatchLeadClassificationInput> candidates)
    {
        var totalChars = 0;
        foreach (var candidate in candidates.Take(MaxBatchSize))
        {
            var candidateChars = Math.Min(candidate.RawText.Length, MaxCandidateTextLength);
            if (totalChars > 0 && totalChars + candidateChars > MaxBatchInputChars)
            {
                yield break;
            }

            totalChars += candidateChars;
            yield return candidate;
        }
    }

    private static LeadClassificationResult? NormalizeResult(LeadClassificationResult? result)
    {
        if (result == null)
        {
            return null;
        }

        return result with
        {
            Confidence = Math.Clamp(result.Confidence, 0, 100),
            Summary = Trim(result.Summary, 240),
            Category = Trim(result.Category, 80),
            Reason = Trim(result.Reason, 240)
        };
    }

    private static LeadClassificationResult NormalizeBatchItem(LeadBatchClassificationItemResult item)
    {
        return new LeadClassificationResult(
            item.IsRelevant,
            Math.Clamp(item.Confidence, 0, 100),
            Trim(item.Summary, 240),
            Trim(item.Category, 80),
            Trim(item.Reason, 240));
    }

    private static string Trim(string? value, int max)
    {
        value = value?.Trim() ?? string.Empty;
        return value.Length <= max ? value : value[..max];
    }

    private sealed record LeadBatchClassificationResult(LeadBatchClassificationItemResult[] Items);
}
