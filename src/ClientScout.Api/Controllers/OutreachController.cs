using System.Security.Claims;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Outreach;
using ClientScout.Domain.Enums;
using Hangfire;
using ClientScout.Application.Outreach.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientScout.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/outreach")]
public class OutreachController : ControllerBase
{
    private readonly IOutreachService _outreachService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IAppDbContext _dbContext;

    public OutreachController(IOutreachService outreachService, IBackgroundJobClient backgroundJobClient, IAppDbContext dbContext)
    {
        _outreachService = outreachService;
        _backgroundJobClient = backgroundJobClient;
        _dbContext = dbContext;
    }

    private Guid? AccountId
    {
        get
        {
            var sub = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                   ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    [HttpGet("profiles/{profileId}/templates")]
    public async Task<IActionResult> GetTemplates(Guid profileId, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var templates = await _outreachService.GetTemplatesAsync(profileId, AccountId.Value, cancellationToken);
            return Ok(templates);
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

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateMessageTemplateDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var template = await _outreachService.CreateTemplateAsync(AccountId.Value, dto, cancellationToken);
            return Ok(template);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("templates/{id}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateMessageTemplateDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var template = await _outreachService.UpdateTemplateAsync(id, AccountId.Value, dto, cancellationToken);
            return Ok(template);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("templates/{id}")]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            await _outreachService.DeleteTemplateAsync(id, AccountId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        var sessions = await _outreachService.GetSessionsAsync(AccountId.Value, cancellationToken);
        return Ok(sessions);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> AddSession([FromBody] CreateUserbotSessionDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        var session = await _outreachService.AddSessionAsync(AccountId.Value, dto, cancellationToken);
        return Ok(session);
    }

    [HttpGet("profiles/{profileId}/campaigns")]
    public async Task<IActionResult> GetCampaigns(Guid profileId, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var campaigns = await _outreachService.GetCampaignsAsync(profileId, AccountId.Value, cancellationToken);
            return Ok(campaigns);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("profiles/{profileId}/stats")]
    public async Task<IActionResult> GetStats(Guid profileId, [FromQuery] string period = "today", [FromQuery] int timezoneOffsetMinutes = 0, CancellationToken cancellationToken = default)
    {
        if (AccountId == null) return Unauthorized();

        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.AccountId == AccountId.Value, cancellationToken);
        if (!hasAccess) return Forbid();

        var nowLocal = DateTimeOffset.UtcNow.AddMinutes(-timezoneOffsetMinutes);
        var localStart = period.Equals("month", StringComparison.OrdinalIgnoreCase)
            ? new DateTimeOffset(nowLocal.Year, nowLocal.Month, 1, 0, 0, 0, nowLocal.Offset)
            : new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, nowLocal.Offset);
        var utcStart = localStart.AddMinutes(timezoneOffsetMinutes).ToUniversalTime();

        var logs = await _dbContext.OutreachLogs
            .AsNoTracking()
            .Include(l => l.Campaign)!.ThenInclude(c => c!.Profile)
            .Include(l => l.Campaign)!.ThenInclude(c => c!.Template)
            .Where(l => l.Campaign!.ProfileId == profileId && l.SentAt >= utcStart)
            .OrderBy(l => l.SentAt)
            .ToListAsync(cancellationToken);

        var leads = await _dbContext.JobLeads
            .AsNoTracking()
            .Where(l => l.ProfileId == profileId && l.FoundAt >= utcStart)
            .Select(l => new { l.Id, l.FoundAt })
            .ToListAsync(cancellationToken);

        var todayUtcStart = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, nowLocal.Offset).AddMinutes(timezoneOffsetMinutes).ToUniversalTime();
        var sentToday = logs.Count(l => l.Status == LogStatus.Sent && l.SentAt >= todayUtcStart);
        var leadsToday = leads.Count(l => l.FoundAt >= todayUtcStart);

        var activity = period.Equals("month", StringComparison.OrdinalIgnoreCase)
            ? Enumerable.Range(1, DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month))
                .Select(day => BuildPoint(logs, leads, day.ToString("00"), 
                    l => l.SentAt.AddMinutes(-timezoneOffsetMinutes).Day == day,
                    l => l.FoundAt.AddMinutes(-timezoneOffsetMinutes).Day == day))
                .ToList()
            : Enumerable.Range(0, 24)
                .Select(hour => BuildPoint(logs, leads, $"{hour:00}:00", 
                    l => l.SentAt.AddMinutes(-timezoneOffsetMinutes).Hour == hour,
                    l => l.FoundAt.AddMinutes(-timezoneOffsetMinutes).Hour == hour))
                .ToList();

        var recentLogs = await _dbContext.OutreachLogs
            .AsNoTracking()
            .Include(l => l.Campaign)!.ThenInclude(c => c!.Profile)
            .Include(l => l.Campaign)!.ThenInclude(c => c!.Template)
            .Where(l => l.Campaign!.ProfileId == profileId)
            .OrderByDescending(l => l.SentAt)
            .Take(10)
            .Select(l => new RecentOutreachLogDto(
                l.Id,
                l.ChatName ?? "Chat",
                l.Campaign!.Profile!.Name,
                l.MessageContent ?? l.Campaign.Template!.Content,
                l.Status,
                l.ErrorMessage,
                l.SentAt))
            .ToListAsync(cancellationToken);

        return Ok(new OutreachStatsDto(sentToday, leadsToday, activity, recentLogs));
    }

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateOutreachCampaignDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var campaign = await _outreachService.CreateCampaignAsync(AccountId.Value, dto, cancellationToken);
            return Ok(campaign);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("campaigns/{id}/status")]
    public async Task<IActionResult> UpdateCampaignStatus(Guid id, [FromQuery] CampaignStatus status, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var campaign = await _outreachService.UpdateCampaignStatusAsync(id, AccountId.Value, status, cancellationToken);
            if (status == CampaignStatus.Running)
            {
                _backgroundJobClient.Enqueue<OutreachJobService>(service => service.ProcessCampaignsAsync(CancellationToken.None));
            }

            return Ok(campaign);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static OutreachActivityPointDto BuildPoint(
        IEnumerable<ClientScout.Domain.Entities.OutreachLog> logs, 
        IEnumerable<dynamic> leads,
        string label, 
        Func<ClientScout.Domain.Entities.OutreachLog, bool> logPredicate,
        Func<dynamic, bool> leadPredicate)
    {
        var matchingLogs = logs.Where(logPredicate).ToList();
        var matchingLeads = leads.Where(leadPredicate).Count();
        return new OutreachActivityPointDto(
            label,
            matchingLogs.Count(l => l.Status == LogStatus.Sent),
            matchingLogs.Count(l => l.Status == LogStatus.Error),
            matchingLeads);
    }
}
