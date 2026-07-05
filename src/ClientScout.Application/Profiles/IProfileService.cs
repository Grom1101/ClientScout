using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Profiles.Models;

namespace ClientScout.Application.Profiles;

public interface IProfileService
{
    Task<List<ProfileDto>> GetProfilesAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<ProfileDto?> GetProfileAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default);
    Task<ProfileDto> CreateProfileAsync(Guid accountId, CreateProfileDto dto, CancellationToken cancellationToken = default);
    Task<ProfileDto> UpdateProfileAsync(Guid id, Guid accountId, UpdateProfileDto dto, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default);
    Task SetDefaultProfileAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default);
}
