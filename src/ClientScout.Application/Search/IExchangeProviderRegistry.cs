using ClientScout.Domain.Enums;

namespace ClientScout.Application.Search;

public interface IExchangeProviderRegistry
{
    IReadOnlyList<IExchangeProvider> GetProviders();
    IExchangeProvider GetRequired(ExchangeType exchangeType);
}
