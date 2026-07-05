using System.Text.Json;
using ClientScout.Application.Search.Models;
using ClientScout.Domain.Entities;

namespace ClientScout.Application.Search;

public class AiLeadClassifier : IAiLeadClassifier
{
    private const int MaxBatchSize = 4;
    private const int MaxBatchInputChars = 4200;
    private const int MaxCandidateTextLength = 700;
    private const int MaxProfileSummaryLength = 1200;
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
            MustIncludeSignals = settings.MustIncludeSignals.Take(10).ToArray(),
            RejectSignals = settings.RejectSignals.Take(14).ToArray(),
            StrongTerms = settings.StrongTerms.Take(14).ToArray(),
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
- Treat UserKeywords as a combined intent/context, not as independent OR filters. When the user provides several keywords, one isolated technical word is not enough to confirm relevance.
- Technical keywords such as C#, Python, React, Unity, Figma, or Photoshop can be stack/context constraints. They do not make a lead relevant unless the requested deliverable also matches the broader profile intent.
- Decide by requested deliverable/profession/result, not simple keyword presence.
- Hard gate: if the text is not a concrete request/order from a buyer/client to perform work, return isRelevant=false even if it contains perfect profile keywords.
- A valid lead must ask for a deliverable such as develop, build, create, implement, fix, debug, finish, integrate, configure, or help with a specific task.
- Reject informational content: news, reports, analytics, market reviews, rankings, top charts, benchmarks, articles, announcements, research, platform reports, and "top grossing/downloaded" posts. Example: "top grossing mobile games in May 2026" is not a lead.
- Reject job vacancies, job ads, full-time/part-time hiring posts, resumes, CVs, candidate profiles, and "looking for work" posts. Words like "job", "work", "vacancy", "position", and "resume" are not positive signals by themselves.
- Reject adjacent non-matching deliverables even when they contain profile keywords. A domain word is not enough: the requested deliverable must match the user's actual profile intent.
- Examples: if the profile is about software development, reject video editing/marketing/articles/news even when they mention that domain; if the profile is about video editing, accept video editing requests and reject software-development-only requests. Apply this logic to every niche, not only gamedev.
- If the text only mentions a profile keyword but the requested deliverable belongs to a different service category than the profile intent, set isRelevant=false.
- Example: if UserKeywords are "C#, gamedev, Unity, game", a generic C# desktop app is not relevant because it misses the gamedev/Unity/game context.
- Respect NegativeKeywords and RejectSignals strictly.
- Reject spam, tutorials, news, reports, analytics, discussions, portfolios, resumes, and adjacent work with a different deliverable.
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
            MustIncludeSignals = settings.MustIncludeSignals.Take(10).ToArray(),
            RejectSignals = settings.RejectSignals.Take(14).ToArray(),
            StrongTerms = settings.StrongTerms.Take(14).ToArray(),
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
- Treat UserKeywords as a combined intent/context, not as independent OR filters. When the user provides several keywords, one isolated technical word is not enough to confirm relevance.
- Technical keywords such as C#, Python, React, Unity, Figma, or Photoshop can be stack/context constraints. They do not make a lead relevant unless the requested deliverable also matches the broader profile intent.
- Compare each candidate independently by requested deliverable/profession/result.
- Do not classify by simple keyword presence.
- Hard gate: if a text is not a concrete request/order from a buyer/client to perform work, return isRelevant=false even if it contains perfect profile keywords.
- A valid lead must ask for a deliverable such as develop, build, create, implement, fix, debug, finish, integrate, configure, or help with a specific task.
- Reject informational content: news, reports, analytics, market reviews, rankings, top charts, benchmarks, articles, announcements, research, platform reports, and "top grossing/downloaded" posts. Example: "top grossing mobile games in May 2026" is not a lead.
- Reject job vacancies, job ads, full-time/part-time hiring posts, resumes, CVs, candidate profiles, and "looking for work" posts. Words like "job", "work", "vacancy", "position", and "resume" are not positive signals by themselves.
- Reject adjacent non-matching deliverables even when they contain profile keywords. A domain word is not enough: the requested deliverable must match the user's actual profile intent.
- Examples: if the profile is about software development, reject video editing/marketing/articles/news even when they mention that domain; if the profile is about video editing, accept video editing requests and reject software-development-only requests. Apply this logic to every niche, not only gamedev.
- If the text only mentions a profile keyword but the requested deliverable belongs to a different service category than the profile intent, set isRelevant=false.
- Example: if UserKeywords are "C#, gamedev, Unity, game", a generic C# desktop app is not relevant because it misses the gamedev/Unity/game context.
- Respect NegativeKeywords and RejectSignals strictly.
- Reject spam, tutorials, news, reports, analytics, discussions, portfolios, resumes, and adjacent work with a different deliverable.
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
