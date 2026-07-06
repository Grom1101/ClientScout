namespace ClientScout.Application.Search;

public interface IAiUsageLogger
{
    Task LogAsync(
        AiProviderModelCandidate candidate,
        int statusCode,
        string body,
        string? errorMessage,
        CancellationToken cancellationToken);
}
