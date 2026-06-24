using System;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Sources.Models;

public record SourceDto(
    Guid Id,
    Guid ProfileId,
    SourceType Type,
    string Name,
    string Url,
    long? ChatId,
    SourceStatus Status,
    string? LastError,
    DateTimeOffset? LastScraped,
    DateTimeOffset CreatedAt
);

public record CreateSourceDto(
    Guid ProfileId,
    SourceType Type,
    string Name,
    string Url,
    long? ChatId,
    string? Credentials
);

public record UpdateSourceDto(
    string Name,
    string Url,
    long? ChatId,
    string? Credentials,
    SourceStatus Status
);
