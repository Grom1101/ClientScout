using ClientScout.Domain.Enums;

namespace ClientScout.Application.Search;

public sealed class ExchangeProviderRegistry : IExchangeProviderRegistry
{
    private readonly IReadOnlyList<IExchangeProvider> _providers;

    public ExchangeProviderRegistry(IEnumerable<IExchangeProvider> providers)
    {
        _providers = providers
            .OrderBy(provider => provider.ExchangeType)
            .ToArray();
    }

    public IReadOnlyList<IExchangeProvider> GetProviders()
    {
        return _providers;
    }

    public IExchangeProvider GetRequired(ExchangeType exchangeType)
    {
        return _providers.FirstOrDefault(provider => provider.ExchangeType == exchangeType)
            ?? throw new ArgumentException("UNSUPPORTED_EXCHANGE");
    }
}
