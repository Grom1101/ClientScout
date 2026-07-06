using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace ClientScout.Api.Services;

public class TelegramBotHostedService : BackgroundService
{
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly string _botToken;
    private readonly HashSet<long> _adminTelegramIds;
    private readonly string _frontendUrl;
    private TelegramBotClient? _botClient;

    public TelegramBotHostedService(ILogger<TelegramBotHostedService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _botToken = configuration["Telegram:BotToken"] ?? configuration["TELEGRAM_BOT_TOKEN"] ?? string.Empty;
        _adminTelegramIds = (configuration.GetSection("Admin:TelegramUserIds").Get<string[]>() ?? [])
            .Select(value => long.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();
        _frontendUrl = configuration["FrontendUrl"] ?? "https://client-scout.com";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_botToken))
        {
            _logger.LogWarning("Telegram Bot token is not configured. Hosted service will not start.");
            return;
        }

        _botClient = new TelegramBotClient(_botToken);
        
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        _logger.LogInformation("Starting Telegram Bot listener...");

        try
        {
            // Clear any webhook before starting polling, otherwise ReceiveAsync will fail
            await _botClient.DeleteWebhook(cancellationToken: stoppingToken);

            await _botClient.ReceiveAsync(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in TelegramBotHostedService");
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // We only process text messages
        if (update.Message is not { } message) return;
        if (message.Text is not { } messageText) return;

        long chatId = message.Chat.Id;
        long userId = message.From?.Id ?? 0;

        if (!_adminTelegramIds.Contains(userId))
        {
            return;
        }

        if (messageText.StartsWith("/admin") || messageText.StartsWith("/stats"))
        {
            var webAppInfo = new WebAppInfo
            {
                Url = $"{_frontendUrl}/admin" // Use exact admin route
            };

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithWebApp("📊 Открыть панель управления", webAppInfo)
                }
            });

            await botClient.SendMessage(
                chatId: chatId,
                text: "Панель администратора готова к работе.",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken
            );
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram API Error");
        return Task.CompletedTask;
    }
}
