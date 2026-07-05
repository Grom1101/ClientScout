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

    public async Task<List<ProfileDto>> GetProfilesAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var profiles = await _dbContext.Profiles
            .Where(p => p.AccountId == accountId)
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return profiles.Select(MapToDto).ToList();
    }

    public async Task<ProfileDto?> GetProfileAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(p => p.Id == id && p.AccountId == accountId, cancellationToken);

        return profile == null ? null : MapToDto(profile);
    }

    public async Task<ProfileDto> CreateProfileAsync(Guid accountId, CreateProfileDto dto, CancellationToken cancellationToken = default)
    {
        var profileCount = await _dbContext.Profiles.CountAsync(p => p.AccountId == accountId, cancellationToken);
        if (profileCount >= 5)
        {
            throw new InvalidOperationException("Максимальное количество профилей — 5.");
        }
        var hasProfiles = profileCount > 0;

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Name = dto.Name,
            Color = dto.Color ?? "#6C63FF",
            Keywords = dto.Keywords ?? new List<string>(),
            NegativeKeywords = dto.NegativeKeywords ?? new List<string>(),
            MinBudget = dto.MinBudget,
            LanguageFilter = dto.LanguageFilter,
            IsDefault = !hasProfiles, // first profile is default
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Profiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(profile);
    }

    public async Task<ProfileDto> UpdateProfileAsync(Guid id, Guid accountId, UpdateProfileDto dto, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(p => p.Id == id && p.AccountId == accountId, cancellationToken);

        if (profile == null)
            throw new KeyNotFoundException("Profile not found.");

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account?.ActiveProfileId != id)
            throw new InvalidOperationException("Only the active profile can be renamed.");

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

    public async Task DeleteProfileAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(p => p.Id == id && p.AccountId == accountId, cancellationToken);

        if (profile == null) return;

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account?.ActiveProfileId == id)
            throw new InvalidOperationException("The active profile cannot be deleted.");

        _dbContext.Profiles.Remove(profile);
        if (account?.ActiveProfileId == id)
        {
            var fallbackProfile = await _dbContext.Profiles
                .Where(p => p.AccountId == accountId && p.Id != id)
                .OrderByDescending(p => p.IsDefault)
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            account.ActiveProfileId = fallbackProfile?.Id;
            if (fallbackProfile != null)
            {
                fallbackProfile.IsDefault = true;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDefaultProfileAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default)
    {
        var profiles = await _dbContext.Profiles
            .Where(p => p.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var targetProfile = profiles.FirstOrDefault(p => p.Id == id);
        if (targetProfile == null)
            throw new KeyNotFoundException("Profile not found.");

        foreach (var p in profiles)
        {
            p.IsDefault = p.Id == id;
        }

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account != null)
        {
            account.ActiveProfileId = id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ProfileDto MapToDto(Profile profile) => new(
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
