using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Sources.Models;
using ClientScout.Application.Telegram;
using Microsoft.Extensions.Configuration;
using TL;
using WTelegram;

namespace ClientScout.Scrapers.Implementations;

public class TelegramClientManager : ITelegramClientManager, ITelegramValidator
{
    private readonly string _apiId;
    private readonly string _apiHash;
    private readonly ConcurrentDictionary<string, Client> _clients = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _clientLocks = new();

    public TelegramClientManager(IConfiguration configuration)
    {
        _apiId = configuration["Telegram:ApiId"] ?? throw new ArgumentException("Telegram:ApiId is missing");
        _apiHash = configuration["Telegram:ApiHash"] ?? throw new ArgumentException("Telegram:ApiHash is missing");
    }

    private string ConfigProvider(string userId, string what)
    {
        return what switch
        {
            "api_id" => _apiId,
            "api_hash" => _apiHash,
            "session_pathname" => GetSessionPath(userId),
            _ => null
        };
    }

    private static string GetSessionPath(string userId) => $"session_{userId}.dat";

    private async Task<Client> GetClientAsync(string userId)
    {
        var clientLock = _clientLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await clientLock.WaitAsync();
        try
        {
            return await GetClientUnlockedAsync(userId);
        }
        finally
        {
            clientLock.Release();
        }
    }

    // Must be called while holding _clientLocks[userId]
    private async Task<Client> GetClientUnlockedAsync(string userId)
    {
        if (_clients.TryGetValue(userId, out var existingClient) && existingClient.User != null)
        {
            return existingClient;
        }

        if (existingClient != null)
        {
            _clients.TryRemove(userId, out _);
            existingClient.Dispose();
        }

        var client = new Client(what => ConfigProvider(userId, what));
        try
        {
            await client.ConnectAsync();

            if (client.User == null)
            {
                try
                {
                    var users = await client.Users_GetUsers(new InputUserBase[] { new InputUserSelf() });
                    if (users == null || users.Length == 0)
                    {
                        client.Dispose();
                        throw new Exception("NOT_AUTHORIZED");
                    }
                }
                catch
                {
                    client.Dispose();
                    throw new Exception("NOT_AUTHORIZED");
                }
            }

            _clients.TryAdd(userId, client);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }


    public async Task<string> SendCodeAsync(string userId, string phoneNumber)
    {
        var clientLock = _clientLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await clientLock.WaitAsync();
        try
        {
            if (_clients.TryRemove(userId, out var oldClient))
            {
                oldClient.Dispose();
            }

            var client = new Client(what => ConfigProvider(userId, what));
            try
            {
                var result = await client.Login(phoneNumber); // Initiates sending code
                _clients.AddOrUpdate(userId, client, (_, existing) =>
                {
                    if (!ReferenceEquals(existing, client))
                    {
                        existing.Dispose();
                    }

                    return client;
                });
                return result; // "verification_code" usually
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
        finally
        {
            clientLock.Release();
        }
    }

    public async Task<(string? nextStep, long? telegramUserId, string? telegramName, string? telegramAvatarBase64)> VerifyCodeAsync(string userId, string phoneNumber, string code)
    {
        var clientLock = _clientLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await clientLock.WaitAsync();
        try
        {
            if (!_clients.TryGetValue(userId, out var client))
            {
                throw new Exception("Auth session not found. Call SendCode first.");
            }

            var result = await client.Login(code); // Verify code
        
            if (result == null && client.User != null)
            {
                var (name, avatarBase64) = await GetProfileInfoAsync(client);
                return (result, client.User.id, name, avatarBase64);
            }
        
            return (result, null, null, null);
        }
        finally
        {
            clientLock.Release();
        }
    }

    public async Task<(string? nextStep, long? telegramUserId, string? telegramName, string? telegramAvatarBase64)> VerifyPasswordAsync(string userId, string password)
    {
        var clientLock = _clientLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await clientLock.WaitAsync();
        try
        {
            if (!_clients.TryGetValue(userId, out var client))
            {
                throw new Exception("Auth session not found. Call SendCode first.");
            }

            var result = await client.Login(password);
        
            if (result == null && client.User != null)
            {
                var (name, avatarBase64) = await GetProfileInfoAsync(client);
                return (result, client.User.id, name, avatarBase64);
            }
        
            return (result, null, null, null);
        }
        finally
        {
            clientLock.Release();
        }
    }

    private async Task<(string? name, string? avatarBase64)> GetProfileInfoAsync(Client client)
    {
        if (client.User == null) return (null, null);

        var name = (client.User.first_name + " " + client.User.last_name).Trim();
        string? avatarBase64 = null;

        try
        {
            if (client.User.photo is TL.UserProfilePhoto photo)
            {
                using var ms = new System.IO.MemoryStream();
                await client.DownloadProfilePhotoAsync(client.User, ms);
                var bytes = ms.ToArray();
                if (bytes.Length > 0)
                {
                    avatarBase64 = "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
                }
            }
        }
        catch (Exception ex)
        {
            // Ignore avatar download failure
            Console.WriteLine($"Failed to download avatar: {ex.Message}");
        }

        return (name, avatarBase64);
    }

    public async Task<ValidateSourceResponseDto> ValidateChatAsync(string userId, string url)
    {
        var client = await GetClientAsync(userId);
        
        var target = ExtractTelegramTarget(url);
        if (target.Username == null)
        {
            throw new Exception("Invalid Telegram URL");
        }

        // Try to resolve username
        var resolved = await client.Contacts_ResolveUsername(target.Username);
        if (resolved == null || resolved.chats.Count == 0)
        {
            throw new Exception("NOT_FOUND");
        }

        var chatBase = resolved.chats.Values.FirstOrDefault();
        if (chatBase is not Channel channel)
        {
            throw new Exception("Target is not a channel/group");
        }

        // Check if we are a member
        if (channel.flags.HasFlag(Channel.Flags.left))
        {
            throw new Exception("NOT_A_MEMBER");
        }

        // Check if read-only
        if (channel.flags.HasFlag(Channel.Flags.broadcast))
        {
            if (channel.admin_rights == null || !channel.admin_rights.flags.HasFlag(ChatAdminRights.Flags.post_messages))
            {
                throw new Exception("READ_ONLY");
            }
        }

        bool isAdmin = channel.admin_rights != null;
        if (!isAdmin && channel.banned_rights != null && 
            (channel.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_messages) || channel.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_plain)))
        {
            throw new Exception("READ_ONLY");
        }

        if (!isAdmin && channel.default_banned_rights != null && 
            (channel.default_banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_messages) || channel.default_banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_plain)))
        {
            throw new Exception("READ_ONLY");
        }

