using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using ClientScout.Infrastructure.Persistence;

namespace ClientScout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private const long AdminTelegramId = 1080953147;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetAdminStats()
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null || account.TelegramUserId != AdminTelegramId)
        {
            return Forbid();
        }

        // Stats calculation
        var today = DateTimeOffset.UtcNow.Date;
        var startOfMonth = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var totalLogs = await _context.AiUsageLogs.CountAsync();
        var errorCount = await _context.AiUsageLogs.CountAsync(l => l.StatusCode >= 400);
        
        var logsByModel = await _context.AiUsageLogs
            .GroupBy(l => l.ModelName)
            .Select(g => new
            {
                Model = g.Key,
                Count = g.Count(),
                TotalCost = g.Sum(l => l.CostUsd),
                TotalInputTokens = g.Sum(l => l.InputTokens),
                TotalOutputTokens = g.Sum(l => l.OutputTokens)
            })
            .ToListAsync();

        var totalCost = logsByModel.Sum(x => x.TotalCost);
        var remainingBalance = 100.0m - totalCost;

        return Ok(new
        {
            success = true,
            totalRequests = totalLogs,
            errors = errorCount,
            totalCost,
            remainingBalance,
            usageByModel = logsByModel
        });
    }

    private Guid? GetAccountId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
