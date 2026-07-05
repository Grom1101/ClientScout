namespace ClientScout.Application.Telegram;

public record TelegramReadMessageDto(
    int MessageId,
    string Text,
    DateTimeOffset Date,
    string OriginalUrl,
    string? AuthorUrl,
    string? TopicName = null);
