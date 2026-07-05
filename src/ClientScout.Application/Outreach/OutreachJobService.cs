using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Telegram;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClientScout.Application.Outreach;

public class OutreachJobService
{
    private static readonly SemaphoreSlim ProcessingLock = new(1, 1);

    private readonly IAppDbContext _dbContext;
    private readonly ITelegramClientManager _telegramClientManager;
    private readonly ILogger<OutreachJobService> _logger;

    public OutreachJobService(IAppDbContext dbContext, ITelegramClientManager telegramClientManager, ILogger<OutreachJobService> logger)
    {
        _dbContext = dbContext;
        _telegramClientManager = telegramClientManager;
        _logger = logger;
    }

    public async Task ProcessCampaignsAsync(CancellationToken cancellationToken = default)
    {
        if (!await ProcessingLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Skipped outreach processing because another run is still active.");
            return;
        }

        try
        {
            var activeCampaigns = await _dbContext.OutreachCampaigns
                .Include(c => c.Profile)
                .Include(c => c.Template)
                .Where(c => c.Status == CampaignStatus.Running)
                .ToListAsync(cancellationToken);

            foreach (var campaign in activeCampaigns)
            {
                try
                {
                    if (campaign.NextRunAt.HasValue && campaign.NextRunAt.Value > DateTimeOffset.UtcNow)
                    {
                        continue;
                    }

                    var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == campaign.Profile!.AccountId, cancellationToken);
                    if (account?.TelegramUserId == null)
                    {
                        campaign.Status = CampaignStatus.Paused;
                        _logger.LogWarning("Paused campaign {CampaignId} because no linked Telegram account found.", campaign.Id);
                        continue;
                    }

                    if (campaign.Template == null)
                    {
                        campaign.Status = CampaignStatus.Paused;
                        _logger.LogWarning("Paused campaign {CampaignId} because template was not found.", campaign.Id);
                        continue;
                    }

                    var targetIds = ParseTargetIds(campaign.TargetChatsJson);
                    var sources = await _dbContext.Sources
                        .Where(s => targetIds.Contains(s.Id) && s.ProfileId == campaign.ProfileId && s.Status == SourceStatus.Active)
                        .ToListAsync(cancellationToken);

                    if (sources.Count == 0)
                    {
                        campaign.Status = CampaignStatus.Paused;
                        _logger.LogWarning("Paused campaign {CampaignId} because it has no active targets.", campaign.Id);
                        continue;
                    }

                    var templateContent = ParseTemplateContent(campaign.Template.Content);
                    var message = templateContent.Content;
                    var attachmentUrl = campaign.Template.AttachmentUrls.FirstOrDefault() ?? templateContent.AttachmentUrls.FirstOrDefault();
                    foreach (var source in sources)
                    {
                        if (!await IsCampaignStillRunningAsync(campaign.Id, cancellationToken))
                        {
                            _logger.LogInformation("Stopped processing campaign {CampaignId} before sending to source {SourceId}.", campaign.Id, source.Id);
                            break;
                        }

                        if (!IsInsideScheduleWindow(campaign, DateTimeOffset.UtcNow))
                        {
                            campaign.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(5);
                            _logger.LogInformation("Skipped campaign {CampaignId} because it is outside the schedule window.", campaign.Id);
                            break;
                        }

                        try
                        {
                            if (!string.IsNullOrWhiteSpace(attachmentUrl) &&
                                TryResolveAttachment(attachmentUrl, out var filePath, out var mimeType))
                            {
                                try
                                {
                                    await _telegramClientManager.SendMessageWithAttachmentAsync(account.Id.ToString(), source.Url, message, filePath, mimeType);
                                }
                                catch
                                {
                                    await _telegramClientManager.SendTextMessageAsync(account.Id.ToString(), source.Url, message);
                                }
                            }
                            else
                            {
                                await _telegramClientManager.SendTextMessageAsync(account.Id.ToString(), source.Url, message);
                            }

                            _dbContext.OutreachLogs.Add(new OutreachLog
                            {
                                Id = Guid.NewGuid(),
                                CampaignId = campaign.Id,
                                ChatId = source.ChatId,
                                ChatName = BuildChatName(source),
                                MessageContent = message,
                                Status = LogStatus.Sent,
                                SentAt = DateTimeOffset.UtcNow
                            });

                            campaign.SentCount++;
                        }
                        catch (Exception sendEx)
                        {
                            _dbContext.OutreachLogs.Add(new OutreachLog
                            {
                                Id = Guid.NewGuid(),
                                CampaignId = campaign.Id,
                                ChatId = source.ChatId,
                                ChatName = BuildChatName(source),
                                MessageContent = message,
                                Status = LogStatus.Error,
                                ErrorMessage = sendEx.Message,
                                SentAt = DateTimeOffset.UtcNow
                            });

                            campaign.ErrorCount++;
                            _logger.LogError(sendEx, "Failed to send campaign {CampaignId} to source {SourceId}", campaign.Id, source.Id);
                        }

                        if (sources.Count > 1 && campaign.DelayMaxSec > 0)
                        {
                            var delay = Random.Shared.Next(Math.Max(0, campaign.DelayMinSec), Math.Max(campaign.DelayMinSec, campaign.DelayMaxSec) + 1);
                            if (!await DelayWhileRunningAsync(campaign.Id, TimeSpan.FromSeconds(delay), cancellationToken))
                            {
                                _logger.LogInformation("Stopped processing campaign {CampaignId} during send delay.", campaign.Id);
                                break;
                            }
                        }
                    }

                    if (await IsCampaignStillRunningAsync(campaign.Id, cancellationToken))
                    {
                        campaign.CurrentIndex++;
                        campaign.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(5, campaign.PeriodicityMinutes));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing campaign {CampaignId}", campaign.Id);
                    campaign.ErrorCount++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            ProcessingLock.Release();
        }
    }

    private async Task<bool> IsCampaignStillRunningAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.OutreachCampaigns
            .AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(c => c.Status == CampaignStatus.Running)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> DelayWhileRunningAsync(Guid campaignId, TimeSpan delay, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(delay);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!await IsCampaignStillRunningAsync(campaignId, cancellationToken))
            {
                return false;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), cancellationToken);
        }

        return await IsCampaignStillRunningAsync(campaignId, cancellationToken);
    }

    private static HashSet<Guid> ParseTargetIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json)?.ToHashSet() ?? new HashSet<Guid>();
        }
        catch
        {
            return new HashSet<Guid>();
        }
    }

    private static bool IsInsideScheduleWindow(OutreachCampaign campaign, DateTimeOffset utcNow)
    {
        if (!string.Equals(campaign.ScheduleMode, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TimeOnly.TryParse(campaign.ScheduleStartTime, out var start) ||
            !TimeOnly.TryParse(campaign.ScheduleEndTime, out var end))
        {
            return true;
        }

        var localTime = utcNow.AddMinutes(-campaign.TimezoneOffsetMinutes).TimeOfDay;
        var current = TimeOnly.FromTimeSpan(localTime);

        return start <= end
            ? current >= start && current <= end
            : current >= start || current <= end;
    }

    private static bool TryResolveAttachment(string url, out string filePath, out string mimeType)
    {
        filePath = string.Empty;
        mimeType = string.Empty;

        if (!url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "ClientScout.Api", "wwwroot", relative),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", relative)
        };

        filePath = candidates.FirstOrDefault(File.Exists) ?? string.Empty;
        if (filePath.Length == 0)
        {
            return false;
        }

        mimeType = GetMimeType(filePath);
        return true;
    }

    private static string GetMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static TemplateContentPayload ParseTemplateContent(string storedContent)
    {
        if (string.IsNullOrWhiteSpace(storedContent)) return new TemplateContentPayload(string.Empty, Array.Empty<string>());

        try
        {
            var payload = JsonSerializer.Deserialize<TemplateContentPayload>(storedContent);
            return payload ?? new TemplateContentPayload(storedContent, Array.Empty<string>());
        }
        catch
        {
            return new TemplateContentPayload(storedContent, Array.Empty<string>());
        }
    }

    private sealed record TemplateContentPayload(string Content, string[] AttachmentUrls);

    private static string BuildChatName(ClientScout.Domain.Entities.Source source)
    {
        var name = string.IsNullOrWhiteSpace(source.Name) ? "Telegram" : source.Name.Trim();
        if (string.IsNullOrWhiteSpace(source.Credentials)) return name;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(source.Credentials);
            var topic = doc.RootElement.TryGetProperty("TopicName", out var topicProp) && topicProp.ValueKind == System.Text.Json.JsonValueKind.String 
                ? topicProp.GetString() 
                : null;
                
            topic ??= doc.RootElement.TryGetProperty("topicName", out var topicProp2) && topicProp2.ValueKind == System.Text.Json.JsonValueKind.String
                ? topicProp2.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(topic))
            {
                return $"{name} › {topic.Trim()}";
            }
        }
        catch { }

        return name;
    }
}

