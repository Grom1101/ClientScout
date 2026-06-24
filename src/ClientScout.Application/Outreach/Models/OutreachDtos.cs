using System;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Outreach.Models;

public record UserbotSessionDto(Guid Id, string Phone, string? DisplayName, bool IsActive, DateTimeOffset CreatedAt);
public record CreateUserbotSessionDto(string Phone, string SessionData, string? DisplayName);

public record MessageTemplateDto(Guid Id, string Name, string Content, DateTimeOffset CreatedAt);
public record CreateMessageTemplateDto(Guid ProfileId, string Name, string Content);

public record OutreachCampaignDto(
    Guid Id,
    Guid ProfileId,
    Guid TemplateId,
    string TargetChatsJson,
    int DelayMinSec,
    int DelayMaxSec,
    CampaignStatus Status,
    int SentCount,
    int ErrorCount,
    DateTimeOffset CreatedAt
);

public record CreateOutreachCampaignDto(
    Guid ProfileId,
    Guid TemplateId,
    string TargetChatsJson,
    int DelayMinSec,
    int DelayMaxSec
);
