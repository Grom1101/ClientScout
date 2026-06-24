using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Sources.Models;
using ClientScout.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Application.Sources;

public class SourceService : ISourceService
{
    private readonly IAppDbContext _dbContext;

    public SourceService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SourceDto>> GetSourcesByProfileAsync(Guid profileId, long userId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var sources = await _dbContext.Sources
            .Where(s => s.ProfileId == profileId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sources.Select(MapToDto).ToList();
    }

    public async Task<SourceDto> CreateSourceAsync(long userId, CreateSourceDto dto, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == dto.ProfileId && p.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var source = new Source
        {
            Id = Guid.NewGuid(),
            ProfileId = dto.ProfileId,
            Type = dto.Type,
            Name = dto.Name,
            Url = dto.Url,
            ChatId = dto.ChatId,
            Credentials = dto.Credentials,
            Status = ClientScout.Domain.Enums.SourceStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Sources.Add(source);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(source);
    }

    public async Task<SourceDto> UpdateSourceAsync(Guid id, long userId, UpdateSourceDto dto, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.Sources
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.Id == id && s.Profile!.UserId == userId, cancellationToken);

        if (source == null) throw new KeyNotFoundException("Source not found.");

        source.Name = dto.Name;
        source.Url = dto.Url;
        source.ChatId = dto.ChatId;
        source.Credentials = dto.Credentials;
        source.Status = dto.Status;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(source);
    }

    public async Task DeleteSourceAsync(Guid id, long userId, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.Sources
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.Id == id && s.Profile!.UserId == userId, cancellationToken);

        if (source == null) return;

        _dbContext.Sources.Remove(source);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SourceDto MapToDto(Source source)
    {
        return new SourceDto(
            source.Id,
            source.ProfileId,
            source.Type,
            source.Name,
            source.Url,
            source.ChatId,
            source.Status,
            source.LastError,
            source.LastScraped,
            source.CreatedAt
        );
    }
}
