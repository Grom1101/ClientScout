using System.Security.Claims;
using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;

namespace ClientScout.Api.Security;

public static class AdminAccess
{
    public static bool IsAdmin(Guid? accountId, long? telegramUserId, IConfiguration configuration)
    {
        if (accountId.HasValue)
        {
            var adminAccountIds = configuration.GetSection("Admin:AccountIds").Get<string[]>() ?? [];
            if (adminAccountIds.Any(value =>
                    Guid.TryParse(value, out var configuredId) && configuredId == accountId.Value))
            {
                return true;
            }
        }

        if (telegramUserId.HasValue)
        {
            var adminTelegramIds = configuration.GetSection("Admin:TelegramUserIds").Get<string[]>() ?? [];
            if (adminTelegramIds.Any(value =>
                    long.TryParse(value, out var configuredId) && configuredId == telegramUserId.Value))
            {
                return true;
            }
        }

        return false;
    }

    public static Guid? GetAccountId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public static long? GetTelegramUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("telegramUserId");
        return long.TryParse(value, out var id) ? id : null;
    }
}

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly IConfiguration _configuration;

    public HangfireDashboardAuthorizationFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return AdminAccess.IsAdmin(
            AdminAccess.GetAccountId(httpContext.User),
            AdminAccess.GetTelegramUserId(httpContext.User),
            _configuration);
    }
}
