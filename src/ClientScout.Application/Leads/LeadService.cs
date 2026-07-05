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

    public async Task<List<LeadDto>> GetLeadsByProfileAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default)
    {
        return await GetLeadHistoryAsync(profileId, accountId, 100, 0, null, cancellationToken);
    }

    public async Task<List<LeadDto>> GetRecentLeadsAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default)
    {
        return await GetLeadHistoryAsync(profileId, accountId, 10, 0, null, cancellationToken);
    }

    public async Task<List<LeadDto>> GetLeadHistoryAsync(Guid profileId, Guid accountId, int limit, int offset, string? aiFilter = null, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);
        var now = DateTimeOffset.UtcNow;

        var query = _dbContext.JobLeads
            .Where(l => l.ProfileId == profileId &&
                        l.Status != LeadStatus.Hidden &&
                        l.ExpiresAt > now)
            .AsQueryable();

        if (string.Equals(aiFilter, "confirmed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.AiStatus == AiLeadStatus.Confirmed);
        }
        else if (string.Equals(aiFilter, "unverified", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.AiStatus == AiLeadStatus.AiUnavailable ||
                                     l.AiStatus == AiLeadStatus.KeywordOnly ||
                                     l.AiStatus == AiLeadStatus.Error ||
                                     l.AiStatus == AiLeadStatus.NotChecked);
        }

        var leads = await query
            .OrderByDescending(l => l.FoundAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return leads
            .Select(LeadMapper.MapToDto)
            .ToList();
    }

    public async Task<int> CountLeadsAsync(Guid profileId, Guid accountId, string? aiFilter = null, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var now = DateTimeOffset.UtcNow;
        var query = _dbContext.JobLeads
            .Where(l => l.ProfileId == profileId &&
                        l.Status != LeadStatus.Hidden &&
                        l.ExpiresAt > now);

        if (string.Equals(aiFilter, "confirmed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.AiStatus == AiLeadStatus.Confirmed);
        }
        else if (string.Equals(aiFilter, "unverified", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.AiStatus == AiLeadStatus.AiUnavailable ||
                                     l.AiStatus == AiLeadStatus.KeywordOnly ||
                                     l.AiStatus == AiLeadStatus.Error ||
                                     l.AiStatus == AiLeadStatus.NotChecked);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task MarkAsViewedAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default)
    {
        var lead = await _dbContext.JobLeads
            .Include(l => l.Profile)
            .FirstOrDefaultAsync(l => l.Id == id && l.Profile!.AccountId == accountId, cancellationToken);

        if (lead != null && lead.Status == LeadStatus.New)
        {
            lead.Status = LeadStatus.Viewed;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsHiddenAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default)
    {
        var lead = await _dbContext.JobLeads
            .Include(l => l.Profile)
            .FirstOrDefaultAsync(l => l.Id == id && l.Profile!.AccountId == accountId, cancellationToken);

        if (lead != null)
        {
            lead.Status = LeadStatus.Hidden;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

