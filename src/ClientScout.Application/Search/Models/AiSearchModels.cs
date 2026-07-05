using ClientScout.Domain.Entities;

namespace ClientScout.Application.Search.Models;

public record SearchExpansionRequest(
    string[] UserKeywords,
    string[] NegativeKeywords,
    string[] PreviousExpandedPositiveTerms,
    string[] PreviousExpandedIntentTerms,
    string[] PreviousStrongTerms,
    string? PreviousSearchProfileSummary,
    string[] PreviousMustIncludeSignals,
    string[] PreviousSoftSignals,
    string[] PreviousRejectSignals,
    string[] AddedKeywords,
    string[] RemovedKeywords,
    string[] AddedNegativeKeywords,
    string[] RemovedNegativeKeywords);

public record SearchExpansionResult(
    string SearchProfileSummary,
    string[] MustIncludeSignals,
    string[] SoftSignals,
    string[] RejectSignals,
    string[] ExpandedPositiveTerms,
    string[] ExpandedIntentTerms,
    string[] StrongTerms,
    string[] NormalizedNegativeTerms);

public record PrefilterResult(
    bool IsCandidate,
    string[] MatchedTerms,
    string[] MatchedStrongTerms,
    string? RejectionReason,
    int Score);

public record LeadClassificationResult(
    bool IsRelevant,
    int Confidence,
    string Summary,
    string Category,
    string Reason);

public record LeadBatchClassificationItemResult(
    string ExternalId,
    bool IsRelevant,
    int Confidence,
    string Summary,
    string Category,
    string Reason);

public record TestCandidateRequest(
    Guid ProfileId,
    Guid? SourceId,
    string Text,
    string? Title,
    string? OriginalUrl,
    string? AuthorUrl,
    string? ExternalId);

public record TestCandidateResult(
    bool Saved,
    bool IsCandidate,
    string? RejectionReason,
    LeadClassificationResult? Classification,
    Leads.LeadDto? Lead);

public record LeadCandidate(
    Guid ProfileId,
    Guid SourceId,
    string ExternalId,
    string? Title,
    string Content,
    string OriginalUrl,
    string? AuthorUrl,
    Source Source,
    string? TopicName = null);

public record SearchCandidateJobDto(
    Guid ProfileId,
    Guid SourceId,
    string ExternalId,
    string? Title,
    string Content,
    string OriginalUrl,
    string? AuthorUrl,
    string? TopicName = null);

public record SearchCandidateBatchJobDto(
    SearchCandidateJobDto[] Candidates);
