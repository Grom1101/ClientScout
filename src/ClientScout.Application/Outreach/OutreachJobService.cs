using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientScout.Application.Common.Interfaces;
using ClientScout.Domain.Entities;
using ClientScout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClientScout.Application.Outreach;

public class OutreachJobService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<OutreachJobService> _logger;

    public OutreachJobService(IAppDbContext dbContext, ILogger<OutreachJobService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ProcessCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var activeCampaigns = await _dbContext.OutreachCampaigns
            .Include(c => c.Profile)
            .Where(c => c.Status == CampaignStatus.Running)
            .ToListAsync(cancellationToken);

        foreach (var campaign in activeCampaigns)
        {
            try
            {
                // MVP logic: Find active userbot session
                var session = await _dbContext.UserbotSessions
                    .FirstOrDefaultAsync(s => s.UserId == campaign.Profile!.UserId && s.IsActive, cancellationToken);
                
                if (session == null)
                {
                    campaign.Status = CampaignStatus.Paused;
                    _logger.LogWarning("Paused campaign {CampaignId} because no active userbot session found.", campaign.Id);
                    continue;
                }

                // In a real app we parse TargetChatsJson, get the next target, and send via WTelegramClient.
                // For MVP, we'll simulate sending and increment the counter.
                
                await Task.Delay(500, cancellationToken); // Simulate API call
                
                var log = new OutreachLog
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    ChatId = null, // Stub
                    ChatName = "StubUser",
                    Status = LogStatus.Sent,
                    SentAt = DateTimeOffset.UtcNow
                };

                _dbContext.OutreachLogs.Add(log);
                campaign.SentCount++;

                // Pseudo logic: pause if finished
                if (campaign.SentCount >= 5) // MVP condition
                {
                    campaign.Status = CampaignStatus.Done;
                    campaign.FinishedAt = DateTimeOffset.UtcNow;
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
}
