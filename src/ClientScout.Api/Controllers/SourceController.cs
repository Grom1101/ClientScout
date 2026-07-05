using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Sources;
using ClientScout.Application.Sources.Models;
using ClientScout.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sources")]
public class SourceController : ControllerBase
{
    private readonly ISourceService _sourceService;
    private readonly IAppDbContext _dbContext;

    public SourceController(ISourceService sourceService, IAppDbContext dbContext)
    {
        _sourceService = sourceService;
        _dbContext = dbContext;
    }

    /// <summary>Get accountId from JWT sub claim.</summary>
    private Guid? AccountId
    {
        get
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    /// <summary>Get Telegram user ID linked to this account from DB.</summary>
    private async Task<long?> GetTelegramUserIdAsync(CancellationToken ct)
    {
        if (AccountId == null) return null;
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == AccountId.Value, ct);
        return account?.TelegramUserId;
    }

    [HttpGet]
    public async Task<IActionResult> GetSources([FromQuery] int? purpose, [FromQuery] Guid? profileId, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();

        // If profileId not specified, use the account's active profile
        Guid resolvedProfileId;
        if (profileId.HasValue)
        {
            resolvedProfileId = profileId.Value;
        }
        else
        {
            var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == AccountId.Value, cancellationToken);
            if (account?.ActiveProfileId == null) return BadRequest(new { message = "No active profile set." });
            resolvedProfileId = account.ActiveProfileId.Value;
        }

        try
        {
            var sources = await _sourceService.GetSourcesByProfileAsync(resolvedProfileId, AccountId.Value, cancellationToken);
            if (purpose.HasValue)
                sources = sources.FindAll(s => s.Purpose == purpose.Value);

            return Ok(sources);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateSource(
        [FromBody] CreateSourceDto dto,
        [FromHeader(Name = "X-User-Id")] string? telegramUserIdHeader,
        CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();

        try
        {
            if (dto.Type == SourceType.Telegram)
            {
                var validation = await _sourceService.ValidateSourceAsync(AccountId.Value, dto.Url, dto.Purpose, cancellationToken);
                if (!validation.IsValid)
                {
                    return BadRequest(new { message = validation.ErrorCode ?? "INVALID_CHAT" });
                }
            }

            var source = await _sourceService.CreateSourceAsync(AccountId.Value, dto, cancellationToken);
            return Ok(source);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex) when (ex.Message == "DUPLICATE_CHAT")
        {
            return BadRequest(new { message = "DUPLICATE_CHAT" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CREATE SOURCE ERROR] {ex}");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSource(Guid id, [FromBody] UpdateSourceDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var source = await _sourceService.UpdateSourceAsync(id, AccountId.Value, dto, cancellationToken);
            return Ok(source);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSource(Guid id, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        await _sourceService.DeleteSourceAsync(id, AccountId.Value, cancellationToken);
        return NoContent();
    }

    [HttpGet("validate")]
    public async Task<IActionResult> ValidateSource([FromQuery] string url, [FromQuery] int purpose = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { message = "URL is required" });

        if (AccountId == null) return Unauthorized();

        // Get linked Telegram user ID from this account
        var telegramUserId = await GetTelegramUserIdAsync(cancellationToken);
        if (telegramUserId == null)
            return BadRequest(new { message = "NOT_AUTHORIZED" });

        try
        {
            var result = await _sourceService.ValidateSourceAsync(AccountId.Value, url, purpose, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VALIDATE ERROR] {ex}");
            return BadRequest(new { message = ex.Message });
        }
    }

    private static long ParseTelegramUserId(string? userIdStr) =>
        !string.IsNullOrWhiteSpace(userIdStr) && long.TryParse(userIdStr, out var id) ? id : 0;
}
