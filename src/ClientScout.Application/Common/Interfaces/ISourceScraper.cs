using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Common.Interfaces;

public interface ISourceScraper
{
    SourceType Type { get; }
    Task<List<JobLead>> ScrapeLatestAsync(Source source, CancellationToken cancellationToken = default);
}
