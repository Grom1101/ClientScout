using System;
using System.Collections.Generic;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Leads;

public record LeadDto(
    Guid Id,
    Guid ProfileId,
    Guid SourceId,
    string SourceName,
    SourceType SourceType,
    string ExternalId,
    string? Title,
    string Content,
    string OriginalUrl,
    string? AuthorUrl,
    decimal? Budget,
    LeadStatus Status,
    List<string> MatchedTerms,
    int Score,
    int? AiConfidence,
    string? AiSummary,
    string? AiCategory,
    string? AiReason,
    AiLeadStatus AiStatus,
    DateTimeOffset FoundAt,
    DateTimeOffset ExpiresAt
);
