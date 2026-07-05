namespace ClientScout.Application.Search;

public interface ISearchJobService
{
    Task ScheduleDueSearchAsync(CancellationToken cancellationToken = default);
    Task ScanSourceAsync(Guid sourceId, CancellationToken cancellationToken = default);
    Task ScanKworkAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task ProcessCandidateAsync(Models.SearchCandidateJobDto dto, CancellationToken cancellationToken = default);
    Task ProcessCandidateBatchAsync(Models.SearchCandidateBatchJobDto dto, CancellationToken cancellationToken = default);
    Task FlushCandidateBatchesAsync(Guid profileId, CancellationToken cancellationToken = default);
}
