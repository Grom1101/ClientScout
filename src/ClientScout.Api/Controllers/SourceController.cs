using System.Security.Claims;
using ClientScout.Application.Sources;
using ClientScout.Application.Sources.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientScout.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SourceController : ControllerBase
{
    private readonly ISourceService _sourceService;

    public SourceController(ISourceService sourceService)
    {
        _sourceService = sourceService;
    }

    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    [HttpGet("profile/{profileId}")]
    public async Task<IActionResult> GetSources(Guid profileId, CancellationToken cancellationToken)
    {
        try
        {
            var sources = await _sourceService.GetSourcesByProfileAsync(profileId, UserId, cancellationToken);
            return Ok(sources);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateSource([FromBody] CreateSourceDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var source = await _sourceService.CreateSourceAsync(UserId, dto, cancellationToken);
            return Ok(source); // or CreatedAtAction if we add GetSourceById
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSource(Guid id, [FromBody] UpdateSourceDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var source = await _sourceService.UpdateSourceAsync(id, UserId, dto, cancellationToken);
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
        await _sourceService.DeleteSourceAsync(id, UserId, cancellationToken);
        return NoContent();
    }
}
