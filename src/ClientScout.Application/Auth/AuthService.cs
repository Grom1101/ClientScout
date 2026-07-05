using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BCrypt.Net;
using ClientScout.Application.Auth.Models;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Profiles;
using ClientScout.Application.Profiles.Models;
using ClientScout.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ClientScout.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IProfileService _profileService;

    public AuthService(IAppDbContext db, IConfiguration config, IProfileService profileService)
    {
        _db = db;
        _config = config;
        _profileService = profileService;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        if (await _db.Accounts.AnyAsync(a => a.Email == email, cancellationToken))
            throw new InvalidOperationException("EMAIL_ALREADY_EXISTS");

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);

        // Create default profile automatically
        var profile = await _profileService.CreateProfileAsync(
            account.Id,
            new CreateProfileDto("Мой профиль", "#7C3AED", null, null, null, null),
            cancellationToken);

        // Set it as active
        account.ActiveProfileId = profile.Id;
        await _db.SaveChangesAsync(cancellationToken);

        var token = GenerateToken(account, rememberMe: false);

        return new AuthResultDto(token, account.Id, account.Email, false, account.ActiveProfileId);
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

        if (account == null)
            throw new UnauthorizedAccessException("INVALID_CREDENTIALS");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, account.PasswordHash))
            throw new UnauthorizedAccessException("INVALID_CREDENTIALS");

        // Ensure active profile is set
        if (!account.ActiveProfileId.HasValue)
        {
            var firstProfile = await _db.Profiles
                .FirstOrDefaultAsync(p => p.AccountId == account.Id && p.IsDefault, cancellationToken)
                ?? await _db.Profiles.FirstOrDefaultAsync(p => p.AccountId == account.Id, cancellationToken);

            if (firstProfile != null)
            {
                account.ActiveProfileId = firstProfile.Id;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var token = GenerateToken(account, dto.RememberMe);

        return new AuthResultDto(token, account.Id, account.Email, account.IsTelegramLinked, account.ActiveProfileId);
    }

    public async Task<AccountDto> GetMeAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new KeyNotFoundException("Account not found.");

        return MapToDto(account);
    }

    public async Task<AccountDto> LinkTelegramAsync(Guid accountId, long telegramUserId, string? telegramName, string? telegramAvatarBase64, CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new KeyNotFoundException("Account not found.");

        account.TelegramUserId = telegramUserId;
        account.TelegramName = telegramName;
        account.TelegramAvatarBase64 = telegramAvatarBase64;
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(account);
    }

    private string GenerateToken(Account account, bool rememberMe)
    {
        var secret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // rememberMe = 30 days; otherwise 24 hours
        var expiry = rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(24);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("accountId", account.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AccountDto MapToDto(Account account) => new(
        account.Id,
        account.Email,
        account.IsTelegramLinked,
        account.TelegramUserId,
        account.ActiveProfileId,
        account.TelegramName,
        account.TelegramAvatarBase64,
        account.Subscription,
        account.CreatedAt
    );
}
