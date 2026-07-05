using ClientScout.Application.Search.Models;

namespace ClientScout.Application.Search;

public interface IAiSearchExpansionService
{
    bool IsAvailable { get; }
    Task<SearchExpansionResult?> ExpandAsync(SearchExpansionRequest request, CancellationToken cancellationToken = default);
}
