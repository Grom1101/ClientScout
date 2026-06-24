using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Sources.Models;

namespace ClientScout.Application.Sources;

public interface ISourceService
{
    Task<List<SourceDto>> GetSourcesByProfileAsync(Guid profileId, long userId, CancellationToken cancellationToken = default);
    Task<SourceDto> CreateSourceAsync(long userId, CreateSourceDto dto, CancellationToken cancellationToken = default);
    Task<SourceDto> UpdateSourceAsync(Guid id, long userId, UpdateSourceDto dto, CancellationToken cancellationToken = default);
    Task DeleteSourceAsync(Guid id, long userId, CancellationToken cancellationToken = default);
}
