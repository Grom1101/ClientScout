using ClientScout.Domain.Enums;

namespace ClientScout.Application.Search;

public sealed class KworkExchangeProvider : IExchangeProvider
{
    public ExchangeType ExchangeType => ExchangeType.Kwork;
    public SourceType SourceType => SourceType.Kwork;
    public string Key => "kwork";
    public string DisplayName => "Kwork";
    public string SourceName => "Kwork";
    public string SourceUrl => "https://kwork.ru/projects";
    public string SourceCredentials => "{\"purpose\":0,\"exchange\":\"kwork\"}";
    public bool SupportsBrowserLogin => true;
    public bool SupportsManualSession => true;
    public bool IsAvailable => true;
}
