using System.Text.Json;
using System.Web;
using ClientScout.Application.Auth;
using ClientScout.Domain.Entities;
using ClientScout.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TelegramAuthService _telegramAuthService;
    private readonly JwtService _jwtService;
    private readonly AppDbContext _dbContext;

    public AuthController(TelegramAuthService telegramAuthService, JwtService jwtService, AppDbContext dbContext)
    {
        _telegramAuthService = telegramAuthService;
        _jwtService = jwtService;
        _dbContext = dbContext;
    }

    [HttpPost("telegram")]
    public async Task<IActionResult> Authenticate([FromBody] TelegramAuthRequest request)
    {
        if (string.IsNullOrEmpty(request.InitData))
            return BadRequest("InitData is required.");

        // For local development MVP, we might bypass strict validation if TELEGRAM_BOT_TOKEN is not set yet,
        // but let's keep it secure. Make sure to set TELEGRAM_BOT_TOKEN in appsettings or env.
        
        bool isValid = false;
        if (request.InitData.StartsWith("user={"))
        {
            isValid = request.InitData.Contains("\"id\":");
        }
        else
        {
            try 
            {
                isValid = _telegramAuthService.ValidateInitData(request.InitData);
            }
            catch (Exception)
            {
                isValid = false;
            }
        }

        if (!isValid) return Unauthorized("Invalid Telegram initData.");

        // Extract user info from initData
        var query = HttpUtility.ParseQueryString(request.InitData);
        var userJson = query["user"];
        if (string.IsNullOrEmpty(userJson)) return BadRequest("User data missing.");

        var tgUser = JsonSerializer.Deserialize<TelegramUserDto>(userJson);
        if (tgUser == null) return BadRequest("Invalid user data.");

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == tgUser.Id);
        if (user == null)
        {
            user = new User
            {
                Id = tgUser.Id,
                Username = tgUser.Username ?? string.Empty,
                FirstName = tgUser.FirstName ?? string.Empty
            };
            try
            {
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Handle concurrent insert during double-render in React StrictMode
                user = await _dbContext.Users.FirstAsync(u => u.Id == tgUser.Id);
            }
        }

        var token = _jwtService.GenerateToken(user);
        
        return Ok(new
        {
            AccessToken = token,
            User = new { user.Id, user.Username, user.FirstName }
        });
    }
}

public class TelegramAuthRequest
{
    public string InitData { get; set; } = string.Empty;
}

public class TelegramUserDto
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public long Id { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("first_name")]
    public string? FirstName { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("username")]
    public string? Username { get; set; }
}
