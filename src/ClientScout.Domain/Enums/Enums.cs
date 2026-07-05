namespace ClientScout.Domain.Enums;

public enum SourceType
{
    Telegram,
    Kwork,
    Upwork
}

public enum ExchangeType
{
    Kwork
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
