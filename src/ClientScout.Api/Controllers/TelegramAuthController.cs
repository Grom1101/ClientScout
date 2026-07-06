using System;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Auth;
using ClientScout.Application.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;

namespace ClientScout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("Auth")]
public class TelegramAuthController : ControllerBase
{
    private readonly ITelegramClientManager _clientManager;
    private readonly IAuthService _authService;
    private readonly ILogger<TelegramAuthController> _logger;

    public TelegramAuthController(
        ITelegramClientManager clientManager,
        IAuthService authService,
        ILogger<TelegramAuthController> logger)
    {
        _clientManager = clientManager;
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest request)
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        try
        {
            _logger.LogInformation("Sending Telegram code for account {AccountId} to {MaskedPhone}",
                accountId.Value,
                MaskPhone(request.PhoneNumber));
            var result = await _clientManager.SendCodeAsync(accountId.Value.ToString(), request.PhoneNumber);
            _logger.LogInformation("Telegram code request completed for account {AccountId} with next step {NextStep}",
                accountId.Value,
                result);
            return Ok(new { success = true, result });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram send-code failed for account {AccountId}", accountId.Value);
            return BadRequest(new { success = false, message = NormalizeTelegramError(ex) });
        }
    }

    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request, CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        try
        {
            var (nextStep, telegramUserId, telegramName, telegramAvatarBase64) = await _clientManager.VerifyCodeAsync(accountId.Value.ToString(), request.PhoneNumber, request.Code);
            
            if (nextStep == null && telegramUserId.HasValue)
            {
                await _authService.LinkTelegramAsync(accountId.Value, telegramUserId.Value, telegramName, telegramAvatarBase64, cancellationToken);
            }

            return Ok(new
            {
                success = nextStep == null,
                requiresPassword = string.Equals(nextStep, "password", StringComparison.OrdinalIgnoreCase),
                result = nextStep
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram verify-code failed for account {AccountId}", accountId.Value);
            return BadRequest(new { success = false, message = NormalizeTelegramError(ex) });
        }
    }

    [HttpPost("verify-password")]
    public async Task<IActionResult> VerifyPassword([FromBody] VerifyPasswordRequest request, CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        try
        {
            var (nextStep, telegramUserId, telegramName, telegramAvatarBase64) = await _clientManager.VerifyPasswordAsync(accountId.Value.ToString(), request.Password);
            
            if (nextStep == null && telegramUserId.HasValue)
            {
                await _authService.LinkTelegramAsync(accountId.Value, telegramUserId.Value, telegramName, telegramAvatarBase64, cancellationToken);
            }

            return Ok(new
            {
                success = nextStep == null,
                result = nextStep
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram verify-password failed for account {AccountId}", accountId.Value);
            return BadRequest(new { success = false, message = NormalizeTelegramError(ex) });
        }
    }

    private Guid? GetAccountId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length <= 4) return "****";
        return new string('*', Math.Max(0, phone.Length - 4)) + phone[^4..];
    }

    private static string NormalizeTelegramError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("PHONE_CODE_INVALID", StringComparison.OrdinalIgnoreCase)) return "PHONE_CODE_INVALID";
        if (message.Contains("PHONE_CODE_EXPIRED", StringComparison.OrdinalIgnoreCase)) return "PHONE_CODE_EXPIRED";
        if (message.Contains("PASSWORD_HASH_INVALID", StringComparison.OrdinalIgnoreCase)) return "PASSWORD_HASH_INVALID";
        if (message.Contains("FLOOD_WAIT", StringComparison.OrdinalIgnoreCase)) return "FLOOD_WAIT";
        if (message.Contains("Auth session not found", StringComparison.OrdinalIgnoreCase)) return "AUTH_SESSION_NOT_FOUND";
        return "TELEGRAM_AUTH_FAILED";
    }
}

public class SendCodeRequest
{
    public string UserId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class VerifyCodeRequest
{
    public string UserId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class VerifyPasswordRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
