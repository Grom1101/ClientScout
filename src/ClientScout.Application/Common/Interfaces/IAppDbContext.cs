using System.Threading;
using System.Threading.Tasks;
using ClientScout.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Account> Accounts { get; }
    DbSet<User> Users { get; }
    DbSet<Profile> Profiles { get; }
    DbSet<ExchangeConnection> ExchangeConnections { get; }
    DbSet<SearchSettings> SearchSettings { get; }
    DbSet<Source> Sources { get; }
    DbSet<JobLead> JobLeads { get; }
    DbSet<MessageTemplate> MessageTemplates { get; }
    DbSet<OutreachCampaign> OutreachCampaigns { get; }
    DbSet<OutreachLog> OutreachLogs { get; }
    DbSet<UserbotSession> UserbotSessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
