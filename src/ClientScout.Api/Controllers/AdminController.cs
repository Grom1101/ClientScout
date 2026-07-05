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
        
        // Calculate provider stats
        var providerStats = await _context.AiUsageLogs
            .GroupBy(l => l.ProviderName)
            .Select(g => new
            {
                providerName = g.Key,
                calls = g.Count(),
                cost = g.Sum(l => l.CostUsd),
                inputTokens = g.Sum(l => l.InputTokens),
                outputTokens = g.Sum(l => l.OutputTokens),
                errors429 = g.Count(l => l.StatusCode == 429)
            })
            .ToListAsync();

        // Calculate model stats
        var modelStats = await _context.AiUsageLogs
            .GroupBy(l => new { l.ProviderName, l.ModelName })
            .Select(g => new
            {
                providerName = g.Key.ProviderName,
                modelName = g.Key.ModelName,
                calls = g.Count(),
                cost = g.Sum(l => l.CostUsd),
                inputTokens = g.Sum(l => l.InputTokens),
                outputTokens = g.Sum(l => l.OutputTokens),
                errors429 = g.Count(l => l.StatusCode == 429)
            })
            .ToListAsync();

        var totalCostUsd = providerStats.Sum(x => x.cost);
        var remainingBudgetUsd = 100.0m - totalCostUsd;
        var totalCalls = providerStats.Sum(x => x.calls);
        var total429Errors = providerStats.Sum(x => x.errors429);

        return Ok(new
        {
            totalCalls,
            total429Errors,
            totalCostUsd,
            remainingBudgetUsd,
            providerStats,
            modelStats
        });
    }

    private Guid? GetAccountId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
