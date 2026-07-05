using ClientScout.Domain.Entities;

namespace ClientScout.Application.Leads;

public static class LeadMapper
{
    public static LeadDto MapToDto(JobLead lead)
    {
        return new LeadDto(
            lead.Id,
            lead.ProfileId,
            lead.SourceId,
            lead.SourceName,
            lead.SourceType,
            lead.ExternalId,
            lead.Title,
            lead.Content,
            lead.OriginalUrl,
            lead.AuthorUrl,
            lead.Budget,
            lead.Status,
            lead.MatchedTerms.Count > 0 ? lead.MatchedTerms : lead.MatchedKeywords,
            lead.Score,
            lead.AiConfidence,
            lead.AiSummary,
            lead.AiCategory,
            lead.AiReason,
            lead.AiStatus,
            lead.FoundAt,
            lead.ExpiresAt);
    }
}
