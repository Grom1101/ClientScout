using System;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Outreach.Models;

public record UserbotSessionDto(Guid Id, string Phone, string? DisplayName, bool IsActive, DateTimeOffset CreatedAt);
public record CreateUserbotSessionDto(string Phone, string SessionData, string? DisplayName);

public record MessageTemplateDto(Guid Id, string Name, string Content, string[] AttachmentUrls, DateTimeOffset CreatedAt);
public record CreateMessageTemplateDto(Guid ProfileId, string Name, string Content, string[]? AttachmentUrls = null);
public record UpdateMessageTemplateDto(string? Name = null, string? Content = null, string[]? AttachmentUrls = null);

public record OutreachCampaignDto(
    Guid Id,
    Guid ProfileId,
    Guid TemplateId,
    string TargetChatsJson,
    int DelayMinSec,
    int DelayMaxSec,
    int PeriodicityMinutes,
    string ScheduleMode,
    string? ScheduleStartTime,
    string? ScheduleEndTime,
    int TimezoneOffsetMinutes,
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
    int DelayMaxSec,
    int PeriodicityMinutes = 30,
    string ScheduleMode = "allday",
    string? ScheduleStartTime = null,
    string? ScheduleEndTime = null,
    int TimezoneOffsetMinutes = 0
);

public record OutreachStatsDto(
    int SentToday,
    int LeadsToday,
    List<OutreachActivityPointDto> Activity,
    List<RecentOutreachLogDto> RecentLogs
);

public record OutreachActivityPointDto(string Label, int Sent, int Errors, int Leads);

public record RecentOutreachLogDto(
    Guid Id,
    string ChatName,
    string ProfileName,
    string MessagePreview,
    LogStatus Status,
    string? ErrorMessage,
    DateTimeOffset SentAt
);
