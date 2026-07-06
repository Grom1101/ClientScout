using ClientScout.Domain.Enums;

namespace ClientScout.Application.Search;

public interface IExchangeProvider
{
    ExchangeType ExchangeType { get; }
    SourceType SourceType { get; }
    string Key { get; }
    string DisplayName { get; }
    string SourceName { get; }
    string SourceUrl { get; }
    string SourceCredentials { get; }
    bool SupportsBrowserLogin { get; }
    bool SupportsManualSession { get; }
    bool IsAvailable { get; }
}
