using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Outreach.Models;

namespace ClientScout.Application.Outreach;

public interface IOutreachService
{
    // Sessions
    Task<List<UserbotSessionDto>> GetSessionsAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserbotSessionDto> AddSessionAsync(long userId, CreateUserbotSessionDto dto, CancellationToken cancellationToken = default);

    // Templates
    Task<List<MessageTemplateDto>> GetTemplatesAsync(Guid profileId, long userId, CancellationToken cancellationToken = default);
    Task<MessageTemplateDto> CreateTemplateAsync(long userId, CreateMessageTemplateDto dto, CancellationToken cancellationToken = default);

    // Campaigns
    Task<List<OutreachCampaignDto>> GetCampaignsAsync(Guid profileId, long userId, CancellationToken cancellationToken = default);
    Task<OutreachCampaignDto> CreateCampaignAsync(long userId, CreateOutreachCampaignDto dto, CancellationToken cancellationToken = default);
    Task UpdateCampaignStatusAsync(Guid id, long userId, ClientScout.Domain.Enums.CampaignStatus status, CancellationToken cancellationToken = default);
}
