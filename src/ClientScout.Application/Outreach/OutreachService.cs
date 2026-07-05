using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Application.Outreach.Models;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Application.Outreach;

public class OutreachService : IOutreachService
{
    private const int MaxMessageLength = 1024;
    private const int MaxAttachmentCount = 1;

    private readonly IAppDbContext _dbContext;

    public OutreachService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<UserbotSessionDto>> GetSessionsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account == null || account.TelegramUserId == null) return new List<UserbotSessionDto>();

        var sessions = await _dbContext.UserbotSessions
            .Where(s => s.UserId == account.TelegramUserId.Value)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new UserbotSessionDto(s.Id, s.Phone, s.DisplayName, s.IsActive, s.CreatedAt)).ToList();
    }

    public async Task<UserbotSessionDto> AddSessionAsync(Guid accountId, CreateUserbotSessionDto dto, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account == null || account.TelegramUserId == null) throw new UnauthorizedAccessException("Telegram account is not linked.");

        var session = new UserbotSession
        {
            Id = Guid.NewGuid(),
            UserId = account.TelegramUserId.Value,
            Phone = dto.Phone,
            SessionData = dto.SessionData,
            DisplayName = dto.DisplayName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.UserbotSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserbotSessionDto(session.Id, session.Phone, session.DisplayName, session.IsActive, session.CreatedAt);
    }

    public async Task<List<MessageTemplateDto>> GetTemplatesAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var templates = await _dbContext.MessageTemplates
            .Where(t => t.ProfileId == profileId)
            .ToListAsync(cancellationToken);

        return templates.Select(MapTemplateToDto).ToList();
    }

    public async Task<MessageTemplateDto> CreateTemplateAsync(Guid accountId, CreateMessageTemplateDto dto, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == dto.ProfileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();
        ValidateTemplate(dto.Content, dto.AttachmentUrls);

        var template = new MessageTemplate
        {
            Id = Guid.NewGuid(),
            ProfileId = dto.ProfileId,
            Name = dto.Name,
            Content = dto.Content,
            AttachmentUrls = dto.AttachmentUrls ?? Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.MessageTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapTemplateToDto(template);
    }

    public async Task<MessageTemplateDto> UpdateTemplateAsync(Guid id, Guid accountId, UpdateMessageTemplateDto dto, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.MessageTemplates
            .Include(t => t.Profile)
            .FirstOrDefaultAsync(t => t.Id == id && t.Profile!.AccountId == accountId, cancellationToken);

        if (template == null) throw new KeyNotFoundException("Template not found.");

        if (!string.IsNullOrWhiteSpace(dto.Name)) template.Name = dto.Name;
        ValidateTemplate(dto.Content ?? template.Content, dto.AttachmentUrls ?? template.AttachmentUrls);
        if (dto.Content != null) template.Content = dto.Content;
        if (dto.AttachmentUrls != null) template.AttachmentUrls = dto.AttachmentUrls;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapTemplateToDto(template);
    }

    public async Task DeleteTemplateAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.MessageTemplates
            .Include(t => t.Profile)
            .FirstOrDefaultAsync(t => t.Id == id && t.Profile!.AccountId == accountId, cancellationToken);

        if (template == null) throw new KeyNotFoundException("Template not found.");

        _dbContext.MessageTemplates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<OutreachCampaignDto>> GetCampaignsAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var campaigns = await _dbContext.OutreachCampaigns
            .Where(c => c.ProfileId == profileId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return campaigns.Select(c => new OutreachCampaignDto(
            c.Id, c.ProfileId, c.TemplateId, c.TargetChatsJson,
            c.DelayMinSec, c.DelayMaxSec, c.PeriodicityMinutes,
            c.ScheduleMode, c.ScheduleStartTime, c.ScheduleEndTime, c.TimezoneOffsetMinutes, c.Status,
            c.SentCount, c.ErrorCount, c.CreatedAt)).ToList();
    }

    public async Task<OutreachCampaignDto> CreateCampaignAsync(Guid accountId, CreateOutreachCampaignDto dto, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == dto.ProfileId && p.AccountId == accountId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var campaign = new OutreachCampaign
        {
            Id = Guid.NewGuid(),
            ProfileId = dto.ProfileId,
            TemplateId = dto.TemplateId,
            TargetChatsJson = dto.TargetChatsJson,
            DelayMinSec = dto.DelayMinSec,
            DelayMaxSec = dto.DelayMaxSec,
            PeriodicityMinutes = Math.Max(5, dto.PeriodicityMinutes),
            ScheduleMode = NormalizeScheduleMode(dto.ScheduleMode),
            ScheduleStartTime = dto.ScheduleStartTime,
            ScheduleEndTime = dto.ScheduleEndTime,
            TimezoneOffsetMinutes = dto.TimezoneOffsetMinutes,
            Status = CampaignStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.OutreachCampaigns.Add(campaign);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OutreachCampaignDto(
            campaign.Id, campaign.ProfileId, campaign.TemplateId, campaign.TargetChatsJson,
            campaign.DelayMinSec, campaign.DelayMaxSec, campaign.PeriodicityMinutes,
            campaign.ScheduleMode, campaign.ScheduleStartTime, campaign.ScheduleEndTime, campaign.TimezoneOffsetMinutes, campaign.Status,
            campaign.SentCount, campaign.ErrorCount, campaign.CreatedAt);
    }

    public async Task<OutreachCampaignDto> UpdateCampaignStatusAsync(Guid id, Guid accountId, CampaignStatus status, CancellationToken cancellationToken = default)
    {
        var campaign = await _dbContext.OutreachCampaigns
            .Include(c => c.Profile)
            .FirstOrDefaultAsync(c => c.Id == id && c.Profile!.AccountId == accountId, cancellationToken);

        if (campaign == null) throw new KeyNotFoundException();

        campaign.Status = status;
        if (status == CampaignStatus.Running && campaign.StartedAt == null)
        {
            campaign.StartedAt = DateTimeOffset.UtcNow;
        }
        if (status == CampaignStatus.Running)
        {
            campaign.NextRunAt ??= DateTimeOffset.UtcNow;
        }
        else
        {
            campaign.NextRunAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OutreachCampaignDto(
            campaign.Id, campaign.ProfileId, campaign.TemplateId, campaign.TargetChatsJson,
            campaign.DelayMinSec, campaign.DelayMaxSec, campaign.PeriodicityMinutes,
            campaign.ScheduleMode, campaign.ScheduleStartTime, campaign.ScheduleEndTime, campaign.TimezoneOffsetMinutes, campaign.Status,
            campaign.SentCount, campaign.ErrorCount, campaign.CreatedAt);
    }

    private static void ValidateTemplate(string content, string[]? attachmentUrls)
    {
        if (content.Length > MaxMessageLength)
        {
            throw new ArgumentException($"Message cannot exceed {MaxMessageLength} characters.");
        }

        if ((attachmentUrls?.Length ?? 0) > MaxAttachmentCount)
        {
            throw new ArgumentException($"Only {MaxAttachmentCount} attachment is allowed.");
        }
    }

    private static string NormalizeScheduleMode(string? mode)
    {
        return string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase) ? "custom" : "allday";
    }

    private static MessageTemplateDto MapTemplateToDto(MessageTemplate template)
    {
        var content = ParseTemplateContent(template.Content);
        var attachmentUrls = template.AttachmentUrls.Length > 0 ? template.AttachmentUrls : content.AttachmentUrls;
        return new MessageTemplateDto(template.Id, template.Name, content.Content, attachmentUrls, template.CreatedAt);
    }

    private static string SerializeTemplateContent(string content, string[]? attachmentUrls)
    {
        var payload = new TemplateContentPayload(content, attachmentUrls ?? Array.Empty<string>());
        return JsonSerializer.Serialize(payload);
    }

    private static TemplateContentPayload ParseTemplateContent(string storedContent)
    {
        if (string.IsNullOrWhiteSpace(storedContent))
        {
            return new TemplateContentPayload(string.Empty, Array.Empty<string>());
        }

        try
        {
            return JsonSerializer.Deserialize<TemplateContentPayload>(storedContent)
                ?? new TemplateContentPayload(storedContent, Array.Empty<string>());
        }
        catch
        {
            return new TemplateContentPayload(storedContent, Array.Empty<string>());
        }
    }

    private sealed record TemplateContentPayload(string Content, string[] AttachmentUrls);
}

