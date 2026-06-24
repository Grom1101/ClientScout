using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using ClientScout.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClientScout.Application.Leads;

public class LeadParsingService
{
    private readonly IAppDbContext _dbContext;
    private readonly IEnumerable<ISourceScraper> _scrapers;
    private readonly ILogger<LeadParsingService> _logger;

    public LeadParsingService(IAppDbContext dbContext, IEnumerable<ISourceScraper> scrapers, ILogger<LeadParsingService> logger)
    {
        _dbContext = dbContext;
        _scrapers = scrapers;
        _logger = logger;
    }

    public async Task RunParsingJobAsync(CancellationToken cancellationToken = default)
    {
        var activeSources = await _dbContext.Sources
            .Include(s => s.Profile)
            .Where(s => s.Profile!.IsActive && s.Status == SourceStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var source in activeSources)
        {
            var scraper = _scrapers.FirstOrDefault(s => s.Type == source.Type);
            if (scraper == null) continue;

            try
            {
                var leads = await scraper.ScrapeLatestAsync(source, cancellationToken);
                source.LastScraped = DateTimeOffset.UtcNow;
                
                await ProcessParsedLeadsAsync(source, leads, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping source {SourceId}", source.Id);
                source.LastError = ex.Message;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessParsedLeadsAsync(Source source, List<JobLead> leads, CancellationToken cancellationToken)
    {
        foreach (var lead in leads)
        {
            // Check if already exists
            var exists = await _dbContext.JobLeads
                .AnyAsync(l => l.SourceId == source.Id && l.ExternalId == lead.ExternalId, cancellationToken);

            if (exists) continue;

            // Match keywords
            var isMatch = IsMatch(lead, source.Profile!);
            if (isMatch)
            {
                _dbContext.JobLeads.Add(lead);
            }
        }
    }

    private static bool IsMatch(JobLead lead, Profile profile)
    {
        var content = (lead.Title + " " + lead.Content).ToLowerInvariant();

        // 1. Negative keywords override everything
        if (profile.NegativeKeywords.Any(nk => content.Contains(nk.ToLowerInvariant())))
            return false;

        // 2. Must contain at least one keyword if keywords are specified
        if (profile.Keywords.Any())
        {
            var matched = profile.Keywords.Where(k => content.Contains(k.ToLowerInvariant())).ToList();
            if (!matched.Any()) return false;
            
            lead.MatchedKeywords = matched;
        }

        return true;
    }
}
