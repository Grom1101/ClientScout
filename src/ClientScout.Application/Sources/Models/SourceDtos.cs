using System;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Sources.Models;

public record SourceDto(
    Guid Id,
    Guid ProfileId,
    SourceType Type,
    int Purpose,
    string Name,
    string Url,
    long? ChatId,
    SourceStatus Status,
    int? MemberCount,
    string? AvatarUrl,
    string? BaseUrl,
    string? TopicId,
    string? TopicName,
    bool IsForumTopic,
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
    string? Credentials,
    int Purpose = 0
);

public record UpdateSourceDto(
    string? Name = null,
    string? Url = null,
    long? ChatId = null,
    string? Credentials = null,
    SourceStatus? Status = null,
    int? Purpose = null
);
