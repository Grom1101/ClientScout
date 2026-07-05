using System;

namespace ClientScout.Application.Search.Models;

public record SearchSettingsDto(
    Guid Id,
    Guid ProfileId,
    bool IsEnabled,
    bool NotificationsEnabled,
    int IntervalMinutes,
    string[] UserKeywords,
    string[] NegativeKeywords,
    bool NeedsAiExpansion,
    DateTimeOffset? LastAiExpandedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record UpdateSearchSettingsDto(
    Guid ProfileId,
    bool IsEnabled,
    bool NotificationsEnabled,
    int IntervalMinutes,
    string[] UserKeywords,
    string[] NegativeKeywords
);
