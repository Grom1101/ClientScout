using System;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Auth;
using ClientScout.Application.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ClientScout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TelegramAuthController : ControllerBase
{
    private readonly ITelegramClientManager _clientManager;
    private readonly IAuthService _authService;

    public TelegramAuthController(ITelegramClientManager clientManager, IAuthService authService)
    {
        _clientManager = clientManager;
        _authService = authService;
    }

    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest request)
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        try
        {
            Console.WriteLine($"[TELEGRAM AUTH] Sending code for account {accountId.Value} to {MaskPhone(request.PhoneNumber)}");
            var result = await _clientManager.SendCodeAsync(accountId.Value.ToString(), request.PhoneNumber);
            Console.WriteLine($"[TELEGRAM AUTH] SendCode result for account {accountId.Value}: {result}");
            return Ok(new { success = true, result });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TELEGRAM AUTH] SendCode failed for account {accountId.Value}: {ex}");
            return BadRequest(new { success = false, message = ex.Message });
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
            return BadRequest(new { success = false, message = ex.Message });
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
            return BadRequest(new { success = false, message = ex.Message });
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
