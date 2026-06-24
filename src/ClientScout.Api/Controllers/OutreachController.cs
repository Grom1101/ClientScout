using System.Security.Claims;
using ClientScout.Application.Outreach;
using ClientScout.Application.Outreach.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientScout.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OutreachController : ControllerBase
{
    private readonly IOutreachService _outreachService;

    public OutreachController(IOutreachService outreachService)
    {
        _outreachService = outreachService;
    }

    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var sessions = await _outreachService.GetSessionsAsync(UserId, cancellationToken);
        return Ok(sessions);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> AddSession([FromBody] CreateUserbotSessionDto dto, CancellationToken cancellationToken)
    {
        var session = await _outreachService.AddSessionAsync(UserId, dto, cancellationToken);
        return Ok(session);
    }

    [HttpGet("profiles/{profileId}/campaigns")]
    public async Task<IActionResult> GetCampaigns(Guid profileId, CancellationToken cancellationToken)
    {
        try
        {
            var campaigns = await _outreachService.GetCampaignsAsync(profileId, UserId, cancellationToken);
            return Ok(campaigns);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateOutreachCampaignDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var campaign = await _outreachService.CreateCampaignAsync(UserId, dto, cancellationToken);
            return Ok(campaign);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("campaigns/{id}/status")]
    public async Task<IActionResult> UpdateCampaignStatus(Guid id, [FromQuery] ClientScout.Domain.Enums.CampaignStatus status, CancellationToken cancellationToken)
    {
        try
        {
            await _outreachService.UpdateCampaignStatusAsync(id, UserId, status, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
