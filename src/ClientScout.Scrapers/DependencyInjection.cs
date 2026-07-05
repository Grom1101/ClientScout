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
        
        services.AddSingleton<ClientScout.Scrapers.Implementations.TelegramClientManager>();
        services.AddSingleton<ClientScout.Application.Telegram.ITelegramClientManager>(p => p.GetRequiredService<ClientScout.Scrapers.Implementations.TelegramClientManager>());
        services.AddSingleton<ClientScout.Application.Telegram.ITelegramValidator>(p => p.GetRequiredService<ClientScout.Scrapers.Implementations.TelegramClientManager>());
        
        return services;
    }
}
