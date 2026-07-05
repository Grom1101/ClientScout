using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Sources.Models;

namespace ClientScout.Application.Sources;

public interface ISourceService
{
    Task<List<SourceDto>> GetSourcesByProfileAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default);
    Task<SourceDto> CreateSourceAsync(Guid accountId, CreateSourceDto dto, CancellationToken cancellationToken = default);
    Task<SourceDto> UpdateSourceAsync(Guid id, Guid accountId, UpdateSourceDto dto, CancellationToken cancellationToken = default);
    Task DeleteSourceAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default);
    Task<ValidateSourceResponseDto> ValidateSourceAsync(Guid accountId, string url, int purpose = 1, CancellationToken cancellationToken = default);
}
