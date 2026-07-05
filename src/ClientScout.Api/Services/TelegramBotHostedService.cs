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
    private readonly long _adminTelegramId = 1080953147;
    private readonly string _frontendUrl;
    private TelegramBotClient? _botClient;

    public TelegramBotHostedService(ILogger<TelegramBotHostedService> logger, IConfiguration configuration)
    {
        _logger = logger;
        // In production, token should be read from configuration, but for simplicity here we use the exact one
        _botToken = "8714330561:AAE0r-2KBOZjf_XuFKajJMb4p_910ZwIGK0"; 
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
            await _botClient.ReceiveAsync(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
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

        // Ensure this is our admin
        if (userId != _adminTelegramId)
        {
            // Optionally, we could send a message indicating no access
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
