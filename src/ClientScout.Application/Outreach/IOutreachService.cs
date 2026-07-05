using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Outreach.Models;

namespace ClientScout.Application.Outreach;

public interface IOutreachService
{
    // Sessions
    Task<List<UserbotSessionDto>> GetSessionsAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<UserbotSessionDto> AddSessionAsync(Guid accountId, CreateUserbotSessionDto dto, CancellationToken cancellationToken = default);

    // Templates
    Task<List<MessageTemplateDto>> GetTemplatesAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default);
    Task<MessageTemplateDto> CreateTemplateAsync(Guid accountId, CreateMessageTemplateDto dto, CancellationToken cancellationToken = default);
    Task<MessageTemplateDto> UpdateTemplateAsync(Guid id, Guid accountId, UpdateMessageTemplateDto dto, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(Guid id, Guid accountId, CancellationToken cancellationToken = default);

    // Campaigns
    Task<List<OutreachCampaignDto>> GetCampaignsAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default);
    Task<OutreachCampaignDto> CreateCampaignAsync(Guid accountId, CreateOutreachCampaignDto dto, CancellationToken cancellationToken = default);
    Task<OutreachCampaignDto> UpdateCampaignStatusAsync(Guid id, Guid accountId, ClientScout.Domain.Enums.CampaignStatus status, CancellationToken cancellationToken = default);
}

