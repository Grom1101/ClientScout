using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Profiles.Models;
using ClientScout.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Application.Profiles;

public class ProfileService : IProfileService
{
    private readonly IAppDbContext _dbContext;

    public ProfileService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ProfileDto>> GetProfilesAsync(long userId, CancellationToken cancellationToken = default)
    {
        var profiles = await _dbContext.Profiles
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return profiles.Select(MapToDto).ToList();
    }

    public async Task<ProfileDto?> GetProfileAsync(Guid id, long userId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
            
        return profile == null ? null : MapToDto(profile);
    }

    public async Task<ProfileDto> CreateProfileAsync(long userId, CreateProfileDto dto, CancellationToken cancellationToken = default)
    {
        var hasProfiles = await _dbContext.Profiles.AnyAsync(p => p.UserId == userId, cancellationToken);

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Color = dto.Color ?? "#6C63FF",
            Keywords = dto.Keywords ?? new List<string>(),
            NegativeKeywords = dto.NegativeKeywords ?? new List<string>(),
            MinBudget = dto.MinBudget,
            LanguageFilter = dto.LanguageFilter,
            IsDefault = !hasProfiles, // First profile is default
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Profiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(profile);
    }

    public async Task<ProfileDto> UpdateProfileAsync(Guid id, long userId, UpdateProfileDto dto, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);

        if (profile == null)
            throw new KeyNotFoundException("Profile not found.");

        profile.Name = dto.Name;
        profile.Color = dto.Color ?? profile.Color;
        profile.IsActive = dto.IsActive;
        profile.Keywords = dto.Keywords ?? profile.Keywords;
        profile.NegativeKeywords = dto.NegativeKeywords ?? profile.NegativeKeywords;
        profile.MinBudget = dto.MinBudget;
        profile.LanguageFilter = dto.LanguageFilter;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(profile);
    }

    public async Task DeleteProfileAsync(Guid id, long userId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);

        if (profile == null) return;

        _dbContext.Profiles.Remove(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDefaultProfileAsync(Guid id, long userId, CancellationToken cancellationToken = default)
    {
        var profiles = await _dbContext.Profiles
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        var targetProfile = profiles.FirstOrDefault(p => p.Id == id);
        if (targetProfile == null)
            throw new KeyNotFoundException("Profile not found.");

        foreach (var p in profiles)
        {
            p.IsDefault = p.Id == id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ProfileDto MapToDto(Profile profile)
    {
        return new ProfileDto(
            profile.Id,
            profile.Name,
            profile.Color,
            profile.IsActive,
            profile.IsDefault,
            profile.Keywords,
            profile.NegativeKeywords,
            profile.MinBudget,
            profile.LanguageFilter,
            profile.CreatedAt
        );
    }
}
