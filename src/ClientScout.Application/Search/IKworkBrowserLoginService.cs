using ClientScout.Application.Search.Models;

namespace ClientScout.Application.Search;

public interface IKworkBrowserLoginService
{
    Task<ExchangeLoginStartResult> StartAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken = default);
    ExchangeLoginFlowStatusDto GetStatus(Guid flowId);
}
