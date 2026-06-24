using System;
using System.Collections.Generic;
using ClientScout.Domain.Enums;

namespace ClientScout.Domain.Entities;

public class User
{
    public long Id { get; set; } // Telegram ID
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Subscription { get; set; } = "free";

    public ICollection<Profile> Profiles { get; set; } = new List<Profile>();
    public ICollection<UserbotSession> UserbotSessions { get; set; } = new List<UserbotSession>();
}

public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long UserId { get; set; }
    public User? User { get; set; }

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
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public List<string> MatchedKeywords { get; set; } = new();
    public DateTimeOffset FoundAt { get; set; } = DateTimeOffset.UtcNow;
}

public class MessageTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Profile? Profile { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
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
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    
    public int SentCount { get; set; }
    public int ErrorCount { get; set; }
    public int CurrentIndex { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
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
