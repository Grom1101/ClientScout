using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClientScout.Application.Leads;

public interface ILeadService
{
    Task<List<LeadDto>> GetLeadsByProfileAsync(Guid profileId, long userId, CancellationToken cancellationToken = default);
    Task MarkAsViewedAsync(Guid id, long userId, CancellationToken cancellationToken = default);
    Task MarkAsHiddenAsync(Guid id, long userId, CancellationToken cancellationToken = default);
}
