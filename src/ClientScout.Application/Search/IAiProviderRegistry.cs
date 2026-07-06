namespace ClientScout.Application.Search;

public interface IAiProviderRegistry
{
    IReadOnlyList<AiProviderOptions> GetProviders(AiTaskKind taskKind);
    IEnumerable<AiProviderModelCandidate> GetCandidates(AiTaskKind taskKind, int promptChars);
}
