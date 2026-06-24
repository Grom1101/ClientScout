using ClientScout.Scrapers.Implementations;
using ClientScout.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ClientScout.Scrapers;

public static class DependencyInjection
{
    public static IServiceCollection AddScrapers(this IServiceCollection services)
    {
        services.AddScoped<ISourceScraper, TelegramScraper>();
        services.AddScoped<ISourceScraper, KworkScraper>();
        
        return services;
    }
}
