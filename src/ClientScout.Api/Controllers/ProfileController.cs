using System.Security.Claims;
using ClientScout.Application.Profiles;
using ClientScout.Application.Profiles.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientScout.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    [HttpGet]
    public async Task<IActionResult> GetProfiles(CancellationToken cancellationToken)
    {
        var profiles = await _profileService.GetProfilesAsync(UserId, cancellationToken);
        return Ok(profiles);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _profileService.GetProfileAsync(id, UserId, cancellationToken);
        if (profile == null) return NotFound();
        
        return Ok(profile);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileDto dto, CancellationToken cancellationToken)
    {
        var profile = await _profileService.CreateProfileAsync(UserId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetProfile), new { id = profile.Id }, profile);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdateProfileDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profileService.UpdateProfileAsync(id, UserId, dto, cancellationToken);
            return Ok(profile);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken cancellationToken)
    {
        await _profileService.DeleteProfileAsync(id, UserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/default")]
    public async Task<IActionResult> SetDefaultProfile(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _profileService.SetDefaultProfileAsync(id, UserId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
