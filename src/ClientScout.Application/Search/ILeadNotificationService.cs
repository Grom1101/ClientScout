using ClientScout.Domain.Entities;

namespace ClientScout.Application.Search;

public interface ILeadNotificationService
{
    Task NotifyLeadAsync(Account account, JobLead lead, CancellationToken cancellationToken = default);
    Task NotifySearchStoppedAsync(Account account, string reason, CancellationToken cancellationToken = default);
}
