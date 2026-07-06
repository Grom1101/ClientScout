namespace ClientScout.Application.Search;

public sealed record AiProviderModelCandidate(AiProviderOptions Provider, AiModelOptions Model)
{
    public string RuntimeKey => $"{Provider.Name}:{Model.Id}";
}
