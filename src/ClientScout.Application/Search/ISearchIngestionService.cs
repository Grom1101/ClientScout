using ClientScout.Application.Search.Models;
using ClientScout.Application.Leads;
using ClientScout.Domain.Entities;

namespace ClientScout.Application.Search;

public interface ISearchIngestionService
{
    Task<TestCandidateResult> IngestTestCandidateAsync(Guid accountId, TestCandidateRequest request, CancellationToken cancellationToken = default);
    Task<LeadDto?> ProcessCandidateAsync(LeadCandidate candidate, CancellationToken cancellationToken = default);
    Task<List<LeadDto>> ProcessCandidatesAsync(IReadOnlyCollection<LeadCandidate> candidates, CancellationToken cancellationToken = default);
    Task ReclassifyUnverifiedLeadsAsync(CancellationToken cancellationToken = default);
    Task CleanupExpiredLeadsAsync(CancellationToken cancellationToken = default);
}
