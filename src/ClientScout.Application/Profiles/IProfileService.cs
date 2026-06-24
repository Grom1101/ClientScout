using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Profiles.Models;

namespace ClientScout.Application.Profiles;

public interface IProfileService
{
    Task<List<ProfileDto>> GetProfilesAsync(long userId, CancellationToken cancellationToken = default);
    Task<ProfileDto?> GetProfileAsync(Guid id, long userId, CancellationToken cancellationToken = default);
    Task<ProfileDto> CreateProfileAsync(long userId, CreateProfileDto dto, CancellationToken cancellationToken = default);
    Task<ProfileDto> UpdateProfileAsync(Guid id, long userId, UpdateProfileDto dto, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(Guid id, long userId, CancellationToken cancellationToken = default);
    Task SetDefaultProfileAsync(Guid id, long userId, CancellationToken cancellationToken = default);
}