        bool isForum = channel.flags.HasFlag(Channel.Flags.forum);

        var response = new ValidateSourceResponseDto
        {
            IsValid = true,
            IsForum = isForum,
            Topics = new System.Collections.Generic.List<TopicDto>()
        };

        if (isForum)
        {
            try
            {
                var topicsResult = await client.Channels_GetAllForumTopics(channel);
                if (topicsResult != null && topicsResult.topics != null)
                {
                    foreach (var topic in topicsResult.topics)
                    {
                        if (topic is ForumTopic ft)
                        {
                            var canWrite = !ft.flags.HasFlag(ForumTopic.Flags.closed) &&
                                           !ft.flags.HasFlag(ForumTopic.Flags.hidden);

                            // General topic (ID 1) often restricts normal users from sending messages
                            // even if not explicitly "closed". If we are not an admin, we assume it's read-only
                            // to match Telegram's default forum behavior.
                            if (ft.id == 1 && !isAdmin)
                            {
                                canWrite = false;
                            }

                            response.Topics.Add(new TopicDto
                            {
                                Id = ft.id.ToString(),
                                Name = ft.title,
                                CanWrite = canWrite
                            });
                        }
                    }
                }

                if (!response.Topics.Any(t => t.Id == "1"))
                {
                    // "General" topic is implicitly ID 1 and usually not returned by GetAllForumTopics.
                    response.Topics.Insert(0, new TopicDto
                    {
                        Id = "1",
                        Name = "General",
                        CanWrite = false // Default to false if we cannot explicitly verify it via GetAllForumTopics
                    });
                }

                if (target.TopicId != null)
                {
                    var topic = response.Topics.FirstOrDefault(t => t.Id == target.TopicId);
                    if (topic == null)
                    {
                        throw new Exception("NOT_FOUND");
                    }

                    if (!topic.CanWrite)
                    {
                        throw new Exception("READ_ONLY");
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message is "NOT_FOUND" or "READ_ONLY") throw;

                Console.WriteLine($"Failed to fetch topics: {ex.Message}");
            }
        }

        return response;
    }

    public async Task SendTextMessageAsync(string userId, string url, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is empty.", nameof(message));
        }

        var client = await GetClientAsync(userId);
        var target = ExtractTelegramTarget(url);
        if (target.Username == null)
        {
            throw new Exception("Invalid Telegram URL");
        }

        var resolved = await client.Contacts_ResolveUsername(target.Username);
        if (resolved == null || resolved.chats.Count == 0)
        {
            throw new Exception("NOT_FOUND");
        }

        var chatBase = resolved.chats.Values.FirstOrDefault();
        if (chatBase is not Channel channel)
        {
            throw new Exception("Target is not a channel/group");
        }

