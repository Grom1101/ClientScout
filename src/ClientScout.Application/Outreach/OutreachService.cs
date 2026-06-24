using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IAppDbContext _dbContext;

    public OutreachService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<UserbotSessionDto>> GetSessionsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.UserbotSessions
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new UserbotSessionDto(s.Id, s.Phone, s.DisplayName, s.IsActive, s.CreatedAt)).ToList();
    }

    public async Task<UserbotSessionDto> AddSessionAsync(long userId, CreateUserbotSessionDto dto, CancellationToken cancellationToken = default)
    {
        var session = new UserbotSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
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

    public async Task<List<MessageTemplateDto>> GetTemplatesAsync(Guid profileId, long userId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var templates = await _dbContext.MessageTemplates
            .Where(t => t.ProfileId == profileId)
            .ToListAsync(cancellationToken);

        return templates.Select(t => new MessageTemplateDto(t.Id, t.Name, t.Content, t.CreatedAt)).ToList();
    }

    public async Task<MessageTemplateDto> CreateTemplateAsync(long userId, CreateMessageTemplateDto dto, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == dto.ProfileId && p.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var template = new MessageTemplate
        {
            Id = Guid.NewGuid(),
            ProfileId = dto.ProfileId,
            Name = dto.Name,
            Content = dto.Content,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.MessageTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MessageTemplateDto(template.Id, template.Name, template.Content, template.CreatedAt);
    }

    public async Task<List<OutreachCampaignDto>> GetCampaignsAsync(Guid profileId, long userId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == profileId && p.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var campaigns = await _dbContext.OutreachCampaigns
            .Where(c => c.ProfileId == profileId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return campaigns.Select(c => new OutreachCampaignDto(
            c.Id, c.ProfileId, c.TemplateId, c.TargetChatsJson,
            c.DelayMinSec, c.DelayMaxSec, c.Status,
            c.SentCount, c.ErrorCount, c.CreatedAt)).ToList();
    }

    public async Task<OutreachCampaignDto> CreateCampaignAsync(long userId, CreateOutreachCampaignDto dto, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _dbContext.Profiles.AnyAsync(p => p.Id == dto.ProfileId && p.UserId == userId, cancellationToken);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var campaign = new OutreachCampaign
        {
            Id = Guid.NewGuid(),
            ProfileId = dto.ProfileId,
            TemplateId = dto.TemplateId,
            TargetChatsJson = dto.TargetChatsJson,
            DelayMinSec = dto.DelayMinSec,
            DelayMaxSec = dto.DelayMaxSec,
            Status = CampaignStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.OutreachCampaigns.Add(campaign);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OutreachCampaignDto(
            campaign.Id, campaign.ProfileId, campaign.TemplateId, campaign.TargetChatsJson,
            campaign.DelayMinSec, campaign.DelayMaxSec, campaign.Status,
            campaign.SentCount, campaign.ErrorCount, campaign.CreatedAt);
    }

    public async Task UpdateCampaignStatusAsync(Guid id, long userId, CampaignStatus status, CancellationToken cancellationToken = default)
    {
        var campaign = await _dbContext.OutreachCampaigns
            .Include(c => c.Profile)
            .FirstOrDefaultAsync(c => c.Id == id && c.Profile!.UserId == userId, cancellationToken);

        if (campaign == null) throw new KeyNotFoundException();

        campaign.Status = status;
        if (status == CampaignStatus.Running && campaign.StartedAt == null)
        {
            campaign.StartedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
