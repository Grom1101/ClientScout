using System;

namespace ClientScout.Application.Auth.Models;

public record RegisterDto(string Email, string Password);

public record LoginDto(string Email, string Password, bool RememberMe = false);

public record LinkTelegramDto(long TelegramUserId);

public record AuthResultDto(
    string Token,
    Guid AccountId,
    string Email,
    bool IsTelegramLinked,
    Guid? ActiveProfileId
);

public record AccountDto(
    Guid Id,
    string Email,
    bool IsTelegramLinked,
    long? TelegramUserId,
    Guid? ActiveProfileId,
    string? TelegramName,
    string? TelegramAvatarBase64,
    string Subscription,
    DateTimeOffset CreatedAt
);
