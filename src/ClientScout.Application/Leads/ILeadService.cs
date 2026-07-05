using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClientScout.Application.Leads;

public interface ILeadService
{
    Task<List<LeadDto>> GetLeadsByProfileAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default);
    Task<List<LeadDto>> GetRecentLeadsAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default);
    Task<List<LeadDto>> GetLeadHistoryAsync(Guid profileId, Guid accountId, int limit, int offset, string? aiFilter = null, CancellationToken cancellationToken = default);
    Task<int> CountLeadsAsync(Guid profileId, Guid accountId, string? aiFilter = null, CancellationToken cancellationToken = default);
    Task MarkAsViewedAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default);
    Task MarkAsHiddenAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default);
}

