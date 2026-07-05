using System;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Auth.Models;

namespace ClientScout.Application.Auth;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    Task<AccountDto> GetMeAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<AccountDto> LinkTelegramAsync(Guid accountId, long telegramUserId, string? telegramName, string? telegramAvatarBase64, CancellationToken cancellationToken = default);
}
