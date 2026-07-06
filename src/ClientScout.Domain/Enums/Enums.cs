namespace ClientScout.Domain.Enums;

public enum SourceType
{
    Telegram,
    Kwork,
    Upwork,
    Fiverr,
    Freelancer
}

public enum ExchangeType
{
    Kwork,
    Upwork,
    Fiverr,
    Freelancer
}

public enum ExchangeConnectionStatus
{
    NotConnected,
    Connected,
    RequiresReconnect
}

public enum SourceStatus
{
    Pending,
    Active,
    Error
}

public enum LeadStatus
{
    New,
    Viewed,
    Responded,
    Hidden
}

public enum AiLeadStatus
{
    NotChecked,
    Confirmed,
    Rejected,
    AiUnavailable,
    KeywordOnly,
    Error
}

public enum CampaignStatus
{
    Draft,
    Running,
    Paused,
    Done
}

public enum LogStatus
{
    Sent,
    Error,
    Skipped
}