        var topicId = int.TryParse(target.TopicId, out var parsedTopicId) ? parsedTopicId : 0;
        await client.SendMessageAsync(channel, message, reply_to_msg_id: topicId);
    }

    public async Task<IReadOnlyList<TelegramReadMessageDto>> ReadLatestMessagesAsync(string userId, string url, int limit, int minMessageId = 0)
    {
        var target = ExtractTelegramTarget(url);
        if (target.Username == null)
        {
            throw new Exception("Invalid Telegram URL");
        }

        // Use a single lock covering both client retrieval and the API calls
        // to prevent WTelegram "You must connect to Telegram first" race condition
        // when multiple sources are scanned in parallel for the same user.
        var clientLock = _clientLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await clientLock.WaitAsync();
        try
        {
            var client = await GetClientUnlockedAsync(userId);

            var resolved = await client.Contacts_ResolveUsername(target.Username);
            if (resolved == null || resolved.chats.Count == 0 || resolved.Chat == null)
            {
                throw new Exception("NOT_FOUND");
            }

            if (resolved.Chat is not Channel channel)
            {
                throw new Exception("Target is not a channel/group");
            }

            string? topicName = null;
            Messages_MessagesBase history;
            if (target.TopicId != null && int.TryParse(target.TopicId, out var topicMsgId))
            {
                // Forum topic: Messages_GetHistory doesn't support topic filtering.
                // Use Messages_GetReplies with msg_id (topic top_msg_id) to fetch messages from a specific topic.
                history = await client.Messages_GetReplies(channel,
                    msg_id: topicMsgId,
                    limit: Math.Clamp(limit, 1, 100),
                    min_id: minMessageId);

                try
                {
                    var msgRes = await client.Channels_GetMessages(channel, new InputMessage[] { new InputMessageID { id = topicMsgId } });
                    if (msgRes.Messages.FirstOrDefault() is MessageService msgService && msgService.action is MessageActionTopicCreate topicCreate)
                    {
                        topicName = topicCreate.title;
                    }
                }
                catch { }
            }
            else
            {
                // Use Messages_GetHistory for regular chats/channels
                history = await client.Messages_GetHistory(channel,
                    limit: Math.Clamp(limit, 1, 100),
                    min_id: minMessageId);
            }

            var messages = history.Messages
                .OfType<Message>()
                .Where(message => !string.IsNullOrWhiteSpace(message.message))
                .OrderBy(message => message.id)
                .Select(message => new TelegramReadMessageDto(
                    message.id,
                    message.message,
                    message.date,
                    $"https://t.me/{target.Username}/{message.id}",
                    TryBuildAuthorUrl(message),
                    topicName))
                .ToList();

            Console.WriteLine($"[DEBUG] Final parsed messages count: {messages.Count}");
            return messages;
        }
        finally
        {
            clientLock.Release();
        }
    }

    private static bool IsTopicMessage(Message message, string topicId)
    {
        if (!int.TryParse(topicId, out var parsedTopicId))
        {
            return true;
        }

        return message.reply_to is MessageReplyHeader header && header.reply_to_top_id == parsedTopicId;
    }

    private static string? TryBuildAuthorUrl(Message message)
    {
        return message.from_id is PeerUser user ? $"tg://user?id={user.user_id}" : null;
    }

    public async Task SendMessageWithAttachmentAsync(string userId, string url, string message, string filePath, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is empty.", nameof(message));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Attachment file was not found.", filePath);
        }

        var client = await GetClientAsync(userId);
        var target = ExtractTelegramTarget(url);
        if (target.Username == null)
        {
            throw new Exception("Invalid Telegram URL");
        }

        var resolved = await client.Contacts_ResolveUsername(target.Username);
        if (resolved == null || resolved.chats.Count == 0 || resolved.Chat == null)
        {
            throw new Exception("NOT_FOUND");
        }

        if (resolved.Chat is not Channel channel)
        {
            throw new Exception("Target is not a channel/group");
        }

        var uploadedFile = await client.UploadFileAsync(filePath);
        var mediaType = mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "photo" : mimeType;
        var topicId = int.TryParse(target.TopicId, out var parsedTopicId) ? parsedTopicId : 0;

        await client.SendMediaAsync(channel, message, uploadedFile, mediaType, reply_to_msg_id: topicId);
    }

    private static TelegramTarget ExtractTelegramTarget(string url)
    {
        var value = url.Trim();
        if (value.StartsWith("@", StringComparison.Ordinal))
        {
            return new TelegramTarget(value[1..], null);
        }

        if (value.StartsWith("t.me/", StringComparison.OrdinalIgnoreCase))
        {
            value = $"https://{value}";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (!uri.Host.Equals("t.me", StringComparison.OrdinalIgnoreCase) &&
             !uri.Host.Equals("www.t.me", StringComparison.OrdinalIgnoreCase)))
        {
            return new TelegramTarget(null, null);
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || segments[0].Equals("c", StringComparison.OrdinalIgnoreCase))
        {
            return new TelegramTarget(null, null);
        }

        var topicId = segments.Length > 1 && int.TryParse(segments[1], out _) ? segments[1] : null;
        return new TelegramTarget(segments[0], topicId);
    }

    private sealed record TelegramTarget(string? Username, string? TopicId);
}
