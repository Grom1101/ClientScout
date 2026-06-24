using System;
using System.Collections.Generic;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Leads;

public record LeadDto(
    Guid Id,
    Guid ProfileId,
    Guid SourceId,
    string ExternalId,
    string? Title,
    string Content,
    string OriginalUrl,
    string? AuthorUrl,
    decimal? Budget,
    LeadStatus Status,
    List<string> MatchedKeywords,
    DateTimeOffset FoundAt
);
