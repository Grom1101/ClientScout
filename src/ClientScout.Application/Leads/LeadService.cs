using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Application.Leads;

public class LeadService : ILeadService
{
    private readonly IAppDbContext _dbContext;

    public LeadService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<LeadDto>> GetLeadsByProfileAsync(Guid profileId, long userId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var leads = await _dbContext.JobLeads
            .Where(l => l.ProfileId == profileId && l.Status != LeadStatus.Hidden)
            .OrderByDescending(l => l.FoundAt)
            .Take(100) // MVP: simple limit
            .ToListAsync(cancellationToken);

        return leads.Select(MapToDto).ToList();
    }

    public async Task MarkAsViewedAsync(Guid id, long userId, CancellationToken cancellationToken = default)
    {
        var lead = await _dbContext.JobLeads
            .Include(l => l.Profile)
            .FirstOrDefaultAsync(l => l.Id == id && l.Profile!.UserId == userId, cancellationToken);

        if (lead != null && lead.Status == LeadStatus.New)
        {
            lead.Status = LeadStatus.Viewed;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsHiddenAsync(Guid id, long userId, CancellationToken cancellationToken = default)
    {
        var lead = await _dbContext.JobLeads
            .Include(l => l.Profile)
            .FirstOrDefaultAsync(l => l.Id == id && l.Profile!.UserId == userId, cancellationToken);

        if (lead != null)
        {
            lead.Status = LeadStatus.Hidden;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static LeadDto MapToDto(JobLead lead)
    {
        return new LeadDto(
            lead.Id,
            lead.ProfileId,
            lead.SourceId,
            lead.ExternalId,
            lead.Title,
            lead.Content,
            lead.OriginalUrl,
            lead.AuthorUrl,
            lead.Budget,
            lead.Status,
            lead.MatchedKeywords,
            lead.FoundAt
        );
    }
}
