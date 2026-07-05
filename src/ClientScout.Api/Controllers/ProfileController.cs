using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Profiles;
using ClientScout.Application.Profiles.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientScout.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/profiles")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
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

    [HttpGet]
    public async Task<IActionResult> GetProfiles(CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        var profiles = await _profileService.GetProfilesAsync(AccountId.Value, cancellationToken);
        return Ok(profiles);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var profile = await _profileService.CreateProfileAsync(AccountId.Value, dto, cancellationToken);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdateProfileDto dto, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            var profile = await _profileService.UpdateProfileAsync(id, AccountId.Value, dto, cancellationToken);
            return Ok(profile);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            await _profileService.SetDefaultProfileAsync(id, AccountId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken cancellationToken)
    {
        if (AccountId == null) return Unauthorized();
        try
        {
            await _profileService.DeleteProfileAsync(id, AccountId.Value, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
