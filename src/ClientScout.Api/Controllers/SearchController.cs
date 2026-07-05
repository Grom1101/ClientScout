using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Search;
using ClientScout.Application.Search.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchSettingsService _settingsService;
    private readonly ISearchIngestionService _ingestionService;
    private readonly IExchangeConnectionService _exchangeConnectionService;
    private readonly IKworkBrowserLoginService _kworkBrowserLoginService;
    private readonly IAppDbContext _dbContext;

    public SearchController(
        ISearchSettingsService settingsService,
        ISearchIngestionService ingestionService,
        IExchangeConnectionService exchangeConnectionService,
        IKworkBrowserLoginService kworkBrowserLoginService,
        IAppDbContext dbContext)
    {
        _settingsService = settingsService;
        _ingestionService = ingestionService;
        _exchangeConnectionService = exchangeConnectionService;
        _kworkBrowserLoginService = kworkBrowserLoginService;
        _dbContext = dbContext;
    }

    private Guid? AccountId
    {
        get
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings([FromQuery] Guid profileId, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();

        try
        {
            var settings = await _settingsService.GetSettingsAsync(profileId, AccountId.Value, cancellationToken);
            return Ok(settings);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSearchSettingsDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();

        try
        {
            var settings = await _settingsService.UpdateSettingsAsync(AccountId.Value, dto, cancellationToken);
            return Ok(settings);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("test-candidate")]
    public async Task<IActionResult> TestCandidate([FromBody] TestCandidateRequest request, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();

        try
        {
            var result = await _ingestionService.IngestTestCandidateAsync(AccountId.Value, request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] Guid profileId, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();

        var profile = await _dbContext.Profiles
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == profileId && p.AccountId == AccountId.Value, cancellationToken);

        if (profile == null) return Forbid();

        var settings = await _dbContext.SearchSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);

        var lastTelegram = await _dbContext.Sources
            .Where(s => s.ProfileId == profileId && s.Type == Domain.Enums.SourceType.Telegram)
            .MaxAsync(s => (DateTimeOffset?)s.LastScraped, cancellationToken);

        var kwork = await _dbContext.ExchangeConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProfileId == profileId && c.ExchangeType == Domain.Enums.ExchangeType.Kwork, cancellationToken);

        var lastCheck = new[] { lastTelegram, kwork?.LastCheckedAt }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();

        DateTimeOffset? nextRun = null;
        if (settings?.IsEnabled == true)
        {
            nextRun = lastCheck == default
                ? (DateTimeOffset?)DateTimeOffset.UtcNow
                : lastCheck.AddMinutes(settings.IntervalMinutes);
        }

        return Ok(new
        {
            isEnabled = settings?.IsEnabled ?? false,
            intervalMinutes = settings?.IntervalMinutes ?? 30,
            notificationsEnabled = settings?.NotificationsEnabled ?? true,
            botConnected = profile.Account?.TelegramUserId != null,
            lastCheckedAt = lastCheck == default ? (DateTimeOffset?)null : lastCheck,
            nextRunAt = nextRun
        });
    }

    [HttpGet("exchanges")]
    public async Task<IActionResult> GetExchanges([FromQuery] Guid profileId, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            return Ok(await _exchangeConnectionService.GetConnectionsAsync(profileId, AccountId.Value, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("exchanges/login/start")]
    public async Task<IActionResult> StartExchangeLogin([FromBody] ExchangeLoginStartDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            if (dto.ExchangeType == Domain.Enums.ExchangeType.Kwork)
            {
                return Ok(await _kworkBrowserLoginService.StartAsync(AccountId.Value, dto.ProfileId, cancellationToken));
            }

            return Ok(await _exchangeConnectionService.StartLoginAsync(AccountId.Value, dto, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("exchanges/login/{flowId:guid}")]
    public IActionResult GetExchangeLoginStatus(Guid flowId)
    {
        if (AccountId == null) return Unauthorized();
        return Ok(_kworkBrowserLoginService.GetStatus(flowId));
    }

    [HttpPost("exchanges/connect")]
    public async Task<IActionResult> ConnectExchange([FromBody] ConnectExchangeDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            return Ok(await _exchangeConnectionService.ConnectAsync(AccountId.Value, dto, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("exchanges/disconnect")]
    public async Task<IActionResult> DisconnectExchange([FromBody] DisconnectExchangeDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            return Ok(await _exchangeConnectionService.DisconnectAsync(AccountId.Value, dto, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
