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

    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    [HttpGet("profile/{profileId}")]
    public async Task<IActionResult> GetLeads(Guid profileId, CancellationToken cancellationToken)
    {
        try
        {
            var leads = await _leadService.GetLeadsByProfileAsync(profileId, UserId, cancellationToken);
            return Ok(leads);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id}/view")]
    public async Task<IActionResult> MarkAsViewed(Guid id, CancellationToken cancellationToken)
    {
        await _leadService.MarkAsViewedAsync(id, UserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/hide")]
    public async Task<IActionResult> MarkAsHidden(Guid id, CancellationToken cancellationToken)
    {
        await _leadService.MarkAsHiddenAsync(id, UserId, cancellationToken);
        return NoContent();
    }
}
