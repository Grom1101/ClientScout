namespace ClientScout.Application.Search;

public sealed record AiProviderJsonResult<T>(
    T? Value,
    AiFailureKind FailureKind,
    int CooldownSeconds);
