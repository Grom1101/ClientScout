using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using ClientScout.Api.Security;
using ClientScout.Infrastructure.Persistence;

namespace ClientScout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AdminController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetAdminStats()
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null || !AdminAccess.IsAdmin(account.Id, account.TelegramUserId, _configuration))
        {
            return Forbid();
        }

        // Stats calculation
        var today = DateTimeOffset.UtcNow.Date;
        var startOfMonth = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var totalLogs = await _context.AiUsageLogs.AsNoTracking().CountAsync();
        var errorCount = await _context.AiUsageLogs.AsNoTracking().CountAsync(l => l.StatusCode >= 400);
        
        // Calculate provider stats
        var providerStats = await _context.AiUsageLogs
            .AsNoTracking()
            .GroupBy(l => l.ProviderName)
            .Select(g => new
            {
                providerName = g.Key,
                calls = g.Count(),
                cost = g.Sum(l => l.CostUsd),
                inputTokens = g.Sum(l => l.InputTokens),
                outputTokens = g.Sum(l => l.OutputTokens),
                errors429 = g.Count(l => l.StatusCode == 429),
                fatalErrors = g.Count(l => l.StatusCode >= 400 && l.StatusCode != 429)
            })
            .ToListAsync();

        // Calculate model stats
        var modelStats = await _context.AiUsageLogs
            .AsNoTracking()
            .GroupBy(l => new { l.ProviderName, l.ModelName })
            .Select(g => new
            {
                providerName = g.Key.ProviderName,
                modelName = g.Key.ModelName,
                calls = g.Count(),
                successfulCalls = g.Count(l => l.StatusCode >= 200 && l.StatusCode < 300),
                cost = g.Sum(l => l.CostUsd),
                inputTokens = g.Sum(l => l.InputTokens),
                outputTokens = g.Sum(l => l.OutputTokens),
                errors429 = g.Count(l => l.StatusCode == 429),
                fatalErrors = g.Count(l => l.StatusCode >= 400 && l.StatusCode != 429)
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
