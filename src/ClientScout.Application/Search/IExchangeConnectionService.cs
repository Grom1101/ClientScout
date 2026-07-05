using ClientScout.Application.Search.Models;
using ClientScout.Domain.Enums;

namespace ClientScout.Application.Search;

public interface IExchangeConnectionService
{
    Task<List<ExchangeConnectionDto>> GetConnectionsAsync(Guid profileId, Guid accountId, CancellationToken cancellationToken = default);
    Task<ExchangeLoginStartResult> StartLoginAsync(Guid accountId, ExchangeLoginStartDto dto, CancellationToken cancellationToken = default);
    Task<ExchangeConnectionDto> ConnectAsync(Guid accountId, ConnectExchangeDto dto, CancellationToken cancellationToken = default);
    Task<ExchangeConnectionDto> DisconnectAsync(Guid accountId, DisconnectExchangeDto dto, CancellationToken cancellationToken = default);
    Task MarkRequiresReconnectAsync(Guid profileId, ExchangeType exchangeType, string error, CancellationToken cancellationToken = default);
}
