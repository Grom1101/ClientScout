using System;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Search.Models;

namespace ClientScout.Application.Search;

public interface ISearchSettingsService
{
    Task<SearchSettingsDto> GetSettingsAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default);
    Task<SearchSettingsDto> UpdateSettingsAsync(Guid accountId, UpdateSearchSettingsDto dto, CancellationToken cancellationToken = default);
    Task EnqueuePendingProfileExpansionsAsync(CancellationToken cancellationToken = default);
    Task ExpandProfileBackgroundAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task ExpandProfileBackgroundAsync(Guid profileId, SearchExpansionRequest request, CancellationToken cancellationToken = default);
}
