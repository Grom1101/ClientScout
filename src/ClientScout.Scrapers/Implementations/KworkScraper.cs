using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using ClientScout.Application.Common.Interfaces;

namespace ClientScout.Scrapers.Implementations;

public class KworkScraper : ISourceScraper
{
    public SourceType Type => SourceType.Kwork;

    public async Task<List<JobLead>> ScrapeLatestAsync(Source source, CancellationToken cancellationToken = default)
    {
        var leads = new List<JobLead>();
        
        // MVP: Simple fetch using AngleSharp
        // The URL is usually something like https://kwork.ru/projects
        // We will do a basic setup. Actually parsing Kwork might require handling auth/cloudflare,
        // but for MVP we will just return a dummy or do a basic GET.
        
        var config = Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);
        
        try
        {
            var document = await context.OpenAsync(source.Url, cancellationToken);
            if (document == null) return leads;

            var items = document.QuerySelectorAll(".wants-card__header-title");
            foreach (var item in items)
            {
                var title = item.TextContent?.Trim();
                var link = (item as AngleSharp.Html.Dom.IHtmlAnchorElement)?.Href;
                
                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(link))
                {
                    leads.Add(new JobLead
                    {
                        Id = Guid.NewGuid(),
                        SourceId = source.Id,
                        ProfileId = source.ProfileId,
                        ExternalId = link, // use link as unique id for now
                        Title = title,
                        Content = title, // we'd need to parse description as well
                        OriginalUrl = link,
                        Status = LeadStatus.New,
                        FoundAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            source.LastError = ex.Message;
        }

        return leads;
    }
}
