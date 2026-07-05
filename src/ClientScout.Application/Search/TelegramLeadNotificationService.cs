using System.Net.Http.Json;
using ClientScout.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClientScout.Application.Search;

public class TelegramLeadNotificationService : ILeadNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramLeadNotificationService> _logger;

    public TelegramLeadNotificationService(HttpClient httpClient, IConfiguration configuration, ILogger<TelegramLeadNotificationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyLeadAsync(Account account, JobLead lead, CancellationToken cancellationToken = default)
    {
        var botToken = _configuration["TELEGRAM_BOT_TOKEN"] ?? _configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken) || botToken == "dummy_token_for_dev" || account.TelegramUserId == null)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(lead.Title) ? "Новый заказ" : lead.Title;
        var summary = string.IsNullOrWhiteSpace(lead.AiSummary) ? lead.Content : lead.AiSummary;
        var text = $"""
Найден заказ

{title}

{summary}

Источник: {lead.SourceName}
AI: {lead.AiConfidence}%
""";

        try
        {
            await _httpClient.PostAsJsonAsync(
                $"https://api.telegram.org/bot{botToken}/sendMessage",
                new
                {
                    chat_id = account.TelegramUserId.Value,
                    text,
                    disable_web_page_preview = true
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send lead notification for lead {LeadId}", lead.Id);
        }
    }

    public async Task NotifySearchStoppedAsync(Account account, string reason, CancellationToken cancellationToken = default)
    {
        var botToken = _configuration["TELEGRAM_BOT_TOKEN"] ?? _configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken) || botToken == "dummy_token_for_dev" || account.TelegramUserId == null)
        {
            return;
        }

        var text = $"""
Поиск остановлен

{reason}
""";

        try
        {
            await _httpClient.PostAsJsonAsync(
                $"https://api.telegram.org/bot{botToken}/sendMessage",
                new
                {
                    chat_id = account.TelegramUserId.Value,
                    text,
                    disable_web_page_preview = true
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send search stopped notification for account {AccountId}", account.Id);
        }
    }
}
