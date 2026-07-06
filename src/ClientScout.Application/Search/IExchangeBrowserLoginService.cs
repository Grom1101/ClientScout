using ClientScout.Application.Search.Models;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Search;

public interface IExchangeBrowserLoginService
{
    ExchangeType ExchangeType { get; }
    Task<ExchangeLoginStartResult> StartAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken = default);
    ExchangeLoginFlowStatusDto GetStatus(Guid flowId);
}
