using System.Security.Claims;
using ClientScout.Application.Leads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientScout.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LeadController : ControllerBase
{
    private readonly ILeadService _leadService;

    public LeadController(ILeadService leadService)
    {
        _leadService = leadService;
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

    [HttpGet("profile/{profileId}")]
    public async Task<IActionResult> GetLeads(Guid profileId, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var leads = await _leadService.GetLeadsByProfileAsync(profileId, AccountId.Value, cancellationToken);
            return Ok(leads);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("/api/leads/recent")]
    public async Task<IActionResult> GetRecent([FromQuery] Guid profileId, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var leads = await _leadService.GetRecentLeadsAsync(profileId, AccountId.Value, cancellationToken);
            return Ok(leads);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("/api/leads")]
    public async Task<IActionResult> GetHistory([FromQuery] Guid profileId, [FromQuery] int limit = 30, [FromQuery] int offset = 0, [FromQuery] string? aiFilter = null, CancellationToken cancellationToken = default)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var leads = await _leadService.GetLeadHistoryAsync(profileId, AccountId.Value, limit, offset, aiFilter, cancellationToken);
            return Ok(leads);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("/api/leads/count")]
    public async Task<IActionResult> CountLeads([FromQuery] Guid profileId, [FromQuery] string? aiFilter = null, CancellationToken cancellationToken = default)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var count = await _leadService.CountLeadsAsync(profileId, AccountId.Value, aiFilter, cancellationToken);
            return Ok(new { count });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id}/view")]
    public async Task<IActionResult> MarkAsViewed(Guid id, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        await _leadService.MarkAsViewedAsync(id, AccountId.Value, cancellationToken);
        return NoContent();
    }

    [HttpPut("/api/leads/{id}/viewed")]
    public async Task<IActionResult> PutMarkAsViewed(Guid id, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        await _leadService.MarkAsViewedAsync(id, AccountId.Value, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/hide")]
    public async Task<IActionResult> MarkAsHidden(Guid id, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        await _leadService.MarkAsHiddenAsync(id, AccountId.Value, cancellationToken);
        return NoContent();
    }

    [HttpDelete("/api/leads/{id}")]
    public async Task<IActionResult> DeleteLead(Guid id, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        await _leadService.MarkAsHiddenAsync(id, AccountId.Value, cancellationToken);
        return NoContent();
    }
}
