using ClientScout.Application.Auth;
using ClientScout.Application.Leads;
using ClientScout.Application.Profiles;
using ClientScout.Application.Profiles;
using ClientScout.Application.Sources;
using ClientScout.Application.Outreach;
using Microsoft.Extensions.DependencyInjection;

namespace ClientScout.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TelegramAuthService>();
        services.AddScoped<JwtService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISourceService, SourceService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<LeadParsingService>();
        services.AddScoped<IOutreachService, OutreachService>();
        services.AddScoped<OutreachJobService>();
        
        return services;
    }
}
