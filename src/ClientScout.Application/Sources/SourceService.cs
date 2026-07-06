using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Sources.Models;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Application.Sources;

public class SourceService : ISourceService
{
    private static readonly HttpClient TelegramMetadataClient = new();
    private readonly IAppDbContext _dbContext;
    private readonly ClientScout.Application.Telegram.ITelegramValidator _telegramValidator;

    public SourceService(IAppDbContext dbContext, ClientScout.Application.Telegram.ITelegramValidator telegramValidator)
    {
        _dbContext = dbContext;
        _telegramValidator = telegramValidator;
    }

    public async Task<List<SourceDto>> GetSourcesByProfileAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var sources = await _dbContext.Sources
            .AsNoTracking()
            .Where(s => s.ProfileId == profileId)
            .Where(s => s.Type != SourceType.Kwork)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sources.Select(MapToDto).ToList();
    }

    public async Task<SourceDto> CreateSourceAsync(Guid accountId, CreateSourceDto dto, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == dto.ProfileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var metadata = await BuildMetadataAsync(dto, cancellationToken);
        var normalizedUrl = NormalizeSourceUrl(dto.Url);
        var existingSources = await _dbContext.Sources
            .AsNoTracking()
            .Where(s => s.ProfileId == dto.ProfileId)
            .ToListAsync(cancellationToken);

        if (existingSources.Any(s =>
            MapToDto(s).Purpose == dto.Purpose &&
            NormalizeSourceUrl(s.Url) == normalizedUrl))
        {
            throw new InvalidOperationException("DUPLICATE_CHAT");
        }

        var source = new Source
        {
            Id = Guid.NewGuid(),
            ProfileId = dto.ProfileId,
            Type = dto.Type,
            Name = string.IsNullOrWhiteSpace(metadata.Name) ? dto.Name : metadata.Name,
            Url = dto.Url,
            ChatId = dto.ChatId,
            Credentials = JsonSerializer.Serialize(metadata),
            Status = SourceStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Sources.Add(source);
        if (dto.Purpose == 0)
        {
            await StopSearchAsync(dto.ProfileId, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(source);
    }

    public async Task<SourceDto> UpdateSourceAsync(Guid id, Guid accountId, UpdateSourceDto dto, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.Sources
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.Id == id && s.Profile!.AccountId == accountId, cancellationToken);

        if (source == null) throw new KeyNotFoundException("Source not found.");

        var previousPurpose = MapToDto(source).Purpose;
        var previousStatus = source.Status;

        if (!string.IsNullOrWhiteSpace(dto.Name)) source.Name = dto.Name;
        if (!string.IsNullOrWhiteSpace(dto.Url)) source.Url = dto.Url;
        if (dto.ChatId.HasValue) source.ChatId = dto.ChatId;
        if (dto.Credentials != null) source.Credentials = dto.Credentials;
        if (dto.Status.HasValue)
        {
            source.Status = dto.Status.Value;
            if (dto.Status.Value == SourceStatus.Active)
            {
                source.LastError = null;
            }
        }

        if (dto.Purpose.HasValue)
        {
            var metadata = ReadMetadata(source.Credentials);
            metadata.Purpose = dto.Purpose.Value;
            source.Credentials = JsonSerializer.Serialize(metadata);
        }

        var nextPurpose = dto.Purpose ?? previousPurpose;
        var purposeChanged = dto.Purpose.HasValue && previousPurpose != nextPurpose;
        var statusChanged = previousStatus != source.Status;
        if ((previousPurpose == 0 || nextPurpose == 0) && (purposeChanged || statusChanged))
        {
            await StopSearchAsync(source.ProfileId, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(source);
    }

    public async Task DeleteSourceAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.Sources
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.Id == id && s.Profile!.AccountId == accountId, cancellationToken);

        if (source == null) return;

        if (source.Type == SourceType.Kwork)
        {
            source.Status = SourceStatus.Pending;
            source.LastError = null;
            await StopSearchAsync(source.ProfileId, cancellationToken);

            var connection = await _dbContext.ExchangeConnections
                .FirstOrDefaultAsync(c => c.ProfileId == source.ProfileId && c.ExchangeType == ExchangeType.Kwork, cancellationToken);

            if (connection != null)
            {
                connection.IsConnected = false;
                connection.RequiresReconnect = false;
                connection.LastError = null;
                connection.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var purpose = MapToDto(source).Purpose;
        _dbContext.Sources.Remove(source);
        if (purpose == 0)
        {
            await StopSearchAsync(source.ProfileId, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task StopSearchAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SearchSettings
            .FirstOrDefaultAsync(s => s.ProfileId == profileId, cancellationToken);

        if (settings != null && settings.IsEnabled)
        {
            settings.IsEnabled = false;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public async Task<ValidateSourceResponseDto> ValidateSourceAsync(Guid accountId, string url, int purpose = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _telegramValidator.ValidateChatAsync(accountId.ToString(), url);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("NOT_AUTHORIZED") || msg.Contains("AUTH_KEY_UNREGISTERED"))
                return new ValidateSourceResponseDto { IsValid = false, ErrorCode = "NOT_AUTHORIZED" };
            if (msg.Contains("NOT_A_MEMBER"))
                return new ValidateSourceResponseDto { IsValid = false, ErrorCode = "NOT_A_MEMBER" };
            if (msg.Contains("READ_ONLY") && purpose == 0)
                return new ValidateSourceResponseDto { IsValid = true, ErrorCode = "READ_ONLY" };
            if (msg.Contains("READ_ONLY"))
                return new ValidateSourceResponseDto { IsValid = false, ErrorCode = "READ_ONLY" };
            if (msg.Contains("NOT_FOUND") || msg.Contains("Invalid Telegram URL") || msg.Contains("not a channel/group"))
                return new ValidateSourceResponseDto { IsValid = false, ErrorCode = "NOT_FOUND" };
                
            throw;
        }
    }

    private static SourceDto MapToDto(Source source)
    {
        var metadata = ReadMetadata(source.Credentials);
        return new SourceDto(
            source.Id,
            source.ProfileId,
            source.Type,
            metadata.Purpose,
            source.Name,
            source.Url,
            source.ChatId,
            source.Status,
            metadata.MemberCount,
            metadata.AvatarUrl,
            metadata.BaseUrl,
            metadata.TopicId,
            metadata.TopicName,
            !string.IsNullOrWhiteSpace(metadata.TopicId),
            source.LastError,
            source.LastScraped,
            source.CreatedAt
        );
    }

    private static SourceMetadata ReadMetadata(string? credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials))
        {
            return new SourceMetadata();
        }

        try
        {
            return JsonSerializer.Deserialize<SourceMetadata>(credentials) ?? new SourceMetadata();
        }
        catch
        {
            return new SourceMetadata();
        }
    }

    private static async Task<SourceMetadata> BuildMetadataAsync(CreateSourceDto dto, CancellationToken cancellationToken)
    {
        var metadata = new SourceMetadata { Purpose = dto.Purpose };
        if (dto.Type != SourceType.Telegram || string.IsNullOrWhiteSpace(dto.Url))
        {
            return metadata;
        }

        try
        {
            var topic = ParseTelegramTopic(dto.Url);
            metadata.BaseUrl = topic.BaseUrl;
            metadata.TopicId = topic.TopicId;
            metadata.TopicName = topic.TopicId == null ? null : dto.Name;

            var publicUrl = NormalizeTelegramPublicUrl(metadata.BaseUrl ?? dto.Url);
            if (publicUrl == null)
            {
                return metadata;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, publicUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 ClientScout/1.0");

            using var response = await TelegramMetadataClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return metadata;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            metadata.Name = HtmlDecode(FindMeta(html, "og:title"));
            metadata.AvatarUrl = HtmlDecode(FindMeta(html, "og:image"));

            var extraText = HtmlDecode(Regex.Match(
                html,
                "<div[^>]*class=[\"'][^\"']*tgme_page_extra[^\"']*[\"'][^>]*>(?<text>.*?)</div>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups["text"].Value);

            metadata.MemberCount = ParseMemberCount(extraText)
                ?? ParseMemberCount(HtmlDecode(FindMeta(html, "og:description")));
        }
        catch
        {
            // Telegram enrichment is best-effort. The source still needs to be saved.
        }

        return metadata;
    }

    private static string NormalizeSourceUrl(string url)
    {
        var topic = ParseTelegramTopic(url);
        if (topic.BaseUrl != null)
        {
            return topic.TopicId == null
                ? topic.BaseUrl.TrimEnd('/').ToLowerInvariant()
                : $"{topic.BaseUrl.TrimEnd('/').ToLowerInvariant()}/{topic.TopicId}";
        }

        return url.Trim().TrimEnd('/').ToLowerInvariant();
    }

    private static TelegramTopicInfo ParseTelegramTopic(string url)
    {
        var value = url.Trim();
        if (value.StartsWith("@", StringComparison.Ordinal))
        {
            return new TelegramTopicInfo($"https://t.me/{value[1..]}", null);
        }

        if (value.StartsWith("t.me/", StringComparison.OrdinalIgnoreCase))
        {
            value = $"https://{value}";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (!uri.Host.Equals("t.me", StringComparison.OrdinalIgnoreCase) &&
             !uri.Host.Equals("www.t.me", StringComparison.OrdinalIgnoreCase)))
        {
            return new TelegramTopicInfo(null, null);
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || segments[0].Equals("c", StringComparison.OrdinalIgnoreCase))
        {
            return new TelegramTopicInfo(null, null);
        }

        var baseUrl = $"{uri.Scheme}://{uri.Host}/{segments[0]}";
        var topicId = segments.Length > 1 && int.TryParse(segments[1], out _) ? segments[1] : null;
        return new TelegramTopicInfo(baseUrl, topicId);
    }

    private static string? NormalizeTelegramPublicUrl(string url)
    {
        var value = url.Trim();
        if (value.StartsWith("@", StringComparison.Ordinal))
        {
            return $"https://t.me/{value[1..]}";
        }

        if (value.StartsWith("t.me/", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{value}";
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Host.Equals("t.me", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.EndsWith(".t.me", StringComparison.OrdinalIgnoreCase)))
        {
            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length == 0)
            {
                return uri.ToString();
            }

            return $"{uri.Scheme}://{uri.Host}/{segments[0]}";
        }

        return null;
    }

    private static string? FindMeta(string html, string property)
    {
        var pattern = $"<meta[^>]+(?:property|name)=[\"']{Regex.Escape(property)}[\"'][^>]+content=[\"'](?<content>.*?)[\"'][^>]*>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success) return match.Groups["content"].Value;

        pattern = $"<meta[^>]+content=[\"'](?<content>.*?)[\"'][^>]+(?:property|name)=[\"']{Regex.Escape(property)}[\"'][^>]*>";
        match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["content"].Value : null;
    }

    private static int? ParseMemberCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var normalized = Regex.Replace(text, @"\s+", " ");
        var match = Regex.Match(
            normalized,
            @"(?<count>\d[\d\s,.]*)(?<suffix>\s*[kKmMКкМм])?\s*(members|subscribers|member|subscriber|участник|участников|подписчик|подписчиков)",
            RegexOptions.IgnoreCase);

        if (!match.Success) return null;

        var rawNumber = match.Groups["count"].Value.Replace(" ", "").Replace(",", ".");
        if (!decimal.TryParse(rawNumber, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        var suffix = match.Groups["suffix"].Value.Trim().ToLowerInvariant();
        if (suffix is "k" or "к") number *= 1000;
        if (suffix is "m" or "м") number *= 1_000_000;

        return (int)Math.Round(number);
    }

    private static string? HtmlDecode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : WebUtility.HtmlDecode(Regex.Replace(value, "<.*?>", string.Empty)).Trim();
    }

    private sealed class SourceMetadata
    {
        public int Purpose { get; set; }
        public string? Name { get; set; }
        public int? MemberCount { get; set; }
        public string? AvatarUrl { get; set; }
        public string? BaseUrl { get; set; }
        public string? TopicId { get; set; }
        public string? TopicName { get; set; }
    }

    private sealed record TelegramTopicInfo(string? BaseUrl, string? TopicId);
}
