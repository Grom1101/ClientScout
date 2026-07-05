using System.Threading.Tasks;

namespace ClientScout.Application.Telegram;

public interface ITelegramClientManager
{
    Task<string> SendCodeAsync(string userId, string phoneNumber);
    Task<(string? nextStep, long? telegramUserId, string? telegramName, string? telegramAvatarBase64)> VerifyCodeAsync(string userId, string phoneNumber, string code);
    Task<(string? nextStep, long? telegramUserId, string? telegramName, string? telegramAvatarBase64)> VerifyPasswordAsync(string userId, string password);
    Task<IReadOnlyList<TelegramReadMessageDto>> ReadLatestMessagesAsync(string userId, string url, int limit, int minMessageId = 0);
    Task SendTextMessageAsync(string userId, string url, string message);
    Task SendMessageWithAttachmentAsync(string userId, string url, string message, string filePath, string mimeType);
}
