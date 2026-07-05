using System;
using System.Collections.Generic;
using ClientScout.Domain.Enums;

namespace ClientScout.Domain.Entities;

/// <summary>
/// Application account: email + password. One account can have many profiles.
/// One account can link one Telegram userbot session.
/// </summary>
public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Telegram user ID linked to this account (null until user links Telegram).</summary>
    public long? TelegramUserId { get; set; }
    public string? TelegramName { get; set; }
    public string? TelegramAvatarBase64 { get; set; }
    public bool IsTelegramLinked => TelegramUserId.HasValue;

    public Guid? ActiveProfileId { get; set; }

    public string Subscription { get; set; } = "free";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Profile> Profiles { get; set; } = new List<Profile>();
}

/// <summary>Telegram user data (kept for Telegram-specific fields).</summary>
public class User
{
    public long Id { get; set; } // Telegram ID
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserbotSession> UserbotSessions { get; set; } = new List<UserbotSession>();
}

public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Primary owner: Account (email/password user)
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6C63FF";
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;

    public List<string> Keywords { get; set; } = new();
    public List<string> NegativeKeywords { get; set; } = new();

    public decimal? MinBudget { get; set; }
    public string? LanguageFilter { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Source> Sources { get; set; } = new List<Source>();
    public ICollection<JobLead> Leads { get; set; } = new List<JobLead>();
    public ICollection<MessageTemplate> Templates { get; set; } = new List<MessageTemplate>();
    public ICollection<OutreachCampaign> Campaigns { get; set; } = new List<OutreachCampaign>();
    public ICollection<ExchangeConnection> ExchangeConnections { get; set; } = new List<ExchangeConnection>();
    public SearchSettings? SearchSettings { get; set; }
}

public class ExchangeConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Profile? Profile { get; set; }
    public ExchangeType ExchangeType { get; set; } = ExchangeType.Kwork;
    public bool IsConnected { get; set; }
    public bool RequiresReconnect { get; set; }
    public string? EncryptedSession { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class SearchSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Profile? Profile { get; set; }

    public bool IsEnabled { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 30;
    public string[] UserKeywords { get; set; } = Array.Empty<string>();
    public string[] NegativeKeywords { get; set; } = Array.Empty<string>();
    public string? SearchProfileSummary { get; set; }
    public string[] MustIncludeSignals { get; set; } = Array.Empty<string>();
    public string[] SoftSignals { get; set; } = Array.Empty<string>();
    public string[] RejectSignals { get; set; } = Array.Empty<string>();
    public string[] ExpandedPositiveTerms { get; set; } = Array.Empty<string>();
    public string[] ExpandedIntentTerms { get; set; } = Array.Empty<string>();
    public string[] StrongTerms { get; set; } = Array.Empty<string>();
    public bool NeedsAiExpansion { get; set; }
    public DateTimeOffset? LastAiExpandedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Profile? Profile { get; set; }

    public SourceType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long? ChatId { get; set; }
    public string? Credentials { get; set; } // Encrypted
    public SourceStatus Status { get; set; } = SourceStatus.Pending;
    public string? LastError { get; set; }
    public DateTimeOffset? LastScraped { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<JobLead> Leads { get; set; } = new List<JobLead>();
}

public class JobLead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Profile? Profile { get; set; }
    public Guid SourceId { get; set; }
    public Source? Source { get; set; }

    public string ExternalId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string? AuthorUrl { get; set; }
    public decimal? Budget { get; set; }
    public SourceType SourceType { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public List<string> MatchedKeywords { get; set; } = new();
    public List<string> MatchedTerms { get; set; } = new();
    public int Score { get; set; }
    public int? AiConfidence { get; set; }
    public string? AiSummary { get; set; }
    public string? AiCategory { get; set; }
    public string? AiReason { get; set; }
    public AiLeadStatus AiStatus { get; set; } = AiLeadStatus.NotChecked;
    public DateTimeOffset FoundAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
}

public class MessageTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Profile? Profile { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[] AttachmentUrls { get; set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public ICollection<OutreachCampaign> Campaigns { get; set; } = new List<OutreachCampaign>();
}

public class OutreachCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Profile? Profile { get; set; }
    public Guid TemplateId { get; set; }
    public MessageTemplate? Template { get; set; }

    public string TargetChatsJson { get; set; } = "[]";
    public int DelayMinSec { get; set; } = 30;
    public int DelayMaxSec { get; set; } = 90;
    public int PeriodicityMinutes { get; set; } = 30;
    public string ScheduleMode { get; set; } = "allday";
    public string? ScheduleStartTime { get; set; }
    public string? ScheduleEndTime { get; set; }
    public int TimezoneOffsetMinutes { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    
    public int SentCount { get; set; }
    public int ErrorCount { get; set; }
    public int CurrentIndex { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<OutreachLog> Logs { get; set; } = new List<OutreachLog>();
}

public class OutreachLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public OutreachCampaign? Campaign { get; set; }
    
    public long? ChatId { get; set; }
    public string? ChatName { get; set; }
    public string? MessageContent { get; set; }  // snapshot of the actual sent text
    public LogStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}

public class UserbotSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long UserId { get; set; }
    public User? User { get; set; }
    
    public string Phone { get; set; } = string.Empty;
    public string SessionData { get; set; } = string.Empty; // Encrypted
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AiUsageLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }
}
