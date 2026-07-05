using ClientScout.Application.Auth;
using ClientScout.Application.Leads;
using ClientScout.Application.Profiles;
using ClientScout.Application.Sources;
using ClientScout.Application.Search;
using ClientScout.Application.Outreach;
using Microsoft.Extensions.DependencyInjection;

namespace ClientScout.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TelegramAuthService>();
        services.AddScoped<JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISourceService, SourceService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<ISearchSettingsService, SearchSettingsService>();
        services.AddOptions<AiProviderPoolOptions>()
            .BindConfiguration("AI");
        services.AddHttpClient<AiJsonClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5)
        });
        services.AddScoped<IAiSearchExpansionService, AiSearchExpansionService>();
        services.AddScoped<IAiLeadClassifier, AiLeadClassifier>();
        services.AddScoped<ISearchCandidateFilter, SearchCandidateFilter>();
        services.AddScoped<ISearchIngestionService, SearchIngestionService>();
        services.AddScoped<IExchangeConnectionService, ExchangeConnectionService>();
        services.AddScoped<ISearchJobService, SearchJobService>();
        services.AddSingleton<IKworkBrowserLoginService, KworkBrowserLoginService>();
        services.AddHttpClient<ILeadNotificationService, TelegramLeadNotificationService>();
        services.AddHttpClient("Kwork");
        services.AddScoped<LeadParsingService>();
        services.AddScoped<IOutreachService, OutreachService>();
        services.AddScoped<OutreachJobService>();

        return services;
    }
}
