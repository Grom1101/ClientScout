using System.Collections.Concurrent;

namespace ClientScout.Application.Search;

public enum AiFailureKind
{
    None,
    MissingProvider,
    RateLimited,
    RequestFailed,
    InvalidResponse
}

public sealed class AiJsonClient
{
    private static readonly ConcurrentDictionary<string, AiProviderRuntimeState> RuntimeStates = new(StringComparer.OrdinalIgnoreCase);

    private readonly IAiProviderRegistry _providerRegistry;
    private readonly OpenAiCompatibleAiProviderClient _providerClient;

    public AiJsonClient(
        IAiProviderRegistry providerRegistry,
        OpenAiCompatibleAiProviderClient providerClient)
    {
        _providerRegistry = providerRegistry;
        _providerClient = providerClient;
    }

    public bool IsAvailable => _providerRegistry.GetProviders(AiTaskKind.LeadClassification).Any();
    public AiFailureKind LastFailureKind { get; private set; } = AiFailureKind.None;
    private int LastFailureCooldownSeconds { get; set; }

    public async Task<T?> GenerateJsonAsync<T>(
        string prompt,
        AiTaskKind taskKind,
        CancellationToken cancellationToken = default)
    {
        LastFailureKind = AiFailureKind.None;
        LastFailureCooldownSeconds = 0;
        var estimatedTokens = EstimateTokens(prompt);

        foreach (var candidate in _providerRegistry.GetCandidates(taskKind, prompt.Length))
        {
            var runtime = RuntimeStates.GetOrAdd(candidate.RuntimeKey, _ => new AiProviderRuntimeState(candidate.Provider.MaxConcurrency));
            if (runtime.IsCoolingDown)
            {
                continue;
            }

            if (!runtime.TryReserve(candidate.Provider, estimatedTokens))
            {
                LastFailureKind = AiFailureKind.RateLimited;
                continue;
            }

            if (!await runtime.TryEnterAsync(cancellationToken))
            {
                LastFailureKind = AiFailureKind.RateLimited;
                continue;
            }

            try
            {
                var result = await _providerClient.GenerateJsonAsync<T>(candidate, prompt, cancellationToken);
                LastFailureKind = result.FailureKind;
                LastFailureCooldownSeconds = result.CooldownSeconds;

                if (result.Value != null)
                {
                    LastFailureKind = AiFailureKind.None;
                    return result.Value;
                }

                if (LastFailureKind is AiFailureKind.RateLimited or AiFailureKind.RequestFailed)
                {
                    runtime.CoolDown(LastFailureCooldownSeconds > 0
                        ? LastFailureCooldownSeconds
                        : candidate.Provider.CooldownSeconds);
                }
            }
            finally
            {
                runtime.Exit();
            }
        }

        if (LastFailureKind == AiFailureKind.None)
        {
            LastFailureKind = AiFailureKind.MissingProvider;
        }

        return default;
    }

    public int GetOptimalBatchSize(AiTaskKind taskKind)
    {
        var primaryCandidate = _providerRegistry.GetCandidates(taskKind, 0).FirstOrDefault();
        if (primaryCandidate == null)
        {
            return 4;
        }

        return primaryCandidate.Model.MaxInputChars >= 35000 ? 15 : 4;
    }

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    private sealed class AiProviderRuntimeState
    {
        private readonly object _gate = new();
        private readonly Queue<DateTimeOffset> _requestWindow = new();
        private readonly Queue<(DateTimeOffset At, int Tokens)> _tokenWindow = new();
        private readonly SemaphoreSlim _concurrency;
        private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;

        public AiProviderRuntimeState(int maxConcurrency)
        {
            _concurrency = new SemaphoreSlim(Math.Max(1, maxConcurrency), Math.Max(1, maxConcurrency));
        }

        public bool IsCoolingDown => _cooldownUntil > DateTimeOffset.UtcNow;

        public bool TryReserve(AiProviderOptions provider, int estimatedTokens)
        {
            lock (_gate)
            {
                var now = DateTimeOffset.UtcNow;
                Trim(now);

                if (_cooldownUntil > now)
                {
                    return false;
                }

                var currentTokens = _tokenWindow.Sum(item => item.Tokens);
                if (_requestWindow.Count >= provider.MaxRequestsPerMinute ||
                    currentTokens + estimatedTokens > provider.MaxTokensPerMinute)
                {
                    return false;
                }

                _requestWindow.Enqueue(now);
                _tokenWindow.Enqueue((now, estimatedTokens));
                return true;
            }
        }

        public Task<bool> TryEnterAsync(CancellationToken cancellationToken)
        {
            return _concurrency.WaitAsync(0, cancellationToken);
        }

        public void Exit()
        {
            _concurrency.Release();
        }

        public void CoolDown(int seconds)
        {
            lock (_gate)
            {
                _cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(seconds);
            }
        }

        private void Trim(DateTimeOffset now)
        {
            var cutoff = now.AddMinutes(-1);
            while (_requestWindow.TryPeek(out var requestAt) && requestAt < cutoff)
            {
                _requestWindow.Dequeue();
            }

            while (_tokenWindow.TryPeek(out var tokenItem) && tokenItem.At < cutoff)
            {
                _tokenWindow.Dequeue();
            }
        }
    }
}
