namespace ClientScout.Domain.Enums;

public enum SourceType
{
    Telegram,
    Kwork,
    Upwork
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
